# Optional GPU path for GRAL (NVIDIA or AMD)

GRAL can move particle tracking and concentration accumulation to an NVIDIA GPU or an AMD GPU, on Windows and on Linux. Everything else (situation setup, flow fields, output files) stays in the C# code, and the CPU path remains the default. GRAL calls the CUDA driver and NVRTC, or the HIP runtime and hiprtc, directly from C# (unmanaged function pointers loaded with `NativeLibrary`); no Python is needed. Both backends compile the same kernel source, `generated.cu`. Before hiprtc compiles it, GRAL makes three changes. The first two leave every computed value as it is. The third groups double-precision atomic additions, so these sums can round differently in the last bits than any one-at-a-time order would; the order of atomic additions is unspecified on both backends anyway:

- The three `asm("trap;")` statements (reached where the CPU code would throw) each set a device flag and then call `__builtin_trap()`. On the tested gfx1201 GPU the Windows HIP runtime does not report a trap as a launch error, so GRAL reads the flag after every launch and stops the run, as on CUDA. For RDNA3 (gfx11) the compiler turns the trap into a halt loop, so there a trap may keep the launch from finishing; this has not been tried.
- The `State` struct moves to constant memory, so its fields load through the scalar cache. The kernel cannot write constant memory, so it records its error code in a separate device symbol, which GRAL reads after every launch.
- Lanes of a wavefront that add to the same double-precision value as the first remaining lane sum their values first, and one lane adds the sum; lanes with other addresses add one at a time as before. AMD gfx12 GPUs have no double atomic add instruction, and the compare-and-swap loop that replaces it is slow when many lanes update one address, as for the emitted mass per source group.

On HIP, GRAL also compiles a kernel for the run's own values just before its first launch. Values that stay fixed during the run become constants in that kernel, so the compiler drops the code of features the run does not use and needs fewer registers. The steps are `flags` (the boolean `State` fields such as `ReceptorsAvailable`, `BuildingsExist` and `Odour`, and `TraceEnabled`), `ints` (`Topo`, `ISTATIONAER`, `IStatistics`, `LogLevel` and the `AdaptiveRoughnessMax > 0` test), `grid` (grid sizes, counts and domain origins such as `NII`, `NJJ`, `NS` and `IKOOAGRAL`), `depo` (only when `DepositionExist` is off: the dry-deposition mode reads, which are then always 0) and `src` (the source type of fresh particles when all fresh particles of a situation share one source type; a situation that mixes source types keeps the general source-type code). Each set of values is compiled once, at its first use, and kept in the module cache, so later runs load it from there; with `INU_GPU_CACHE_DIR=off` it is compiled again in every run. When a value changes during the run, for example the source-type mix of a later situation, GRAL loads the kernel for the new values, and if a specialized kernel cannot be built or loaded, GRAL runs those values on the general kernel. Once a run has loaded six specialized kernels, a situation with a new single source type uses the kernel without the `src` step.

On an RX 9070 XT, the specialized kernel cut the fresh-particle kernel time of B00 from 11.4 s to 7.0 s in one session; the time of the general kernel varies between sessions, and the 13.3 s given further below came from an earlier one. A separate 24-situation transient project with one source type, not one of the 16 validation cases, summed 1235 s and 1212 s of situation time instead of 1791 s and 1795 s, 1.45 and 1.48 times faster. Both builds of that project also carried local changes to how residual particles are re-released, so neither its times nor its residual particle counts are those of this code. Each build ran twice in one session, in opposite order, because the general-kernel build had run about 15 % faster on another day.

A specialized kernel can round some results differently from the general kernel in the last bits. The `.con` files of the 16 validation cases were byte-identical to those of the general kernel. Separate feature test projects with decay, deposition, receptors or vegetation differed by at most 1.8e-7 relative in any cell, close to the 1.2e-7 per cell between two runs of the general kernel. In the long transient project the differences grew: the number of re-released residual particles (up to 26 million per situation) first changed by one particle in situation 10, and by situation 24 the member totals differed by at most 1.8e-4 of their value. Two runs of the same kernel in that project also drifted apart: their residual counts first differed in situation 13 (general kernel) and 17 (specialized), and by situation 24 their member totals differed by up to 1.0e-4 and 1.4e-4. `INU_GPU_SPECIALIZE=0` keeps the general kernel.

## Choosing the backend

With no backend set, GRAL uses CUDA. Only when the NVIDIA driver library is missing or the driver reports no device, and the AMD HIP runtime is installed, does GRAL try HIP. Unless a device is set, it then takes the first AMD GPU that is not integrated, and stops with a startup failure if only integrated GPUs are visible; `allow_integrated = 1` in `GPU.txt` lets it take the integrated GPU in that case. `backend = hip` in `GPU.txt` follows the same rule. The variable `INU_GPU_BACKEND=hip` takes device 0 in that case, also when it is integrated. Any other CUDA problem (for example a missing NVRTC or a driver error) is reported as a startup failure, and GRAL does not switch to HIP. Set the backend to `cuda` or `hip` (case does not matter) to choose one, for example on a machine with both kinds of GPU, and the device to an ordinal within that backend. Both can be set in the project's `GPU.txt` (`backend`, `device`) or with `INU_GPU_BACKEND` and `INU_GPU_DEVICE`; see "GPU.txt, the GPU settings of a project" below.

## Requirements (NVIDIA)

- An NVIDIA GPU and driver. The driver must accept PTX from the NVRTC version you use; for NVRTC 12.9 that is a driver with CUDA 12.9 support. If the driver cannot JIT the PTX, the runtime retries with a CUBIN when NVRTC can emit one for the device's compute-capability major.
- NVRTC, the CUDA runtime compiler (the NVRTC library and its matching `nvrtc-builtins` library). GRAL looks in this order:
  1. `INU_GPU_NVRTC_DIR`, when set (only there);
  2. the `nvrtc` folder next to `fields_manifest.json`, either with the two libraries copied in or filled with `pip install --target <runtime>\nvrtc nvidia-cuda-nvrtc-cu12`;
  3. the CUDA toolkit in `CUDA_PATH`, then the other installed toolkits in `CUDA_PATH_V*` (newest first). Entries that are not absolute paths are skipped;
  4. the per-user folder `%LOCALAPPDATA%\GRAL\GpuToolchain\nvrtc` (Windows), which a front end can fill without administrator rights.

  On Linux, steps 3 and 4 are instead: the toolkit in `CUDA_PATH` or `CUDA_HOME` (its `lib64`, `lib` and `targets/x86_64-linux/lib` folders); then `/usr/local/cuda/lib64`, `/usr/local/cuda-<version>/lib64` (newest first), `/usr/lib/x86_64-linux-gnu` and `/usr/lib64`; last, the library that the loader finds under the name `libnvrtc.so.13` or `libnvrtc.so.12` (`LD_LIBRARY_PATH`, `ldconfig`). The `libnvrtc-builtins` library must be in the same folder as the NVRTC library.

  CUDA 13.x NVRTC no longer compiles for Maxwell, Pascal and Volta (compute capability below 7.5); use NVRTC 12.x for those cards.
- Windows x64 or Linux x64. Tested on Windows 11 and in WSL2 (Ubuntu 24.04, the Linux user space on the Windows driver) with an RTX 5080 and NVRTC 12.9; bare-metal Linux with the NVIDIA kernel driver was not part of these tests.

## Requirements (AMD)

- Windows x64 or Linux x64.
- An AMD GPU that the HIP runtime supports, and the HIP 7 runtime: on Windows the AMD Adrenalin driver installs it (`amdhip64_7.dll` in System32); on Linux it is part of ROCm 7 or later (`libamdhip64.so.7`). On Linux GRAL loads it from `$ROCM_PATH/lib`, `$HIP_PATH/lib`, `/opt/rocm/lib`, `/opt/rocm/core-<version>/lib` (the layout of ROCm 10) or `/opt/rocm-<version>/lib` (versions newest first), and last through the loader (`LD_LIBRARY_PATH`, `ldconfig`).
- On Windows, the Microsoft Visual C++ 2015-2022 x64 runtime, which `amd_comgr.dll` needs.
- hiprtc, the HIP runtime compiler: `hiprtcNNNN.dll` with its `hiprtc-builtinsNNNN.dll` and the code-object manager (`amd_comgr.dll` or `amd_comgr_N.dll`) in the same folder. GRAL looks in this order:
  1. `INU_GPU_HIPRTC_DIR`, when set (only there);
  2. the `hiprtc` folder next to `fields_manifest.json`;
  3. `%HIP_PATH%\bin`, which the AMD HIP SDK installer sets;
  4. the per-user folder `%LOCALAPPDATA%\GRAL\GpuToolchain\hiprtc`.

  On Linux, hiprtc is part of ROCm: `libhiprtc.so.N` with `libhiprtc-builtins.so.N` and `libamd_comgr.so.N` in the same folder. Steps 3 and 4 are then: the folder of the HIP runtime that was loaded, the ROCm folders listed above for the runtime, and last the library that the loader finds under the name `libhiprtc.so.7`.

  The ROCm Python packages also contain hiprtc, for example `pip install --index-url https://stable.repo.amd.com/rocm/whl-next/ "rocm[libraries,devel,device-gfx1201]"` for RDNA4; the libraries are in `Lib\site-packages\_rocm_sdk_core\bin` of that environment.
- Tested with a Radeon RX 9070 XT (gfx1201), the HIP runtime 7.12 of driver 32.0.32015.12012 and `hiprtc0716.dll` from ROCm 10.1 (its `hiprtcVersion` reports 9.0). On Linux: the same card in WSL2 (Ubuntu 24.04, ROCm 10.1 with its WSL library `librocdxg`, HIP runtime 7.16 and hiprtc 9.0 from `/opt/rocm/core-10.1/lib`); bare-metal Linux with the `amdgpu` kernel driver has not been tried. Other AMD GPUs have not been tried; on any architecture other than gfx1201, GRAL prints `GPU warning: ...` at startup, so compare a test case with a CPU run before relying on the results.

hiprtc compiles the kernel with `-ffp-contract=off` and IEEE fp32 division and square root, which match the NVRTC options, and with `-std=c++17`, because the hiprtc runtime header needs it (the kernel itself is C++11, as on NVRTC). It also passes `-mllvm -amdgpu-enable-pre-ra-optimizations=0`: with that AMDGPU back-end step enabled, hiprtc from ROCm 10.1 compiled the fresh-particle kernel for gfx1201 into code that gave wrong concentrations. Two more options affect only code generation. `--gpu-max-threads-per-block=256` matches the 128 or 256 threads per block that GRAL launches; without a bound the compiler must allow 1,024 threads and limits each kernel to 192 vector registers, which made the fresh-particle kernel spill heavily. `-mllvm -inline-threshold=50000` inlines the wind interpolation into the residual-particle kernel and on the wet-deposition path; in the fresh-particle kernel the call made in every time step stays a call, because the AMDGPU back end caps the size of the function that inlining would produce. On the RX 9070 XT, these two options together with the `State` and atomic-add changes above cut the fresh-particle kernel time of test case B00 (24 situations) from 26.1 s to 13.3 s in a same-session comparison. The concentration (`.con`) files of all 16 validation cases stayed byte-identical to those of the HIP build without these changes; none of these cases writes receptor or deposition output.

At startup the runtime reads the compute capability of the selected device, asks NVRTC which architectures it supports, and compiles for the highest one that does not exceed the device. Once the device and the target are chosen, and before the kernel is compiled or taken from the cache, the `INU_GPU_STDERR` log gets a line starting with `CUDA device`; it shows the device, compute capability, NVRTC version, target, memory, CUDA driver version and whether the display watchdog is active. On AMD the line starts with `HIP device` and shows the gfx architecture, the hiprtc version and path, the device, memory, the HIP runtime version, `kernel_exec_timeout` and the compute mode. Every startup error appears on the console as `GPU startup failed: ...`; errors from the driver, NVRTC or the kernel module also go to that log. The log is appended across runs, so read its last entries.

## Running GRAL on the GPU

The build copies `fields_manifest.json` and `generated.cu` to `gpu\runtime` next to `GRAL.dll`, so one setting is enough. Put it in a text file `GPU.txt` in the project's Computation folder, so the project records whether it runs on the GPU:

```
use_gpu = 1   ! Use the GPU for the particle dispersion Yes = 1, No = 0
```

A missing file, a missing `use_gpu` line or `use_gpu = 0` means the CPU. `INU_GPU_EXPERIMENTAL=1` or `0` overrides the file for one run, for example in test scripts:

```powershell
$env:INU_GPU_EXPERIMENTAL = '1'
dotnet GRAL.dll D:\project\Computation
```

The GPU follows GRAL's reproducible-results option (`in.dat` line 22): with it, the GPU adds its results in integers, so repeated runs give the same files. `GPU.txt` can switch these reproducible sums off for the project:

```
reproducible_sums = 0
```

`reproducible_sums` only matters when line 22 is 1 and the GPU is used. No entry or `1` keeps the integer sums; `0` uses the floating-point sums, which can be faster or slower (see the measurements below) and can differ in the last bits between two runs. `INU_GPU_REPRODUCIBLE_SUMS=0` or `1` overrides the entry for one run. A value other than 0 or 1 on line 22 gives a warning on the console and in the GRAL log, and the line then counts as 0.

`in.dat` has no GPU lines: it ends at line 22, and GRAL ignores further lines without a message. A GUI without the GPU option (V27.01 and older) does not know `GPU.txt` and leaves it alone, so a project keeps its GPU settings when such a GUI rewrites `in.dat`.

What GRAL writes about the GPU to `Logfile_GRALCore.txt`:

- always one line with the decision, also for a CPU run, written when `in.dat` and `GPU.txt` have been read: `GPU for the particle dispersion: off (no GPU.txt)`, `off (GPU.txt)`, `on (GPU.txt)` or `on`/`off (INU_GPU_EXPERIMENTAL=...)`. When the variable overrides a file with `use_gpu = 1`, the bracket ends with `, GPU.txt asks for the GPU`. The console gets this line only when `GPU.txt` or the variable asks for the GPU, so a CPU-only run prints nothing new. A value of `INU_GPU_EXPERIMENTAL` other than 1 or 0 switches nothing; the line then names it as ignored, on the console too;
- after a successful GPU start: the backend, the device ordinal and name, its memory, the driver and compiler versions and the binary kind; whether reproducible sums are on and what decided that (`in.dat line 22`, `GPU.txt reproducible_sums = 0` or the variable); the settings file and its status (`GPU settings file: GPU.txt (read)` for the project's file, the full path when `INU_GPU_SETTINGS_FILE` names another file, `none (absent)` or `none (off)`); every setting of the file's key list that is set, with its source (`env` or `file`); and every other `INU_GPU_*` or `INU_GRAL_GPU_*` variable of the process with its value, so that a variable inherited from the calling program is visible;
- warnings (`GPU warning: ...`), per-situation CPU fallbacks and a whole-run fallback, with the reason;
- at the end: `GPU run summary: N situation(s) computed on the GPU, M on the CPU`, with a note when the CPU situations are per-situation fallbacks, when the whole run fell back, or when a GPU error stopped the run (the situation that failed is in neither count).

With several GRAL instances (`SITUATIONS` argument), each instance writes these lines to its own `Logfile_GRALCore(...)` file.

| Variable | Default | Effect |
|---|---|---|
| `INU_GPU_EXPERIMENTAL` | unset | `1` or `0` turns the GPU on or off for this run, overriding `use_gpu` in `GPU.txt`; unset or any other value leaves the choice to `GPU.txt` (another value is reported as ignored) |
| `INU_GPU_MANIFEST` | `fields_manifest.json` in GRAL's `gpu\runtime` folder, which the build writes, else next to GRAL (the first folder that also holds `generated.cu`) | path to `fields_manifest.json`; `generated.cu` is read from the same folder |
| `INU_GPU_METRICS` | `GRAL_GPU_metrics.jsonl` in the working directory, which is the Computation folder when GRAL is started with it; when the GPU logs cannot be written there, `GRAL/GpuLogs` in the per-user local application data folder (`%LOCALAPPDATA%` on Windows, `$XDG_DATA_HOME` or `~/.local/share` on Linux), and the console and `Logfile_GRALCore.txt` say so | JSON-lines file for per-situation metrics |
| `INU_GPU_STDERR` | `GRAL_GPU_runtime.log` in the working directory, or in the same per-user folder as the metrics file | log file of the GPU runtime (appended; a second GRAL process writes a file with a process-specific suffix) |

Missing folders in the two log paths are created.

### GPU.txt, the GPU settings of a project

All GPU settings of a project are in one text file, `GPU.txt` in the project's Computation folder, which is GRAL's working directory in a run. Each project can therefore have its own device and budgets, for example on a PC with several GPUs or on a compute server, and neither a front end nor the user has to set environment variables. A missing file means: GPU off, every default. The name is taken as written, like those of GRAL's other input files: on Linux a file named `gpu.txt` is not read, and GRAL then says so on the console, in its log and in the `warnings` of `--gpu-check`. `INU_GPU_SETTINGS_FILE` names another file in its place; the value `off` means that no file is read, and a path that does not exist is the same as no file. Blanks around the value are ignored. Give an absolute path: a relative one is taken from the working directory of each GRAL process. GRAL only reads the file. Every run reads it, because it holds the GPU switch, and `GRAL --gpu-check` reads it too. Nothing is read from or written to a per-user settings file; only the compiled-kernel cache (`GpuModuleCache`) and a runtime compiler (`GpuToolchain`) are kept in `%LOCALAPPDATA%\GRAL`.

One `key = value ! comment` per line. Text after `!` is a comment, as in `in.dat` (so a value cannot contain `!`). Blank lines and lines that start with `!` or `#` are ignored. Keys are not case-sensitive, the value is trimmed, and an empty value means "not set". The file is UTF-8, with or without a byte order mark; a UTF-16 or UTF-32 file is rejected as an invalid file. An example with every key (only `use_gpu` is needed):

```
! GPU settings of this project
use_gpu = 1               ! 1: particle dispersion on the GPU; 0 or no entry: CPU
reproducible_sums =       ! with reproducible results (in.dat line 22): 1 or no entry: integer sums; 0: floating-point sums
device =                  ! device ordinal within the backend; no entry: automatic
device_name =             ! name of that device; another name at the ordinal makes the choice automatic again
backend =                 ! cuda or hip; no entry: automatic
allow_integrated =        ! 1: an integrated AMD GPU may be used when no other is found
fallback_on_startup =     ! 1: run on the CPU when the GPU cannot start
nvrtc_dir =               ! use only this NVRTC folder
hiprtc_dir =              ! use only this hiprtc folder
chunk =                   ! largest number of particles per launch, 1 to 65536
block =                   ! threads per block, 128 or 256
max_kernel_ms =           ! kernel-time limit for the launch sizing, 0: off
memory_budget_bytes =     ! upper limit for GRAL's device buffers
memory_reserve_bytes =    ! VRAM left for the display and other programs
pinned_budget_bytes =     ! limit for particle windows in mapped host memory
cache_dir =               ! folder for compiled kernels, or off
```

| Key | Variable | Value |
|---|---|---|
| `use_gpu` | `INU_GPU_EXPERIMENTAL` | `1`: particle dispersion on the GPU; `0` or no entry: CPU |
| `reproducible_sums` | `INU_GPU_REPRODUCIBLE_SUMS` | `0` or `1`; only with `in.dat` line 22 = 1 and the GPU on. No entry or `1`: integer sums; `0`: floating-point sums |
| `device` | `INU_GPU_DEVICE` | nonnegative integer |
| `device_name` | none | name of the device chosen with `device` |
| `backend` | `INU_GPU_BACKEND` | `cuda` or `hip` |
| `allow_integrated` | none | `1`: an integrated AMD GPU may be used when the automatic choice finds no other (otherwise `only_integrated`); `0` or no entry: it may not |
| `fallback_on_startup` | `INU_GPU_FALLBACK_ON_STARTUP` | `0` or `1` |
| `nvrtc_dir` | `INU_GPU_NVRTC_DIR` | absolute folder of this machine |
| `hiprtc_dir` | `INU_GPU_HIPRTC_DIR` | absolute folder of this machine |
| `chunk` | `INU_GRAL_GPU_CHUNK` | as the variable |
| `block` | `INU_GRAL_GPU_BLOCK` | as the variable |
| `max_kernel_ms` | `INU_GPU_MAX_KERNEL_MS` | as the variable |
| `memory_budget_bytes` | `INU_GPU_MEMORY_BUDGET_BYTES` | as the variable |
| `memory_reserve_bytes` | `INU_GPU_MEMORY_RESERVE_BYTES` | as the variable |
| `pinned_budget_bytes` | `INU_GPU_PINNED_BUDGET_BYTES` | as the variable |
| `cache_dir` | `INU_GPU_CACHE_DIR` | as the variable |

For each key the environment variable wins when it is set and not empty, then the file, then the default. `INU_GPU_EXPERIMENTAL` counts only as `1` or `0`.

`nvrtc_dir`, `hiprtc_dir` and `cache_dir` name folders of one machine. In a `GPU.txt` that is copied to another machine, and above all between Windows and Linux, remove or change them: a Windows path is not an absolute path on Linux and the other way round, so `nvrtc_dir` or `hiprtc_dir` is then an invalid value (the message says which form is expected), and such a `cache_dir` only switches the kernel cache off.

An unknown key, a line without `=` or an invalid value is an error: the message names the file, the line number and the key, and the check reports `invalid_setting`. A run stops before its first situation with exit code 1, also when startup fallback is on and also when the file or `INU_GPU_EXPERIMENTAL=0` leaves the GPU off, because every run reads the file. The values get the same checks as the variables. A value of the file that turns out to be unusable only when the backend starts (a `device` ordinal beyond the last device without `device_name`, or a compiler folder without a compiler) also stops the run when startup fallback is on. The same value from a variable is a normal startup failure, which falls back when fallback is on.

`device_name` guards the ordinal: device ordinals change when a card is swapped or the driver renumbers the devices. When the file gives `device` and `device_name`, and the device at that ordinal has another name or does not exist, GRAL ignores the file's device, chooses the device automatically and writes a warning (console, GRAL log, `warnings` of the check). `INU_GPU_DEVICE` is always used as given.

`GRAL.exe --gpu-check` (or `dotnet GRAL.dll --gpu-check`) checks that the manifest and `generated.cu` exist and that the manifest can be read, then starts the backend as a GPU run would, with the same settings (variables and `GPU.txt`) and search order: driver, device, runtime compiler and a probe compile. When the backend has started, the check also applies the syntax checks of the settings that only a run uses (`INU_GPU_SPECIALIZE`, `INU_GPU_REPRODUCIBLE_SUMS`, launch size, threads per block, kernel-time limit and the three memory settings, from the variable or the file) and reports a bad value as `invalid_setting`; it checks `INU_GPU_SPECIALIZE` on CUDA too, where a run ignores it. It does not use these values, does not look at the cache folder, and does not compile the real kernel. It takes this argument alone, or with one folder (`--gpu-check <Computation folder>`), which then is the working directory as in a run. With any other argument it prints a usage line and ends with exit code 2; it never starts a run. It reads the file named by `INU_GPU_SETTINGS_FILE`, or `GPU.txt` in the working directory; a front end that starts the check outside the project folder gives the folder or sets the variable to the project's `GPU.txt`. The check does not need `use_gpu = 1`: it tells whether the GPU could start. It reads nothing else of a project (so nothing from `in.dat`) and writes no file, and prints one line, `GRAL_GPU_CHECK {...}`. The exit code is 0 when the status is `ok` and 2 otherwise. Besides `schema`, `manifest`, `status`, the error `message`, `log` and the fields of the chosen device, the line has:

| Key | Content |
|---|---|
| `settings_file`, `settings_file_status` | full path of the GPU settings file that was looked at (`null` with `INU_GPU_SETTINGS_FILE=off`), and `read`, `absent` or `off` |
| `settings` | one entry per key of the settings file that has a value, `use_gpu` and `reproducible_sums` included: `{"value": "...", "source": "env"}` or `"file"` |
| `devices` | every device of the backend that was started: `backend`, `ordinal`, `name`, `total_memory_bytes`, `integrated` (`null` when the driver does not say) and `selected`. A name or a size the driver does not give is `""` or `0`. Present, possibly empty, also after a failure. No context is created for it, and a run does not make this list |
| `other_backends` | backends whose runtime library is installed but which were not started, for example `["hip"]` on a PC with an NVIDIA GPU and the AMD HIP runtime; a file test, the library is not loaded (on Linux, a HIP runtime that is in none of the ROCm folders is looked up through the loader, which opens and closes it). `cuda` is listed only when the backend setting `hip` skipped it: on the automatic path CUDA has already been tried |
| `warnings` | warnings that did not stop the start, for example a `device_name` that no longer matches |

The device fields found so far (and `backend`) are also there when a later step fails, for example with `nvrtc_rejected`. The statuses are:

| Status | Meaning |
|---|---|
| `ok` | the GPU backend starts |
| `no_manifest` | the runtime files are missing or the manifest cannot be read |
| `no_driver`, `driver_error` | no NVIDIA driver or HIP 7 runtime, or a driver library that does not load |
| `no_device` | the driver reports no device |
| `only_integrated` | only integrated AMD GPUs are visible, and neither `allow_integrated = 1` nor `INU_GPU_BACKEND=hip` is set |
| `no_nvrtc`, `no_hiprtc` | no runtime compiler was found |
| `nvrtc_rejected`, `hiprtc_rejected` | runtime compilers were found, but none compiles for the device |
| `invalid_setting` | an `INU_GPU_*` or `INU_GRAL_GPU_*` value or an entry of `GPU.txt` is invalid |
| `failed` | any other startup error |

Optional settings (those with a key in `GPU.txt` can be set there instead):

| Variable | Default | Effect |
|---|---|---|
| `INU_GPU_BACKEND` | unset | `cuda` or `hip`; unset chooses as described in "Choosing the backend" |
| `INU_GPU_DEVICE` | `0` on CUDA; on HIP the first AMD GPU that is not integrated (device 0 if all are integrated and `INU_GPU_BACKEND=hip`) | device ordinal within the chosen backend; a set value is used as given, also for an integrated GPU |
| `INU_GPU_NVRTC_DIR` | unset | use only this NVRTC folder |
| `INU_GPU_HIPRTC_DIR` | unset | use only this hiprtc folder |
| `INU_GPU_HIP_RUNTIME` | unset | absolute path of another HIP 7 runtime (`amdhip64_7.dll`, on Linux `libamdhip64.so.7`) to use instead of the driver's System32 copy or the ROCm folders |
| `INU_GPU_HIPRTC_EXTRA` | unset | extra hiprtc options, separated by spaces, for diagnosis; they become part of the cache key |
| `INU_GPU_SPECIALIZE` | `1` | HIP only, CUDA ignores it: `1`, `on` or unset uses all specialization steps; `0` or `off` keeps the general kernel; a comma list of `flags`, `ints`, `grid`, `depo` and `src` uses only those steps (case is ignored). An unknown step or a list without a step is a GPU startup error |
| `INU_GPU_CACHE_DIR` | `%LOCALAPPDATA%\GRAL\GpuModuleCache`; on Linux `GRAL/GpuModuleCache` in `$XDG_DATA_HOME`, else in `~/.local/share` | local folder for compiled kernels, so later runs skip the NVRTC or hiprtc compile; `off` disables it. The folder is created when it does not exist. A value that is not an absolute local folder of the system, or a folder that cannot be written, does not stop the run: the kernel is then compiled in every run, and the GRAL log has a line `GPU kernel cache: not used (...)`. On HIP, each project configuration adds its own specialized kernels; entries are never deleted |
| `INU_GPU_FALLBACK_ON_STARTUP` | `0` | `1`: if the GPU cannot start (no driver, no NVRTC or hiprtc, missing manifest, or an invalid `INU_GPU_BACKEND`, `INU_GPU_HIPRTC_DIR`, `INU_GPU_HIP_RUNTIME`, `INU_GPU_DEVICE`, `INU_GPU_MEMORY_RESERVE_BYTES`, `INU_GPU_MEMORY_BUDGET_BYTES`, `INU_GPU_MAX_KERNEL_MS`, `INU_GPU_SPECIALIZE` (HIP only), `INU_GPU_REPRODUCIBLE_SUMS`, `INU_GRAL_GPU_CHUNK` or `INU_GRAL_GPU_BLOCK` value), run the whole job on the CPU and log the reason. An invalid `INU_GPU_PINNED_BUDGET_BYTES` variable is found only at the first launch and stops the run, and so does an invalid `GPU.txt`, whatever this setting says |
| `INU_GPU_REPRODUCIBLE_SUMS` | unset: `reproducible_sums` in `GPU.txt`, which defaults to `1` | only with fixed random seeds (in.dat line 22, "Use fixed random seed value" = 1): `1` adds the concentration, deposition, odour, vertical-concentration, receptor and emission totals in integers, so that repeated runs on the same GPU give the same result files (tested on an RX 9070 XT and an RTX 5080; see below); `0` uses the floating-point atomic additions. Native-seed runs always use the floating-point additions |
| `INU_GPU_MEMORY_RESERVE_BYTES` | 10 % of VRAM, 128-512 MiB, at most a quarter of VRAM | VRAM left for the display and other programs |
| `INU_GPU_MEMORY_BUDGET_BYTES` | all VRAM | upper limit for GRAL's device buffers; larger arrays go to mapped host memory |
| `INU_GPU_PINNED_BUDGET_BYTES` | 256 MiB | limit for the particle windows and scratch rows placed in mapped host memory when they do not fit within the device budget; other arrays placed there (above `INU_GPU_MEMORY_BUDGET_BYTES` or after a failed device allocation) do not count against it |
| `INU_GPU_MAX_KERNEL_MS` | `500` | on a GPU with the display watchdog, each launch is sized from the time of the previous one, aiming at half this limit; single launches can still exceed it (`launches_over_max_kernel_ms` in the metrics); `0` turns the sizing off. On the tested AMD GPU the HIP runtime reported no watchdog (`kernel_exec_timeout=0`), so this setting had no effect there; the RX 9070 XT also drove the display, and single launches took up to 5.5 s without a driver reset |
| `INU_GRAL_GPU_CHUNK` | `65536` | largest number of particles per launch (1 to 65536); launches can be smaller on a GPU with the display watchdog. With reproducible sums the results do not depend on it; without them the floating-point sums depend on how the particles are split into launches, so the last bits can change (a launch-size change altered 48 of 98 result files of a wet-deposition project, see below) |
| `INU_GRAL_GPU_BLOCK` | `128` | threads per block, 128 or 256 |

For developers (variables only, not in `GPU.txt` and not checked by `--gpu-check`):

- `INU_GPU_FORCE_CUBIN=1`: CUDA only; load the kernel as a CUBIN without trying PTX first. Any other value is ignored.
- `INU_GPU_CACHE_COORDINATES=1`: also hash the particle start coordinates and send them to the GPU only when they differ from those of the previous situation (off by default, because they usually change); `0` or unset is off, another value stops the run.
- `INU_GPU_TRACE_SITUATION` (default 1) selects the situation that is traced; `INU_GPU_DEVICE_TRACE`, `INU_GPU_DEVICE_STEP_TRACE` (needs `INU_GPU_DEVICE_TRACE`) and `INU_GPU_DEVICE_RESIDUAL_TRACE` name the files for the per-particle final state, the per-step state of a fixed set of particles and the residual-particle state; `INU_GPU_RESIDUAL_TRACE_COUNT` (default 64, 1 to 65536) sets the number of traced residual particles. These are diagnostic outputs of the kernel for one situation.

## What runs on the GPU

Transient and stationary runs with terrain or flat terrain; meteopgt.all meteorology, and in stationary mode also ZR, EKI and Sonic profiles; point, line, area and portal sources including tunnel entrances and opposite lanes; buildings; dry and wet deposition; decay; odour output; vertical concentration output; receptors; and up to nine concentration layers.

The inputs of each situation are checked before they go to the GPU; an invalid value (for example a non-finite particle value) stops the run with an error. Invalid `INU_GRAL_GPU_CHUNK` or `INU_GRAL_GPU_BLOCK` values count as a startup failure. A few inputs that the GPU cannot take run on the CPU instead: transient mode with meteorology other than meteopgt.all, a meteorology profile count outside 1 to 50 when the GPU needs the profile (flat terrain without buildings, or ZR or Sonic meteorology), and accumulated concentration or deposition fields that already hold non-finite values (for example from a restart file). GRAL then prints `GPU fallback to CPU: <reason>` and writes the reason to the GRAL log.

With fixed random seeds the GPU uses the same random streams as the CPU; floating-point rounding leaves small differences, most likely from the math functions (CUDA and .NET implementations can differ in the last bits; the kernel is compiled without FMA contraction), far below the difference between two CPU seeds. With native seeds every run draws new random streams on either backend, so two GPU runs differ from each other about as much as two CPU runs do. The GPU derives each fresh particle's seed by hashing a random master, the situation and the particle number; in the native-seed tests no run statistic (maximum, P99, P95, mean) separated these GPU runs from CPU runs.

Many particles add to the same output cell at the same time, and the GPU runs these atomic additions in no fixed order. A floating-point sum depends on the order, so without further measures two runs with the same fixed seeds can differ in the last bits. In transient runs, these bits change the masses of the residual particles of the next situation, which come from the transient concentration field, and the difference spreads from there. The receptor file and GRAL's ASCII concentration files print the full double value, so they show such differences directly. The CPU path has the same property: its threads add under a lock in the order they arrive.

For this reason, a fixed-seed run on the GPU adds in integers instead (`INU_GPU_REPRODUCIBLE_SUMS`). GRAL compiles a separate kernel for it; the kernel of other runs is unchanged (its CUDA PTX for compute_75 and compute_120 is byte-identical to that of the kernel before this option; the HIP code object was not compared). Each addend is split into three 64-bit integers in fixed units of 1, 2^-32 and 2^-64. Integer additions give the same total in any order. When the middle or low word of a cell reaches 2^62 or -2^62, the thread that crossed the limit moves 2^62 of it into the next higher word, so these two words never overflow, however many particles add to the cell. When the situation ends, a small kernel adds each total once to its output array; it converts the integers to floating point only there. A non-finite addend, an addend whose magnitude is 2^62 or more, or a total near the end of the high word's 64-bit range stops the run with a device error that names the option. Addends smaller than about 2.7e-20 are lost, so cells whose totals are only a few orders of magnitude above that keep fewer significant digits (in a steady deposition test, cells of 1e-17 to 1e-13 differed from the floating-point sums by up to 1 %, while the field totals agreed to 16 digits). The integer words take 24 bytes per output element (about 250 MiB in test case B00); the memory manager places them like the other output arrays, in device memory if the budget allows and otherwise in mapped host memory. The units do not depend on the project, so a run that starts at a later situation uses the same units as a full run.

Tests on an RX 9070 XT: nine projects ran three times each, with launch sizes of 65,536, 4,096 and 1,000 particles and 128 or 256 threads per block, and their result files (`.con`, `.dep` and odour entries, `ReceptorConcentrations.dat`, ASCII results) were byte-identical. They covered transient runs with dry or wet deposition, steady runs with terrain and receptors, and tunnel and odour projects. B00 at two launch sizes and a vertical-concentration project run twice were byte-identical as well, and the final code gave the same bytes as these runs in the four projects rerun with it. The general kernel (`INU_GPU_SPECIALIZE=0`, the form CUDA uses) gave the same bytes as the specialized one, and so did runs with the integer words in mapped host memory. Without the option, the same build reproduced the earlier differences in two transient deposition projects, and B00 output and kernel time (7.46 s against 7.47 s) matched the build without the option. The integer sums round differently from the floating-point sums, so their results differ slightly; the pooled NMAE of the concentrations between the two was 2e-5 to 4e-5 in three transient projects. In one session the kernels took 12 % longer in B00, 5 % less time in a transient dry-deposition project and less than half the time in a wet-deposition project. The gains probably come from avoiding the compare-and-swap loop that replaces the missing double-precision atomic add on gfx1201; the end-of-situation kernels took about 55 ms of the B00 increase of about 0.9 s. Tests on an RTX 5080: nine projects, B00 among them, gave byte-identical result files over two runs, launch sizes of 65,536, 4,096 and 1,000 particles, 256 threads per block, and display-watchdog launch sizing at 50 ms, the default 500 ms and off (`INU_GPU_MAX_KERNEL_MS`); the CUBIN path, a lowered carry threshold, and integer words placed in mapped host memory at the start gave the same bytes (a move to host memory during the run was not triggered). Without the option, B00 gave the same bytes as before this option existed, and a launch-size change altered 48 of 98 result files of a wet-deposition project, against none with the option. On B00 the kernels took 4 % longer with the option (7.53 s against 7.25 s, three alternating runs each on an otherwise idle GPU). The CUDA kernel was compiled but not run for compute_50 and compute_75. The option does not make GPU and CPU results identical; the most likely main cause is that the math functions of CUDA/HIP and .NET can differ in the last bits. It also does not cover situations that fall back to the CPU, and `.grz` archives still differ, because each zip entry stores its write time.

## When something goes wrong

- An invalid `GPU.txt` ends GRAL before the first situation: `ERROR: GPU settings file GPU.txt line N: ...` on the console, the same text in `Problemreport_GRAL.txt` and `Logfile_GRALCore.txt`, and exit code 1.
- A startup failure without fallback, or an error after GPU work has started, ends GRAL like its other failed runs: the reason is written to the console (`ERROR: GPU failure in weather situation N: ...`), to `Problemreport_GRAL.txt` and to `Logfile_GRALCore.txt`, the log ends with `GRAL simulations stopped with errors`, and the exit code is 1. A reason longer than 2000 characters is cut there; `GRAL_GPU_runtime.log` has all of it. The vertical concentration file is not written from the unfinished situation, and the transient checkpoint files (`*.tmp`) of the last finished situation are kept, as after an interrupted run.
- `GPU startup failed: ...`: the message names the missing piece (driver, device ordinal, NVRTC, manifest). Set `INU_GPU_FALLBACK_ON_STARTUP=1`, or `fallback_on_startup = 1` in `GPU.txt`, to run on the CPU instead; the run then finishes with exit code 0 and the log says that the whole run fell back.
- A device error or watchdog reset during a run stops GRAL with an error; it is not retried on the CPU. Lower `INU_GPU_MAX_KERNEL_MS` on slow NVIDIA display GPUs; on an AMD GPU that resets, lower `INU_GRAL_GPU_CHUNK` instead.
- On a machine with only AMD GPUs, a startup failure that names only the NVIDIA driver means that GRAL found no HIP 7 runtime (`amdhip64_7.dll` in System32, or on Linux `libamdhip64.so.7` in the ROCm folders or the loader path), or that a leftover NVIDIA driver loads but fails. Update the Adrenalin driver (on Linux, install ROCm 7 or later) or set `INU_GPU_HIP_RUNTIME`; with a leftover NVIDIA driver, set `INU_GPU_BACKEND=hip`.
- Stopping GRAL (Ctrl+C or the GUI) takes effect at the next GPU startup step, kernel launch or transfer, and GRAL finishes as on the CPU path.
- The GPU driver and the runtime compiler (NVRTC or hiprtc) run inside the GRAL process. An NVRTC or hiprtc that cannot be loaded is reported as a startup failure, but a crash inside the driver, NVRTC, hiprtc or `amd_comgr` (for example a compiler library that faults while compiling) ends GRAL, and `INU_GPU_FALLBACK_ON_STARTUP` cannot turn it into a CPU run. Point `INU_GPU_NVRTC_DIR` (or `INU_GPU_HIPRTC_DIR` on AMD) at a known-good compiler if that happens.
- The commits that added the settings file and moved it to the project's `GPU.txt`, the log lines, the check additions and the exit handling were first run on an RTX 5080 (CUDA). Their HIP side ran later on an RX 9070 XT, 13 projects on Windows and the same 13 in WSL2, with every situation on the GPU. `device_name`, `allow_integrated`, the rule for `backend = hip` in `GPU.txt` and the device list on HIP are still untested, and so is any PC with more than one GPU.
- Several GRAL processes may run on one GPU. If they get the same log paths, the second one adds its process id and a time stamp to its file names. They may also share one cache folder (entries are written to a temporary file, moved into place and checked when read); concurrent use of one cache folder has not been tested.
