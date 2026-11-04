# Optional NVIDIA GPU path for GRAL

GRAL can move particle tracking and concentration accumulation to an NVIDIA GPU. Everything else (situation setup, flow fields, output files) stays in the C# code, and the CPU path remains the default.

## Requirements

- An NVIDIA GPU and driver. The driver must accept PTX from the NVRTC version you use; for NVRTC 12.9 that is a driver with CUDA 12.9 support. If the driver cannot JIT the PTX, the runtime retries with a CUBIN when NVRTC can emit one for the device's compute-capability major.
- NVRTC, the CUDA runtime compiler. Any of these works:
  - `pip install nvidia-cuda-nvrtc-cu12` into the Python used below (supports compute capability 5.0 and newer),
  - a CUDA toolkit installation (`CUDA_PATH`),
  - an explicit folder in `INU_GPU_NVRTC_DIR` that holds the NVRTC library and its matching `nvrtc-builtins` library.
  CUDA 13.x NVRTC no longer compiles for Maxwell, Pascal and Volta (compute capability below 7.5); use NVRTC 12.x for those cards.
- 64-bit Python 3.8 or newer. The runtime uses only the standard library.
- Windows x64, which is the tested platform. The Linux code path (`libcuda.so.1`, `libnvrtc.so`) is untested.

At startup the runtime reads the compute capability of the selected device, asks NVRTC which architectures it supports, and compiles for the highest one that does not exceed the device. Once the server has found a device and a compile target, the `INU_GPU_STDERR` log gets a line starting with `CUDA device`; it shows the device, compute capability, NVRTC version, target, memory, driver version and whether the display watchdog is active. A server error during startup is appended to the same log. Problems GRAL finds before the server starts (settings, paths, Python) appear as `GPU startup failed: ...` on the console. The log is appended across runs, so read its last entries.

## Running GRAL on the GPU

Copy the five files in `gpu/runtime` to a writable folder and set:

| Variable | Value |
|---|---|
| `INU_GPU_EXPERIMENTAL` | `1` to request the GPU; anything else keeps the CPU |
| `INU_GPU_PYTHON` | path to `python.exe` |
| `INU_GPU_SERVER` | path to `server.py` |
| `INU_GPU_MANIFEST` | path to `fields_manifest.json` |
| `INU_GPU_METRICS` | JSON-lines file for per-situation metrics |
| `INU_GPU_STDERR` | log file of the GPU server (appended; a second GRAL process writes a file with a process-specific suffix) |

```powershell
$env:INU_GPU_EXPERIMENTAL = '1'
$env:INU_GPU_PYTHON   = 'C:\Python312\python.exe'
$env:INU_GPU_SERVER   = 'D:\gral_gpu\runtime\server.py'
$env:INU_GPU_MANIFEST = 'D:\gral_gpu\runtime\fields_manifest.json'
$env:INU_GPU_METRICS  = 'D:\gral_gpu\logs\gpu_metrics.jsonl'
$env:INU_GPU_STDERR   = 'D:\gral_gpu\logs\gpu_server.log'
dotnet GRAL.dll D:\project\Computation
```

Optional settings:

| Variable | Default | Effect |
|---|---|---|
| `INU_GPU_DEVICE` | `0` | CUDA device ordinal |
| `INU_GPU_NVRTC_DIR` | unset | use only this NVRTC folder |
| `INU_GPU_CACHE_DIR` | unset | local folder for compiled kernels; skips recompilation on later runs |
| `INU_GPU_FALLBACK_ON_STARTUP` | `0` | `1`: if the GPU cannot start (no driver, no NVRTC, wrong Python), run the whole job on the CPU and log the reason |
| `INU_GPU_MEMORY_RESERVE_BYTES` | 10 % of VRAM, 128-512 MiB | VRAM left for the display and other programs |
| `INU_GPU_MEMORY_BUDGET_BYTES` | all VRAM | upper limit for GRAL's device buffers; larger arrays go to mapped host memory |
| `INU_GPU_PINNED_BUDGET_BYTES` | 256 MiB | limit for that mapped host memory |
| `INU_GPU_MAX_KERNEL_MS` | `500` | on a display GPU, launches are sized to stay well below this time |
| `INU_GRAL_GPU_CHUNK` | `4096` | particles per launch (1 to 65536); does not change results |
| `INU_GRAL_GPU_BLOCK` | `128` | CUDA block size, 128 or 256 |

## What runs on the GPU

Transient and stationary runs with terrain or flat terrain; meteopgt.all meteorology, and in stationary mode also ZR, EKI and Sonic profiles; point, line, area and portal sources including tunnel entrances and opposite lanes; buildings; dry and wet deposition; decay; odour output; vertical concentration output; receptors; and up to nine concentration layers.

The inputs of each situation are checked before they go to the GPU; an invalid value (for example a non-finite particle value) stops the run with an error. Invalid `INU_GRAL_GPU_CHUNK` or `INU_GRAL_GPU_BLOCK` values count as a startup failure. A few inputs that the GPU cannot take run on the CPU instead: transient mode with meteorology other than meteopgt.all, a meteorology profile count outside 1 to 50, and accumulated concentrations that already hold non-finite values (for example from a restart file). GRAL then prints `GPU fallback to CPU: <reason>` and writes the reason to the GRAL log.

With fixed random seeds the GPU uses the same random streams as the CPU; floating-point rounding (CUDA and .NET math functions differ in the last bits) leaves small differences, far below the difference between two CPU seeds. With native seeds every run draws new random streams on either backend, so two runs differ the way two CPU runs do. The GPU derives each fresh particle's seed by hashing a random master, the situation and the particle number; in the native-seed tests no run statistic (maximum, P99, P95, mean) separated these GPU runs from CPU runs.

## When something goes wrong

- `GPU startup failed: ...`: the message names the missing piece (driver, device ordinal, NVRTC, Python). Set `INU_GPU_FALLBACK_ON_STARTUP=1` to run on the CPU instead.
- A device error or watchdog reset during a run stops GRAL with an error; it is not retried on the CPU. Lower `INU_GPU_MAX_KERNEL_MS` on slow display GPUs.
- Several GRAL processes may share one GPU and one cache folder; each writes its own log files.
