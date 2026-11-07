# Optional GPU path for GRAL (NVIDIA or AMD)

GRAL can move particle tracking and concentration accumulation to an NVIDIA GPU or, on Windows, to an AMD GPU. Everything else (situation setup, flow fields, output files) stays in the C# code, and the CPU path remains the default. GRAL calls the CUDA driver and NVRTC, or the HIP runtime and hiprtc, directly from C# (unmanaged function pointers loaded with `NativeLibrary`); no Python is needed. Both backends compile the same kernel source, `generated.cu`. Before hiprtc compiles it, GRAL makes three changes. The first two leave every computed value as it is. The third groups double-precision atomic additions, so these sums can round differently in the last bits than any one-at-a-time order would; the order of atomic additions is unspecified on both backends anyway:

- The three `asm("trap;")` statements (reached where the CPU code would throw) each set a device flag and then call `__builtin_trap()`. On the tested gfx1201 GPU the Windows HIP runtime does not report a trap as a launch error, so GRAL reads the flag after every launch and stops the run, as on CUDA. For RDNA3 (gfx11) the compiler turns the trap into a halt loop, so there a trap may keep the launch from finishing; this has not been tried.
- The `State` struct moves to constant memory, so its fields load through the scalar cache. The kernel cannot write constant memory, so it records its error code in a separate device symbol, which GRAL reads after every launch.
- Lanes of a wavefront that add to the same double-precision value as the first remaining lane sum their values first, and one lane adds the sum; lanes with other addresses add one at a time as before. AMD gfx12 GPUs have no double atomic add instruction, and the compare-and-swap loop that replaces it is slow when many lanes update one address, as for the emitted mass per source group.

## Choosing the backend

With `INU_GPU_BACKEND` unset, GRAL uses CUDA. Only when the NVIDIA driver library is missing or the driver reports no device, and the AMD HIP runtime is installed, does GRAL try HIP. Unless `INU_GPU_DEVICE` is set, it then takes the first AMD GPU that is not integrated, and stops with a startup failure if only integrated GPUs are visible. Any other CUDA problem (for example a missing NVRTC or a driver error) is reported as a startup failure, and GRAL does not switch to HIP. Set `INU_GPU_BACKEND` to `cuda` or `hip` (case does not matter) to choose a backend, for example on a machine with both kinds of GPU.

## Requirements (NVIDIA)

- An NVIDIA GPU and driver. The driver must accept PTX from the NVRTC version you use; for NVRTC 12.9 that is a driver with CUDA 12.9 support. If the driver cannot JIT the PTX, the runtime retries with a CUBIN when NVRTC can emit one for the device's compute-capability major.
- NVRTC, the CUDA runtime compiler (the NVRTC library and its matching `nvrtc-builtins` library). GRAL looks in this order:
  1. `INU_GPU_NVRTC_DIR`, when set (only there);
  2. the `nvrtc` folder next to `fields_manifest.json`, either with the two libraries copied in or filled with `pip install --target <runtime>\nvrtc nvidia-cuda-nvrtc-cu12`;
  3. the CUDA toolkit in `CUDA_PATH`, then the other installed toolkits in `CUDA_PATH_V*` (newest first). Entries that are not absolute paths are skipped.
  CUDA 13.x NVRTC no longer compiles for Maxwell, Pascal and Volta (compute capability below 7.5); use NVRTC 12.x for those cards.
- Windows x64, which is the tested platform. The Linux code path (`libcuda.so.1`, `libnvrtc.so`, `CUDA_HOME`) is untested.

## Requirements (AMD)

- Windows x64. The HIP backend is not implemented for Linux.
- An AMD GPU that the HIP runtime supports, and an AMD Adrenalin driver that installs the HIP 7 runtime (`amdhip64_7.dll` in System32).
- The Microsoft Visual C++ 2015-2022 x64 runtime, which `amd_comgr.dll` needs.
- hiprtc, the HIP runtime compiler: `hiprtcNNNN.dll` with its `hiprtc-builtinsNNNN.dll` and the code-object manager (`amd_comgr.dll` or `amd_comgr_N.dll`) in the same folder. GRAL looks in this order:
  1. `INU_GPU_HIPRTC_DIR`, when set (only there);
  2. the `hiprtc` folder next to `fields_manifest.json`;
  3. `%HIP_PATH%\bin`, which the AMD HIP SDK installer sets.

  The ROCm Python packages also contain hiprtc, for example `pip install --index-url https://stable.repo.amd.com/rocm/whl-next/ "rocm[libraries,devel,device-gfx1201]"` for RDNA4; the libraries are in `Lib\site-packages\_rocm_sdk_core\bin` of that environment.
- Tested with a Radeon RX 9070 XT (gfx1201), the HIP runtime 7.12 of driver 32.0.32015.12012 and `hiprtc0716.dll` from ROCm 10.1 (its `hiprtcVersion` reports 9.0). Other AMD GPUs have not been tried; on any architecture other than gfx1201, GRAL prints `GPU warning: ...` at startup, so compare a test case with a CPU run before relying on the results.

hiprtc compiles the kernel with `-ffp-contract=off` and IEEE fp32 division and square root, which match the NVRTC options, and with `-std=c++17`, because the hiprtc runtime header needs it (the kernel itself is C++11, as on NVRTC). It also passes `-mllvm -amdgpu-enable-pre-ra-optimizations=0`: with that AMDGPU back-end step enabled, hiprtc from ROCm 10.1 compiled the fresh-particle kernel for gfx1201 into code that gave wrong concentrations. Two more options affect only code generation. `--gpu-max-threads-per-block=256` matches the 128 or 256 threads per block that GRAL launches; without a bound the compiler must allow 1,024 threads and limits each kernel to 192 vector registers, which made the fresh-particle kernel spill heavily. `-mllvm -inline-threshold=50000` inlines the wind interpolation into the residual-particle kernel and on the wet-deposition path; in the fresh-particle kernel the call made in every time step stays a call, because the AMDGPU back end caps the size of the function that inlining would produce. On the RX 9070 XT, these two options together with the `State` and atomic-add changes above cut the fresh-particle kernel time of test case B00 (24 situations) from 26.1 s to 13.3 s in a same-session comparison. The concentration (`.con`) files of all 16 validation cases stayed byte-identical to those of the HIP build without these changes; none of these cases writes receptor or deposition output.

At startup the runtime reads the compute capability of the selected device, asks NVRTC which architectures it supports, and compiles for the highest one that does not exceed the device. Once the device and the target are chosen, and before the kernel is compiled or taken from the cache, the `INU_GPU_STDERR` log gets a line starting with `CUDA device`; it shows the device, compute capability, NVRTC version, target, memory, CUDA driver version and whether the display watchdog is active. On AMD the line starts with `HIP device` and shows the gfx architecture, the hiprtc version and path, the device, memory, the HIP runtime version, `kernel_exec_timeout` and the compute mode. Every startup error appears on the console as `GPU startup failed: ...`; errors from the driver, NVRTC or the kernel module also go to that log. The log is appended across runs, so read its last entries.

## Running GRAL on the GPU

Copy `fields_manifest.json` and `generated.cu` from `gpu/runtime` to a folder (add the `nvrtc` or `hiprtc` folder there if you use one) and set:

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
| `INU_GPU_BACKEND` | unset | `cuda` or `hip`; unset chooses as described in "Choosing the backend" |
| `INU_GPU_DEVICE` | `0` on CUDA; on HIP the first AMD GPU that is not integrated (device 0 if all are integrated and `INU_GPU_BACKEND=hip`) | device ordinal within the chosen backend; a set value is used as given, also for an integrated GPU |
| `INU_GPU_NVRTC_DIR` | unset | use only this NVRTC folder |
| `INU_GPU_HIPRTC_DIR` | unset | use only this hiprtc folder |
| `INU_GPU_HIP_RUNTIME` | unset | absolute path of another HIP 7 runtime (`amdhip64_7.dll`) to use instead of the driver's System32 copy |
| `INU_GPU_HIPRTC_EXTRA` | unset | extra hiprtc options, separated by spaces, for diagnosis; they become part of the cache key |
| `INU_GPU_CACHE_DIR` | `%LOCALAPPDATA%\GRAL\GpuModuleCache` | local folder for compiled kernels, so later runs skip the NVRTC or hiprtc compile; `off` disables it |
| `INU_GPU_FALLBACK_ON_STARTUP` | `0` | `1`: if the GPU cannot start (no driver, no NVRTC or hiprtc, missing manifest, or an invalid `INU_GPU_BACKEND`, `INU_GPU_HIPRTC_DIR`, `INU_GPU_HIP_RUNTIME`, `INU_GPU_DEVICE`, `INU_GPU_MEMORY_RESERVE_BYTES`, `INU_GPU_MEMORY_BUDGET_BYTES`, `INU_GPU_MAX_KERNEL_MS`, `INU_GRAL_GPU_CHUNK` or `INU_GRAL_GPU_BLOCK` value), run the whole job on the CPU and log the reason. An invalid `INU_GPU_PINNED_BUDGET_BYTES` is found only at the first launch and stops the run |
| `INU_GPU_MEMORY_RESERVE_BYTES` | 10 % of VRAM, 128-512 MiB, at most a quarter of VRAM | VRAM left for the display and other programs |
| `INU_GPU_MEMORY_BUDGET_BYTES` | all VRAM | upper limit for GRAL's device buffers; larger arrays go to mapped host memory |
| `INU_GPU_PINNED_BUDGET_BYTES` | 256 MiB | limit for the particle windows and scratch rows placed in mapped host memory when they do not fit within the device budget; other arrays placed there (above `INU_GPU_MEMORY_BUDGET_BYTES` or after a failed device allocation) do not count against it |
| `INU_GPU_MAX_KERNEL_MS` | `500` | on a GPU with the display watchdog, each launch is sized from the time of the previous one, aiming at half this limit; single launches can still exceed it (`launches_over_max_kernel_ms` in the metrics); `0` turns the sizing off. On the tested AMD GPU the HIP runtime reported no watchdog (`kernel_exec_timeout=0`), so this setting had no effect there; the RX 9070 XT also drove the display, and single launches took up to 5.5 s without a driver reset |
| `INU_GRAL_GPU_CHUNK` | `65536` | largest number of particles per launch (1 to 65536); launches can be smaller on a GPU with the display watchdog; does not change results |
| `INU_GRAL_GPU_BLOCK` | `128` | threads per block, 128 or 256 |

## What runs on the GPU

Transient and stationary runs with terrain or flat terrain; meteopgt.all meteorology, and in stationary mode also ZR, EKI and Sonic profiles; point, line, area and portal sources including tunnel entrances and opposite lanes; buildings; dry and wet deposition; decay; odour output; vertical concentration output; receptors; and up to nine concentration layers.

The inputs of each situation are checked before they go to the GPU; an invalid value (for example a non-finite particle value) stops the run with an error. Invalid `INU_GRAL_GPU_CHUNK` or `INU_GRAL_GPU_BLOCK` values count as a startup failure. A few inputs that the GPU cannot take run on the CPU instead: transient mode with meteorology other than meteopgt.all, a meteorology profile count outside 1 to 50 when the GPU needs the profile (flat terrain without buildings, or ZR or Sonic meteorology), and accumulated concentration or deposition fields that already hold non-finite values (for example from a restart file). GRAL then prints `GPU fallback to CPU: <reason>` and writes the reason to the GRAL log.

With fixed random seeds the GPU uses the same random streams as the CPU; floating-point rounding leaves small differences, most likely from the math functions (CUDA and .NET implementations can differ in the last bits; the kernel is compiled without FMA contraction), far below the difference between two CPU seeds. With native seeds every run draws new random streams on either backend, so two GPU runs differ from each other about as much as two CPU runs do. The GPU derives each fresh particle's seed by hashing a random master, the situation and the particle number; in the native-seed tests no run statistic (maximum, P99, P95, mean) separated these GPU runs from CPU runs.

## When something goes wrong

- `GPU startup failed: ...`: the message names the missing piece (driver, device ordinal, NVRTC, manifest). Set `INU_GPU_FALLBACK_ON_STARTUP=1` to run on the CPU instead.
- A device error or watchdog reset during a run stops GRAL with an error; it is not retried on the CPU. Lower `INU_GPU_MAX_KERNEL_MS` on slow NVIDIA display GPUs; on an AMD GPU that resets, lower `INU_GRAL_GPU_CHUNK` instead.
- On a machine with only AMD GPUs, a startup failure that names only the NVIDIA driver means that GRAL found no HIP 7 runtime (`amdhip64_7.dll` in System32), or that a leftover NVIDIA driver loads but fails. Update the Adrenalin driver or set `INU_GPU_HIP_RUNTIME`; with a leftover NVIDIA driver, set `INU_GPU_BACKEND=hip`.
- Stopping GRAL (Ctrl+C or the GUI) takes effect at the next GPU startup step, kernel launch or transfer, and GRAL finishes as on the CPU path.
- The GPU driver and the runtime compiler (NVRTC or hiprtc) run inside the GRAL process. An NVRTC or hiprtc that cannot be loaded is reported as a startup failure, but a crash inside the driver, NVRTC, hiprtc or `amd_comgr` (for example a compiler library that faults while compiling) ends GRAL, and `INU_GPU_FALLBACK_ON_STARTUP` cannot turn it into a CPU run. Point `INU_GPU_NVRTC_DIR` (or `INU_GPU_HIPRTC_DIR` on AMD) at a known-good compiler if that happens.
- Several GRAL processes may run on one GPU. If they get the same log paths, the second one adds its process id and a time stamp to its file names. They may also share one cache folder (entries are written to a temporary file, moved into place and checked when read); concurrent use of one cache folder has not been tested.
