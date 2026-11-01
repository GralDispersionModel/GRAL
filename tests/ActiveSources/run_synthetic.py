"""Generate and check a small GRAL hourly-source experiment from virtual inputs."""

import argparse
import json
import math
import re
import struct
import subprocess
import zipfile
from pathlib import Path


CASES = {
    "control24": [(1, 1, 1)] * 24,
    "sparse24": [(3, 0, 0)] * 24,
    "switch48": [(3, 0, 0)] * 24 + [(0, 3, 0)] * 24,
    "edge4": [(3, 0, 0), (0, 0, 0), (0, 0, 0), (0, 3, 0)],
}
GROUPS = (1, 2, 3)
TPS = 0.03
PARTICLES = round(TPS * 3600)


def write_case(folder, factors, hourly_total=3):
    folder.mkdir(parents=True, exist_ok=False)
    files = {
        "GRAL.geb": "20\n20\n5,1.15\n10\n10\n1\n1,2,3\n0\n200\n0\n200\n",
        "in.dat": "\n".join((
            "0.03", "3600", "0", "4", "0", "0.1", "37", "Y", "NO2",
            "1.5", "2", "1", "0", "0", "compressed V03", "nokeystroke",
            "ASCiiResults 0", "0", "0", "0", "0", "1",
        )) + "\n",
        "point.dat": "x,y,z,emission,unused,unused,unused,velocity,diameter,temperature,SG\n"
        "virtual point sources, kg/h\n" + "".join(
            f"100,100,2,1,0,0,0,0,1,293,{group}\n" for group in GROUPS
        ),
        "Max_Proc.txt": "1\n",
        "meteopgt.all": "10 1 0\ndirection speed stability\n27 2 4\n",
        "mettimeseries.dat": "".join(
            f"{1 + hour // 24:02d}.01 {1 + hour % 24} 2 27 4\n"
            for hour in range(len(factors))
        ),
        "emissions_timeseries.txt": "Date;Hour;1;2;3\n" + "".join(
            f"{1 + hour // 24:02d}.01;{1 + hour % 24};{a};{b};{c}\n"
            for hour, (a, b, c) in enumerate(factors)
        ),
    }
    for name, content in files.items():
        (folder / name).write_text(content, encoding="ascii", newline="\n")
    assert all(sum(row) in (0, hourly_total) for row in factors)
    return files


def particle_allocations(stdout, source_count=len(GROUPS)):
    blocks = []
    current = {}
    source_ids = range(1, source_count + 1)
    for line in stdout.splitlines():
        source = re.fullmatch(r"PS\s+(\d+)\s*:\s*(\d+)\s*", line)
        if source:
            source_id, count = map(int, source.groups())
            assert source_id in source_ids and source_id not in current
            current[source_id] = count
        if re.search(r"Using a total of \d+ particles", line):
            blocks.append(tuple(current.get(source_id, 0) for source_id in source_ids))
            current = {}
    assert not current, "partial LogLevel02 PS allocation block"
    return blocks


def check_grz(folder, factors):
    nonzero = 0
    per_group = {group: {"positive_cells": 0, "sum": 0.0} for group in GROUPS}
    ever_active = {group: False for group in GROUPS}
    for hour, row in enumerate(factors, 1):
        path = folder / f"{hour:05d}.grz"
        assert path.is_file(), f"missing {path.name}"
        with zipfile.ZipFile(path) as archive:
            assert len(archive.namelist()) == len(GROUPS)
            for group, factor in zip(GROUPS, row):
                name = f"{hour:05d}-1{group:02d}.con"
                payload = archive.read(name)
                assert len(payload) == 28 + 10 * 10 * 4, (name, len(payload))
                header = struct.unpack_from("<iiiffii", payload)
                assert header[0] == -3 and header[-2:] == (10, 10), (name, header)
                values = struct.unpack_from("<100f", payload, 28)
                assert all(math.isfinite(v) and v >= 0 for v in values), name
                cell_sum = sum(values)
                positive = sum(v > 0 for v in values)
                if factor > 0:
                    assert cell_sum > 0, f"active SG {group} has no concentration at hour {hour}"
                    ever_active[group] = True
                elif not ever_active[group]:
                    assert cell_sum == 0, f"inactive SG {group} has concentration before activation at hour {hour}"
                per_group[group]["positive_cells"] += positive
                per_group[group]["sum"] += cell_sum
                nonzero += positive
    assert nonzero > 0, "all concentration grids are zero"
    return nonzero, per_group


def check_run(folder, stdout, factors, baseline=False):
    hours = len(factors)
    assert "GRAL simulations finished at:" in (folder / "Logfile_GRALCore.txt").read_text(errors="replace")
    problem = folder / "Problemreport_GRAL.txt"
    assert not problem.exists() or not problem.read_text(errors="replace").strip(), "GRAL problem report"
    blocks = particle_allocations(stdout)
    expected_blocks = 1 if baseline else hours + 1
    assert len(blocks) == expected_blocks, f"expected {expected_blocks} allocation blocks, got {len(blocks)}"
    assert blocks[0] == (36, 36, 36), f"unexpected initial static allocation: {blocks[0]}"
    if not baseline:
        for hour, (actual, row) in enumerate(zip(blocks[1:], factors), 1):
            expected = tuple(round(PARTICLES * factor / sum(row)) if sum(row) else 0 for factor in row)
            assert actual == expected, f"hour {hour}: expected {expected}, got {actual}"
            assert sum(actual) == (PARTICLES if sum(row) else 0), f"hour {hour}: particle budget changed"
    totals = [int(n) for n in re.findall(r"Using a total of (\d+) particles", stdout)]
    assert totals == [sum(block) for block in blocks], "allocation summary disagrees with PS counts"
    emission_rows = re.findall(
        r"Summarized emission rates per source group in kg\s*\n[^\n]*\n([^\n]+)", stdout
    )
    assert len(emission_rows) == 1, "missing final emitted-mass summary"
    emitted = tuple(float(value.replace(",", ".")) for value in emission_rows[0].split())
    expected_mass = tuple(sum(row[index] for row in factors) for index in range(len(GROUPS)))
    assert len(emitted) == len(GROUPS)
    assert all(abs(actual - expected) <= max(0.02, expected * 0.01)
               for actual, expected in zip(emitted, expected_mass)), (emitted, expected_mass)
    nonzero, grid_by_group = check_grz(folder, factors)
    return {"hours": hours, "initial_particles": blocks[0], "hourly_particles": blocks[1:],
            "finite_positive_grid_cells": nonzero, "configured_hourly_emission": [sum(row) for row in factors],
            "emitted_mass_kg": emitted, "grid_by_group": grid_by_group}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--case", choices=CASES, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--dll", type=Path, help="GRAL.dll; omit to prepare input only")
    parser.add_argument("--baseline", action="store_true", help="accept the original static allocation in control24")
    args = parser.parse_args()
    if args.baseline and args.case != "control24":
        parser.error("--baseline is only valid for control24")
    factors = CASES[args.case]
    write_case(args.out, factors)
    if not args.dll:
        print(f"[PREPARE] {args.case} {args.out}")
        return
    command = ["dotnet", str(args.dll.resolve()), str(args.out.resolve()), "LOGLEVEL02"]
    result = subprocess.run(command, cwd=args.out, capture_output=True, text=True,
                            errors="replace", timeout=1200)
    (args.out / "stdout.txt").write_text(result.stdout, encoding="utf-8")
    (args.out / "stderr.txt").write_text(result.stderr, encoding="utf-8")
    assert result.returncode == 0, f"GRAL returned {result.returncode}: {result.stderr[-500:]}"
    report = check_run(args.out, result.stdout, factors, baseline=args.baseline)
    (args.out / "check.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"[PASS] {args.case} {len(factors)} h {report['finite_positive_grid_cells']} nonzero cells")


if __name__ == "__main__":
    main()
