# Optional NVIDIA GPU path for GRAL

GRAL can move particle tracking and concentration accumulation to an NVIDIA GPU. Everything else (situation setup, flow fields, output files) stays in the C# code, and the CPU path remains the default. GRAL calls the CUDA driver and NVRTC directly from C# (unmanaged function pointers loaded with `NativeLibrary`); no Python is needed.

## Requirements

- An NVIDIA GPU and driver. The driver must accept PTX from the NVRTC version you use; for NVRTC 12.9 that is a driver with CUDA 12.9 support. If the driver cannot JIT the PTX, the runtime retries with a CUBIN when NVRTC can emit one for the device's compute-capability major.
- NVRTC, the CUDA runtime compiler (the NVRTC library and its matching `nvrtc-builtins` library). GRAL looks in this order:
  1. `INU_GPU_NVRTC_DIR`, when set (only there);
  2. the `nvrtc` folder next to `fields_manifest.json`, either with the two libraries copied in or filled with `pip install --target <runtime>\nvrtc nvidia-cuda-nvrtc-cu12`;
  3. the CUDA toolkit in `CUDA_PATH`, then the other installed toolkits in `CUDA_PATH_V*` (newest first). Entries that are not absolute paths are skipped.
  CUDA 13.x NVRTC no longer compiles for Maxwell, Pascal and Volta (compute capability below 7.5); use NVRTC 12.x for those cards.
- Windows x64, which is the tested platform. The Linux code path (`libcuda.so.1`, `libnvrtc.so`, `CUDA_HOME`) is untested.

At startup the runtime reads the compute capability of the selected device, asks NVRTC which architectures it supports, and compiles for the highest one that does not exceed the device. Once the device and the target are chosen, and before the kernel is compiled or taken from the cache, the `INU_GPU_STDERR` log gets a line starting with `CUDA device`; it shows the device, compute capability, NVRTC version, target, memory, CUDA driver version and whether the display watchdog is active. Every startup error appears on the console as `GPU startup failed: ...`; errors from the driver, NVRTC or the kernel module also go to that log. The log is appended across runs, so read its last entries.

## Running GRAL on the GPU

Copy `fields_manifest.json` and `generated.cu` from `gpu/runtime` to a folder (add the `nvrtc` folder there if you use it) and set:

| Variable | Value |
|---|---|
| `INU_GPU_EXPERIMENTAL` | `1` to request the GPU; anything else keeps the CPU |
| `INU_GPU_MANIFEST` | path to `fields_manifest.json`; `generated.cu` is read from the same folder |
| `INU_GPU_METRICS` | JSON-lines file for per-situation metrics |
| `INU_GPU_STDERR` | log file of the GPU runtime (appended; a second GRAL process writes a file with a process-specific suffix) |

```powershell
$env:INU_GPU_EXPERIMENTAL = '1'
$env:INU_GPU_MANIFEST = 'D:\gral_gpu\runtime\fields_manifest.json'
$env:INU_GPU_METRICS  = 'D:\gral_gpu\logs\gpu_metrics.jsonl'
$env:INU_GPU_STDERR   = 'D:\gral_gpu\logs\gpu_runtime.log'
dotnet GRAL.dll D:\project\Computation
```

Optional settings:

| Variable | Default | Effect |
|---|---|---|
| `INU_GPU_DEVICE` | `0` | CUDA device ordinal |
| `INU_GPU_NVRTC_DIR` | unset | use only this NVRTC folder |
| `INU_GPU_CACHE_DIR` | `%LOCALAPPDATA%\GRAL\GpuModuleCache` | local folder for compiled kernels, so later runs skip the NVRTC compile; `off` disables it |
| `INU_GPU_FALLBACK_ON_STARTUP` | `0` | `1`: if the GPU cannot start (no driver, no NVRTC, missing manifest, or an invalid `INU_GPU_DEVICE`, `INU_GPU_MEMORY_RESERVE_BYTES`, `INU_GPU_MEMORY_BUDGET_BYTES`, `INU_GPU_MAX_KERNEL_MS`, `INU_GRAL_GPU_CHUNK` or `INU_GRAL_GPU_BLOCK` value), run the whole job on the CPU and log the reason. An invalid `INU_GPU_PINNED_BUDGET_BYTES` is found only at the first launch and stops the run |
| `INU_GPU_MEMORY_RESERVE_BYTES` | 10 % of VRAM, 128-512 MiB, at most a quarter of VRAM | VRAM left for the display and other programs |
| `INU_GPU_MEMORY_BUDGET_BYTES` | all VRAM | upper limit for GRAL's device buffers; larger arrays go to mapped host memory |
| `INU_GPU_PINNED_BUDGET_BYTES` | 256 MiB | limit for the particle windows and scratch rows placed in mapped host memory when they do not fit within the device budget; other arrays placed there (above `INU_GPU_MEMORY_BUDGET_BYTES` or after a failed device allocation) do not count against it |
| `INU_GPU_MAX_KERNEL_MS` | `500` | on a GPU with the display watchdog, each launch is sized from the time of the previous one, aiming at half this limit; single launches can still exceed it (`launches_over_max_kernel_ms` in the metrics); `0` turns the sizing off |
| `INU_GRAL_GPU_CHUNK` | `65536` | largest number of particles per launch (1 to 65536); launches can be smaller on a GPU with the display watchdog; does not change results |
| `INU_GRAL_GPU_BLOCK` | `128` | CUDA block size, 128 or 256 |

## What runs on the GPU

Transient and stationary runs with terrain or flat terrain; meteopgt.all meteorology, and in stationary mode also ZR, EKI and Sonic profiles; point, line, area and portal sources including tunnel entrances and opposite lanes; buildings; dry and wet deposition; decay; odour output; vertical concentration output; receptors; and up to nine concentration layers.

The inputs of each situation are checked before they go to the GPU; an invalid value (for example a non-finite particle value) stops the run with an error. Invalid `INU_GRAL_GPU_CHUNK` or `INU_GRAL_GPU_BLOCK` values count as a startup failure. A few inputs that the GPU cannot take run on the CPU instead: transient mode with meteorology other than meteopgt.all, a meteorology profile count outside 1 to 50 when the GPU needs the profile (flat terrain without buildings, or ZR or Sonic meteorology), and accumulated concentration or deposition fields that already hold non-finite values (for example from a restart file). GRAL then prints `GPU fallback to CPU: <reason>` and writes the reason to the GRAL log.

With fixed random seeds the GPU uses the same random streams as the CPU; floating-point rounding leaves small differences, most likely from the math functions (CUDA and .NET implementations can differ in the last bits; the kernel is compiled without FMA contraction), far below the difference between two CPU seeds. With native seeds every run draws new random streams on either backend, so two GPU runs differ from each other about as much as two CPU runs do. The GPU derives each fresh particle's seed by hashing a random master, the situation and the particle number; in the native-seed tests no run statistic (maximum, P99, P95, mean) separated these GPU runs from CPU runs.

## When something goes wrong

- `GPU startup failed: ...`: the message names the missing piece (driver, device ordinal, NVRTC, manifest). Set `INU_GPU_FALLBACK_ON_STARTUP=1` to run on the CPU instead.
- A device error or watchdog reset during a run stops GRAL with an error; it is not retried on the CPU. Lower `INU_GPU_MAX_KERNEL_MS` on slow display GPUs.
- Stopping GRAL (Ctrl+C or the GUI) takes effect at the next GPU startup step, kernel launch or transfer, and GRAL finishes as on the CPU path.
- The CUDA driver and NVRTC run inside the GRAL process. An NVRTC that cannot be loaded is reported as a startup failure, but a crash inside the driver or NVRTC (for example an NVRTC library that faults while compiling) ends GRAL, and `INU_GPU_FALLBACK_ON_STARTUP` cannot turn it into a CPU run. Point `INU_GPU_NVRTC_DIR` at a known-good NVRTC if that happens.
- Several GRAL processes may run on one GPU. If they get the same log paths, the second one adds its process id and a time stamp to its file names. They may also share one cache folder (entries are written to a temporary file, moved into place and checked when read); concurrent use of one cache folder has not been tested.
