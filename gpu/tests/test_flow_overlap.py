"""Check serial and overlapped GPU runs, plus the final EKI observation on CPU.

Run with Python 3.9+ and a published GRAL build:
    python gpu/tests/test_flow_overlap.py /path/to/publish/GRAL.dll
Use --reference to also compare with a separate serial build.
Use --backend cuda for NVIDIA. GPU checks require a working native runtime.
Use --cpu-only to run just the EKI input checks without a GPU.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import subprocess
import tempfile
import zipfile


def write_geometry(path):
    # Flat terrain in the binary GRAMM geometry format, with stretched layers.
    nx, ny, nz = 24, 20, 16
    spacing = 250
    faces = [round(sum(20 * 1.3**k for k in range(n)), 2) for n in range(nz + 1)]
    depths = [b - a for a, b in zip(faces, faces[1:])]
    centers = [(a + b) / 2 for a, b in zip(faces, faces[1:])]

    with path.open("wb") as stream:
        def floats(values, repeats=1):
            for value in values:
                stream.write(struct.pack("<f", value) * repeats)

        stream.write(b"-99 \r\n" + struct.pack("<iii", nx, ny, nz))
        floats([0], nx * ny)
        floats(centers, nx * ny)
        floats(range(0, (nx + 1) * spacing, spacing))
        floats(range(0, (ny + 1) * spacing, spacing))
        floats(faces)
        floats([spacing**2 * dz for dz in depths], nx * ny)
        floats([spacing * dz for dz in depths], (nx + 1) * ny)
        floats([spacing * dz for dz in depths], nx * (ny + 1))
        floats([0, 0, spacing**2], nx * ny * (nz + 1))
        for count in (nx, ny, nx, ny):
            floats([spacing], count)
        stream.write(struct.pack("<iid", 0, 0, 0))
        floats(faces, (nx + 1) * (ny + 1))


def make_case(root, allow_fallback, backend, *, overlap=None):
    comp = root / "Computation"
    weather = root / "Weather"
    comp.mkdir(parents=True)
    weather.mkdir()
    settings = {
        "GRAL.geb": "20\n20\n5,1.15\n50\n50\n1\n1,2,3\n2000\n3000\n2000\n3000\n",
        "GRAMM.geb": "24\n20\n16\n0\n6000\n0\n5000\n",
        "windfeld.txt": "../Weather\n",
        "GRAL_FlowFields.txt": "0\n",
        "Max_Proc.txt": "2\n",
        "GPU.txt": f"use_gpu = 1\nbackend = {backend}\nfallback_on_startup = {int(allow_fallback)}\nreproducible_sums = 1\n",
        "in.dat": "1\n3600\n0\n4\n0\n0.1\n37\nY\nNO2\n1.5\n2\n1\n0\n0\ncompressed\nnokeystroke\nASCiiResults 0\n0\n0\n0\n0\n1\n",
        "cadastre.dat": "x,y,z,dx,dy,dz,emission,unused,unused,unused,SG\n"
                        + "".join(f"2500,2500,2,100,100,1,1,0,0,0,{group}\n" for group in (1, 2, 3)),
        "meteopgt.all": "10,1,0\ndirection,speed,stability,frequency\n27,2,4,1\n18,2.5,3,1\n9,2,4,1\n",
        "mettimeseries.dat": "01.01 1 2 9 4\n01.01 2 2 27 4\n01.01 3 2.5 18 3\n",
    }
    if overlap is not None:
        settings["GPU.txt"] += f"flow_field_overlap = {int(overlap)}\n"
    for name, value in settings.items():
        (comp / name).write_bytes(value.encode("ascii"))
    write_geometry(comp / "ggeom.asc")
    for n, (u, v, stability) in enumerate(((200, 0, 4), (0, 250, 3), (-200, 0, 4)), start=1):
        wind = struct.pack("<iiiii", -1, 24, 20, 16, 250)
        (weather / f"{n:05}.wnd").write_bytes(wind + struct.pack("<hhh", u, v, 0) * (24 * 20 * 16))
        scl = struct.pack("<iiiif", -1, 24, 20, 16, 250)
        (weather / f"{n:05}.scl").write_bytes(scl + struct.pack("<h", stability) * (24 * 20))
    return comp


def fixture_environment():
    # Use this fixture's GPU settings and published kernels; honor host runtime and device paths.
    host_settings = {"INU_GPU_DEVICE", "INU_GPU_HIP_RUNTIME", "INU_GPU_HIPRTC_DIR",
                     "INU_GPU_NVRTC_DIR", "INU_GPU_CACHE_DIR"}
    return {key: value for key, value in os.environ.items()
            if not key.upper().startswith(("INU_GPU_", "INU_GRAL_GPU_")) or key.upper() in host_settings}


def run_gral(dll, comp, timeout):
    return subprocess.run(["dotnet", str(dll), str(comp)], cwd=comp, stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, env=fixture_environment(), timeout=timeout,
                          encoding="utf-8", errors="replace")


def run_case(dll, root, backend, overlap):
    comp = make_case(root, True, backend, overlap=overlap)
    run = run_gral(dll, comp, 180)
    log = run.stdout
    if run.returncode or "GPU run summary: 3 situation(s) computed on the GPU, 0 on the CPU" not in log:
        raise RuntimeError(log)
    fields = re.findall(r"Reading GRAMM wind field: .*?(\d+)\.wnd", log)
    if list(map(int, fields)) != [3, 1, 2]:
        raise AssertionError(f"Unexpected wind input sequence: {fields}")
    starts = re.findall(r"Flow field calculation for the next situation starts in parallel(?: \(weather (\d+), field (\d+)\))?", log)
    expected = [("2", "1"), ("3", "2")] if overlap else []
    if starts != expected:
        raise AssertionError(f"Unexpected overlap targets: {starts}, expected {expected}")
    metrics = [json.loads(line) for line in (comp / "GRAL_GPU_metrics.jsonl").read_text("utf-8-sig").splitlines()]
    if [row["situation"] for row in metrics] != [1, 2, 3] or any(
        row["rng_seed_mode"] != "fixed" or not row["result_server_metrics"]["reproducible_sums"] or
        row["result_server_metrics"]["device"]["backend"] != backend for row in metrics
    ) or metrics[1]["residual_particles"] <= 0:
        raise AssertionError("Expected three fixed-seed GPU dispatches and residuals in situation 2")
    payloads = {}
    for suffix in ("gff", "grz"):
        files = sorted(comp.glob(f"*.{suffix}"))
        if [p.stem for p in files] != ["00001", "00002", "00003"]:
            raise AssertionError(f"Unexpected {suffix} files: {files}")
        for path in files:
            with zipfile.ZipFile(path) as archive:
                names = archive.namelist()
                if len(names) != (1 if suffix == "gff" else 3) or len(set(names)) != len(names):
                    raise AssertionError(f"Unexpected members in {path.name}: {names}")
                for name in names:
                    data = archive.read(name)
                    if suffix == "grz":
                        if not name.endswith(".con") or data[:4] != struct.pack("<i", -1):
                            raise AssertionError(f"Expected a sparse concentration grid: {name}")
                        values = [v for _, _, v in struct.iter_unpack("<iif", memoryview(data)[4:])]
                        if not any(v > 0 for v in values) or not all(math.isfinite(v) and v >= 0 for v in values):
                            raise AssertionError(f"Concentrations must be finite and include positive cells: {name}")
                    payloads[path.name, name] = hashlib.sha256(data).digest()
    counts = [(row["fresh_particles"], row["fresh_active_particles"], row["residual_particles"]) for row in metrics]
    return payloads, counts


def check_eki(dll, root, backend):
    # No .wnd series in flat terrain: the EKI reader alone decides when to stop.
    for name, second, expected in (("valid", "140 1 2 1 3 2 4", 0), ("malformed", "bad 1 2 1 3 2 4", 1)):
        comp = make_case(root / name, True, backend)
        (comp / "ggeom.asc").unlink()
        (comp / "GRAMM.geb").unlink()
        (comp / "GPU.txt").write_bytes(b"use_gpu = 0\n")
        (comp / "Max_Proc.txt").write_bytes(b"1\n")
        settings = (comp / "in.dat").read_text("ascii").splitlines()
        settings[2:4] = ["1", "2"]  # steady, EKI
        (comp / "in.dat").write_bytes(("\n".join(settings) + "\n").encode("ascii"))
        (comp / "elimakidata.prn").write_bytes(("120 2 1 3 1 4 2\n" + second).encode("ascii"))
        result = run_gral(dll, comp, 60)
        log = result.stdout
        weather = sorted(p.stem for p in comp.glob("*.grz"))
        wanted = ["00001", "00002"] if expected == 0 else ["00001"]
        if result.returncode != expected or weather != wanted:
            raise AssertionError(f"EKI {name}: status {result.returncode}, results {weather}\n{log}")
        if expected == 0 and not re.search(r"Obukhov length \[m\]:\s+140[.,]0", log):
            raise AssertionError(f"Final EKI observation was not applied\n{log}")
        if expected and "elimakidata.prn in line 2" not in log:
            raise AssertionError(f"Missing diagnostic for second EKI row\n{log}")
    print("PASS: CPU reads both EKI observations, flushes the valid row and reports the malformed row")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--reference", type=Path)
    parser.add_argument("--backend", choices=("hip", "cuda"), default="hip")
    parser.add_argument("--cpu-only", action="store_true")
    args = parser.parse_args()
    candidate = args.candidate.resolve(strict=True)
    with tempfile.TemporaryDirectory() as temporary:
        root = Path(temporary)
        check_eki(candidate, root, args.backend)
        if args.cpu_only:
            return
        serial, counts = run_case(candidate, root / "serial", args.backend, False)
        comparisons = [("overlap", candidate, True)]
        if args.reference:
            comparisons.append(("reference", args.reference.resolve(strict=True), None))
        for label, dll, overlap in comparisons:
            fields, particles = run_case(dll, root / label, args.backend, overlap)
            if particles != counts:
                raise AssertionError(f"{label} particle counts {particles} differ from serial counts {counts}")
            different = [k for k in fields.keys() | serial.keys() if fields.get(k) != serial.get(k)]
            if different:
                raise AssertionError(f"{label} differs from the serial run in {sorted(different)}")
    print(f"PASS: three {args.backend.upper()} situations, two flow overlaps, three wind fields and nine concentration grids")


if __name__ == "__main__":
    main()
