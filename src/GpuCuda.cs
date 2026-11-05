using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace GRAL_2001
{
    /// <summary>CUDA driver or NVRTC call that returned an error code.</summary>
    internal sealed class GpuCudaException : Exception
    {
        internal int Code { get; }
        internal GpuCudaException(int code, string operation, string name) : base(operation + ": " + code + " '" + name + "'") { Code = code; }
    }

    /// <summary>
    /// CUDA driver and NVRTC bindings for the GPU path (NativeLibrary + unmanaged function pointers, so it works with
    /// DisableRuntimeMarshalling). Selects the device, finds a working NVRTC, compiles and loads the kernel module.
    /// Every call must come from the thread that created the object, because the CUDA context is current there.
    /// </summary>
    internal sealed unsafe class GpuCuda : IDisposable
    {
        internal const int ErrorOutOfMemory = 2;
        private const int ErrorDeviceUnavailable = 46;
        // Module images the driver cannot use: invalid image, no binary for GPU, invalid PTX, unsupported PTX version,
        // and 221/223 when the driver cannot JIT PTX at all; a CUBIN may still load.
        internal static readonly int[] RejectedImageCodes = { 200, 209, 218, 221, 222, 223 };
        private const int AttrKernelExecTimeout = 17, AttrCanMapHostMemory = 19, AttrComputeMode = 20, AttrTccDriver = 35,
                          AttrCcMajor = 75, AttrCcMinor = 76, AttrSingleToDoublePerfRatio = 87;
        // Project sizes are runtime array shapes, so they never enter the compile options and the module cache key stays stable.
        internal static readonly string[] NvrtcOptions = { "--std=c++11", "--fmad=false" };
        internal const string NvrtcProgram = "kernel.cu";

        private static readonly bool Windows = OperatingSystem.IsWindows();
        private static string DriverLibrary => Windows ? "nvcuda.dll" : "libcuda.so.1";
        private static string NvrtcGlob => Windows ? "nvrtc64_*.dll" : "libnvrtc.so*";
        private static readonly Regex NvrtcName = Windows ? new Regex(@"^nvrtc64_[0-9]+_[0-9]+\.dll$", RegexOptions.IgnoreCase)
                                                          : new Regex(@"^libnvrtc\.so(\.[0-9]+)*$");

        private IntPtr driver, nvrtc, nvrtcBuiltins, context;
        private readonly List<ulong> buffers = new List<ulong>();
        private readonly List<IntPtr> hostBuffers = new List<IntPtr>();
        private readonly List<IntPtr> modules = new List<IntPtr>();
        private readonly List<IntPtr> events = new List<IntPtr>();
        private readonly Action<string> log;

        // Driver API
        private delegate* unmanaged<uint, int> cuInit;
        private delegate* unmanaged<int*, int> cuDriverGetVersion, cuDeviceGetCount;
        private delegate* unmanaged<int*, int, int> cuDeviceGet;
        private delegate* unmanaged<byte*, int, int, int> cuDeviceGetName;
        private delegate* unmanaged<nuint*, int, int> cuDeviceTotalMem;
        private delegate* unmanaged<int*, int, int, int> cuDeviceGetAttribute;
        private delegate* unmanaged<IntPtr*, uint, int, int> cuCtxCreate;
        private delegate* unmanaged<IntPtr, int> cuCtxDestroy, cuModuleUnload, cuEventSynchronize, cuEventDestroy;
        private delegate* unmanaged<IntPtr*, void*, int> cuModuleLoadData;
        private delegate* unmanaged<IntPtr*, IntPtr, byte*, int> cuModuleGetFunction;
        private delegate* unmanaged<ulong*, nuint*, IntPtr, byte*, int> cuModuleGetGlobal;
        private delegate* unmanaged<int*, int, IntPtr, int> cuFuncGetAttribute;
        private delegate* unmanaged<ulong*, nuint, int> cuMemAlloc;
        private delegate* unmanaged<ulong, int> cuMemFree;
        private delegate* unmanaged<nuint*, nuint*, int> cuMemGetInfo;
        private delegate* unmanaged<void**, nuint, uint, int> cuMemHostAlloc;
        private delegate* unmanaged<void*, int> cuMemFreeHost;
        private delegate* unmanaged<ulong*, void*, uint, int> cuMemHostGetDevicePointer;
        private delegate* unmanaged<ulong, byte, nuint, int> cuMemsetD8;
        private delegate* unmanaged<ulong, void*, nuint, int> cuMemcpyHtoD;
        private delegate* unmanaged<void*, ulong, nuint, int> cuMemcpyDtoH;
        private delegate* unmanaged<int> cuCtxSynchronize;
        private delegate* unmanaged<IntPtr, uint, uint, uint, uint, uint, uint, uint, IntPtr, void**, void**, int> cuLaunchKernel;
        private delegate* unmanaged<IntPtr*, uint, int> cuEventCreate;
        private delegate* unmanaged<IntPtr, IntPtr, int> cuEventRecord;
        private delegate* unmanaged<float*, IntPtr, IntPtr, int> cuEventElapsedTime;
        private delegate* unmanaged<int, byte**, int> cuGetErrorName;

        // NVRTC
        private delegate* unmanaged<IntPtr*, byte*, byte*, int, byte**, byte**, int> nvrtcCreateProgram;
        private delegate* unmanaged<IntPtr, int, byte**, int> nvrtcCompileProgram;
        private delegate* unmanaged<IntPtr, nuint*, int> nvrtcGetProgramLogSize, nvrtcGetPTXSize, nvrtcGetCUBINSize;
        private delegate* unmanaged<IntPtr, byte*, int> nvrtcGetProgramLog, nvrtcGetPTX, nvrtcGetCUBIN;
        private delegate* unmanaged<IntPtr*, int> nvrtcDestroyProgram;

        internal int DeviceOrdinal { get; private set; }
        internal int DeviceArch { get; private set; }
        internal bool CanMapHostMemory { get; private set; }
        internal bool KernelExecTimeout { get; private set; }
        internal int TargetArch { get; private set; }
        internal string BinaryKind { get; private set; } = "ptx";
        internal string NvrtcPath { get; private set; }
        internal string NvrtcBuiltinsPath { get; private set; }
        internal (int Major, int Minor) NvrtcVersion { get; private set; }
        internal string[] CompileOptions { get; private set; }
        internal double ContextMs { get; private set; }
        internal Dictionary<string, object> DeviceInfo { get; } = new Dictionary<string, object>();

        internal GpuCuda(string runtimeDirectory, Action<string> log)
        {
            this.log = log;
            try { Initialize(runtimeDirectory); }
            catch { Dispose(); throw; }
        }

        private static IntPtr Export(IntPtr library, string name) => NativeLibrary.GetExport(library, name);
        private static bool TryExport(IntPtr library, string name, out IntPtr address) => NativeLibrary.TryGetExport(library, name, out address);

        private void BindDriver()
        {
            IntPtr d = driver;
            cuInit = (delegate* unmanaged<uint, int>)Export(d, "cuInit");
            cuDriverGetVersion = (delegate* unmanaged<int*, int>)Export(d, "cuDriverGetVersion");
            cuDeviceGetCount = (delegate* unmanaged<int*, int>)Export(d, "cuDeviceGetCount");
            cuDeviceGet = (delegate* unmanaged<int*, int, int>)Export(d, "cuDeviceGet");
            cuDeviceGetName = (delegate* unmanaged<byte*, int, int, int>)Export(d, "cuDeviceGetName");
            cuDeviceTotalMem = (delegate* unmanaged<nuint*, int, int>)Export(d, "cuDeviceTotalMem_v2");
            cuDeviceGetAttribute = (delegate* unmanaged<int*, int, int, int>)Export(d, "cuDeviceGetAttribute");
            cuCtxCreate = (delegate* unmanaged<IntPtr*, uint, int, int>)Export(d, "cuCtxCreate_v2");
            cuCtxDestroy = (delegate* unmanaged<IntPtr, int>)Export(d, "cuCtxDestroy_v2");
            cuModuleLoadData = (delegate* unmanaged<IntPtr*, void*, int>)Export(d, "cuModuleLoadData");
            cuModuleUnload = (delegate* unmanaged<IntPtr, int>)Export(d, "cuModuleUnload");
            cuModuleGetFunction = (delegate* unmanaged<IntPtr*, IntPtr, byte*, int>)Export(d, "cuModuleGetFunction");
            cuModuleGetGlobal = (delegate* unmanaged<ulong*, nuint*, IntPtr, byte*, int>)Export(d, "cuModuleGetGlobal_v2");
            cuFuncGetAttribute = (delegate* unmanaged<int*, int, IntPtr, int>)Export(d, "cuFuncGetAttribute");
            cuMemAlloc = (delegate* unmanaged<ulong*, nuint, int>)Export(d, "cuMemAlloc_v2");
            cuMemFree = (delegate* unmanaged<ulong, int>)Export(d, "cuMemFree_v2");
            cuMemGetInfo = (delegate* unmanaged<nuint*, nuint*, int>)Export(d, "cuMemGetInfo_v2");
            cuMemHostAlloc = (delegate* unmanaged<void**, nuint, uint, int>)Export(d, "cuMemHostAlloc");
            cuMemFreeHost = (delegate* unmanaged<void*, int>)Export(d, "cuMemFreeHost");
            cuMemHostGetDevicePointer = (delegate* unmanaged<ulong*, void*, uint, int>)Export(d, "cuMemHostGetDevicePointer_v2");
            cuMemsetD8 = (delegate* unmanaged<ulong, byte, nuint, int>)Export(d, "cuMemsetD8_v2");
            cuMemcpyHtoD = (delegate* unmanaged<ulong, void*, nuint, int>)Export(d, "cuMemcpyHtoD_v2");
            cuMemcpyDtoH = (delegate* unmanaged<void*, ulong, nuint, int>)Export(d, "cuMemcpyDtoH_v2");
            cuCtxSynchronize = (delegate* unmanaged<int>)Export(d, "cuCtxSynchronize");
            cuLaunchKernel = (delegate* unmanaged<IntPtr, uint, uint, uint, uint, uint, uint, uint, IntPtr, void**, void**, int>)Export(d, "cuLaunchKernel");
            cuEventCreate = (delegate* unmanaged<IntPtr*, uint, int>)Export(d, "cuEventCreate");
            cuEventRecord = (delegate* unmanaged<IntPtr, IntPtr, int>)Export(d, "cuEventRecord");
            cuEventSynchronize = (delegate* unmanaged<IntPtr, int>)Export(d, "cuEventSynchronize");
            cuEventElapsedTime = (delegate* unmanaged<float*, IntPtr, IntPtr, int>)Export(d, "cuEventElapsedTime");
            cuEventDestroy = (delegate* unmanaged<IntPtr, int>)Export(d, "cuEventDestroy_v2");
            cuGetErrorName = (delegate* unmanaged<int, byte**, int>)Export(d, "cuGetErrorName");
        }

        private void BindNvrtc(IntPtr library)
        {
            nvrtcCreateProgram = (delegate* unmanaged<IntPtr*, byte*, byte*, int, byte**, byte**, int>)Export(library, "nvrtcCreateProgram");
            nvrtcCompileProgram = (delegate* unmanaged<IntPtr, int, byte**, int>)Export(library, "nvrtcCompileProgram");
            nvrtcGetProgramLogSize = (delegate* unmanaged<IntPtr, nuint*, int>)Export(library, "nvrtcGetProgramLogSize");
            nvrtcGetProgramLog = (delegate* unmanaged<IntPtr, byte*, int>)Export(library, "nvrtcGetProgramLog");
            nvrtcGetPTXSize = (delegate* unmanaged<IntPtr, nuint*, int>)Export(library, "nvrtcGetPTXSize");
            nvrtcGetPTX = (delegate* unmanaged<IntPtr, byte*, int>)Export(library, "nvrtcGetPTX");
            if (TryExport(library, "nvrtcGetCUBINSize", out IntPtr size) && TryExport(library, "nvrtcGetCUBIN", out IntPtr data))
            {
                nvrtcGetCUBINSize = (delegate* unmanaged<IntPtr, nuint*, int>)size;
                nvrtcGetCUBIN = (delegate* unmanaged<IntPtr, byte*, int>)data;
            }
            else { nvrtcGetCUBINSize = null; nvrtcGetCUBIN = null; }
            nvrtcDestroyProgram = (delegate* unmanaged<IntPtr*, int>)Export(library, "nvrtcDestroyProgram");
        }

        internal void Check(int code, string operation)
        {
            if (code == 0) return;
            string name = "?";
            byte* text = null;
            if (cuGetErrorName != null && cuGetErrorName(code, &text) == 0 && text != null) name = Marshal.PtrToStringUTF8((IntPtr)text) ?? "?";
            throw new GpuCudaException(code, operation, name);
        }

        private int Attribute(int code, int device, string label)
        {
            int value;
            Check(cuDeviceGetAttribute(&value, code, device), "device attribute " + label);
            return value;
        }

        private static string DriverText(int version) => (version / 1000) + "." + (version % 1000 / 10);

        internal static string NvrtcRequirement(int arch)
        {
            if (arch < 50) return "compute capability below 5.0 is not supported by NVRTC 12.x or 13.x";
            if (arch < 75) return "this device needs NVRTC 12.x; NVRTC 13.x requires compute capability 7.5 or newer";
            return "this device needs NVRTC 12.x or 13.x";
        }

        private static int DeviceOrdinalSetting(string raw, int count)
        {
            System.Numerics.BigInteger ordinal;
            if (string.IsNullOrEmpty(raw)) ordinal = 0;
            else if (raw.All(ch => ch >= '0' && ch <= '9')) ordinal = System.Numerics.BigInteger.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
            else throw new ArgumentException("INU_GPU_DEVICE must be a nonnegative integer, got '" + raw + "'");
            if (count < 1) throw new InvalidOperationException("No CUDA device is visible (check the NVIDIA driver and CUDA_VISIBLE_DEVICES)");
            if (ordinal >= count)
                throw new ArgumentException("INU_GPU_DEVICE=" + ordinal + " is out of range: " + count + " CUDA device(s) visible (valid 0.." + (count - 1) + "; CUDA_VISIBLE_DEVICES renumbers devices)");
            return (int)ordinal;
        }

        private void Initialize(string runtimeDirectory)
        {
            // On Windows the driver comes from System32 only, never from the project folder or PATH.
            try { driver = Windows ? NativeLibrary.Load(DriverLibrary, typeof(GpuCuda).Assembly, DllImportSearchPath.System32) : NativeLibrary.Load(DriverLibrary); }
            catch (Exception error) when (error is DllNotFoundException || error is BadImageFormatException)
            { throw new InvalidOperationException("NVIDIA driver library " + DriverLibrary + " could not be loaded (" + error.Message + "); install the NVIDIA display driver", error); }
            BindDriver();
            var clock = Stopwatch.StartNew();
            Check(cuInit(0), "cuInit");
            int driverVersion, count, device;
            Check(cuDriverGetVersion(&driverVersion), "cuDriverGetVersion");
            Check(cuDeviceGetCount(&count), "cuDeviceGetCount");
            DeviceOrdinal = DeviceOrdinalSetting(Environment.GetEnvironmentVariable("INU_GPU_DEVICE"), count);
            Check(cuDeviceGet(&device, DeviceOrdinal), "cuDeviceGet");
            byte* nameBuffer = stackalloc byte[256];
            Check(cuDeviceGetName(nameBuffer, 256, device), "cuDeviceGetName");
            string name = Marshal.PtrToStringUTF8((IntPtr)nameBuffer) ?? "?";
            nuint total;
            Check(cuDeviceTotalMem(&total, device), "cuDeviceTotalMem");
            int ccMajor = Attribute(AttrCcMajor, device, "compute capability major");
            int ccMinor = Attribute(AttrCcMinor, device, "compute capability minor");
            DeviceArch = ccMajor * 10 + ccMinor;
            CanMapHostMemory = Attribute(AttrCanMapHostMemory, device, "canMapHostMemory") != 0;
            int computeMode = Attribute(AttrComputeMode, device, "compute mode");
            KernelExecTimeout = Attribute(AttrKernelExecTimeout, device, "kernel exec timeout") != 0;
            string modeText = computeMode switch { 0 => "default", 2 => "prohibited", 3 => "exclusive_process", _ => computeMode.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            DeviceInfo["ordinal"] = DeviceOrdinal;
            DeviceInfo["device_count"] = count;
            DeviceInfo["name"] = name;
            DeviceInfo["compute_capability"] = ccMajor + "." + ccMinor;
            DeviceInfo["total_memory_bytes"] = (long)total;
            DeviceInfo["driver_version"] = driverVersion;
            DeviceInfo["driver_cuda"] = DriverText(driverVersion);
            DeviceInfo["compute_mode"] = modeText;
            DeviceInfo["kernel_exec_timeout"] = KernelExecTimeout;
            DeviceInfo["tcc_driver"] = Attribute(AttrTccDriver, device, "TCC driver") != 0;
            DeviceInfo["fp32_to_fp64_perf_ratio"] = Attribute(AttrSingleToDoublePerfRatio, device, "single/double perf ratio");
            DeviceInfo["can_map_host_memory"] = CanMapHostMemory;
            DeviceInfo["platform"] = Windows ? "win32" : OperatingSystem.IsLinux() ? "linux" : RuntimeInformation.OSDescription;
            if (computeMode == 2)
                throw new InvalidOperationException("CUDA device " + DeviceOrdinal + " (" + name + ") is in prohibited compute mode; select another device with INU_GPU_DEVICE or change it with nvidia-smi -c");
            IntPtr ctx;
            int rc = cuCtxCreate(&ctx, 8, device);
            if (rc == ErrorDeviceUnavailable)
                throw new InvalidOperationException("CUDA device " + DeviceOrdinal + " (" + name + ") is unavailable (compute mode " + modeText + "; another process may own it)");
            Check(rc, "cuCtxCreate");
            context = ctx;
            var candidates = NvrtcCandidates(Environment.GetEnvironmentVariable("INU_GPU_NVRTC_DIR"), runtimeDirectory);
            SelectToolchain(candidates, driverVersion);
            string query = SupportedArchs != null ? "" : " (exact target unverified; NVRTC has no architecture query)";
            DeviceInfo["nvrtc_version"] = NvrtcVersion.Major + "." + NvrtcVersion.Minor;
            DeviceInfo["nvrtc_path"] = NvrtcPath;
            DeviceInfo["target_arch"] = TargetArch;
            DeviceInfo["binary_kind"] = BinaryKind;
            log("CUDA device " + DeviceOrdinal + " compute_" + DeviceArch + "; NVRTC " + NvrtcVersion.Major + "." + NvrtcVersion.Minor + "; target " +
                (BinaryKind == "ptx" ? "compute" : "sm") + "_" + TargetArch + query + "; " + name + "; " + ((long)total / (1024 * 1024)) + " MiB; driver " +
                DriverText(driverVersion) + " (" + driverVersion + "); kernel_exec_timeout=" + (KernelExecTimeout ? 1 : 0) + "; tcc=" +
                ((bool)DeviceInfo["tcc_driver"] ? 1 : 0) + "; fp32:fp64=" + DeviceInfo["fp32_to_fp64_perf_ratio"] + ":1; compute_mode=" + modeText);
            ContextMs = clock.Elapsed.TotalMilliseconds;
        }

        internal int[] SupportedArchs { get; private set; }

        /// <summary>
        /// NVRTC libraries to try, newest name first within each directory: INU_GPU_NVRTC_DIR alone when set; otherwise the
        /// runtime folder's <c>nvrtc</c> subfolder (also as a pip <c>--target</c> install of the NVRTC wheel), then the CUDA toolkits.
        /// </summary>
        internal static List<string> NvrtcCandidates(string explicitDirectory, string runtimeDirectory)
        {
            var directories = new List<string>();
            var ignored = new List<string>();
            if (explicitDirectory != null)
            {
                if (explicitDirectory.Length == 0 || !Path.IsPathFullyQualified(explicitDirectory)) throw new ArgumentException("INU_GPU_NVRTC_DIR must be a nonempty absolute path");
                directories.Add(explicitDirectory);
            }
            else
            {
                if (runtimeDirectory != null)
                {
                    string bundled = Path.Combine(runtimeDirectory, "nvrtc");
                    directories.Add(bundled);
                    // Layout of `pip install --target <runtime>/nvrtc nvidia-cuda-nvrtc-cu12` (CUDA 12) or the CUDA 13 wheel.
                    foreach (string sub in Windows ? new[] { "bin", Path.Combine("bin", "x86_64"), Path.Combine("bin", "x64") } : new[] { "lib" })
                    {
                        directories.Add(Path.Combine(bundled, "nvidia", "cuda_nvrtc", sub));
                        directories.Add(Path.Combine(bundled, "nvidia", "cu13", sub));
                    }
                }
                var roots = new List<(string Variable, string Root)>();
                foreach (string variable in Windows ? new[] { "CUDA_PATH" } : new[] { "CUDA_PATH", "CUDA_HOME" })
                    if (Environment.GetEnvironmentVariable(variable) is string root && root.Length > 0) roots.Add((variable, root));
                if (Windows)
                {
                    // Every installed CUDA toolkit sets CUDA_PATH_V<major>_<minor>; newest first.
                    var versioned = Environment.GetEnvironmentVariables().Keys.Cast<string>()
                        .Where(key => key.StartsWith("CUDA_PATH_V", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(key => key, StringComparer.OrdinalIgnoreCase);
                    foreach (string key in versioned) if (Environment.GetEnvironmentVariable(key) is string root && root.Length > 0) roots.Add((key, root));
                }
                foreach (var (variable, root) in roots)
                {
                    if (!Path.IsPathFullyQualified(root))
                    {
                        // A relative toolkit entry must not block an NVRTC found elsewhere; it is named if nothing is found.
                        ignored.Add(variable);
                        continue;
                    }
                    if (Windows) { directories.Add(Path.Combine(root, "bin", "x64")); directories.Add(Path.Combine(root, "bin")); }
                    else { directories.Add(Path.Combine(root, "lib64")); directories.Add(Path.Combine(root, "lib")); directories.Add(Path.Combine(root, "targets", "x86_64-linux", "lib")); }
                }
            }
            var result = new List<string>();
            var seen = new HashSet<string>(Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (string directory in directories)
            {
                string[] files;
                try { files = Directory.Exists(directory) ? Directory.GetFiles(directory, NvrtcGlob) : Array.Empty<string>(); }
                catch (Exception error) when (error is UnauthorizedAccessException || error is IOException) { continue; }
                foreach (string path in files.Where(p => NvrtcName.IsMatch(Path.GetFileName(p)) && File.Exists(p)).OrderByDescending(p => p, StringComparer.Ordinal))
                    if (seen.Add(ResolvedPath(path))) result.Add(path);
            }
            if (result.Count == 0)
            {
                if (explicitDirectory != null) throw new FileNotFoundException("INU_GPU_NVRTC_DIR contains no NVRTC library: " + explicitDirectory);
                throw new FileNotFoundException("NVRTC library not found in " + (runtimeDirectory == null ? "" : Path.Combine(runtimeDirectory, "nvrtc") + ", ") +
                                                "CUDA_PATH or CUDA_PATH_V*; copy the NVRTC libraries (or pip install --target that folder nvidia-cuda-nvrtc-cu12), " +
                                                "install the CUDA toolkit, or set INU_GPU_NVRTC_DIR" +
                                                (ignored.Count > 0 ? " (ignored because not absolute: " + string.Join(", ", ignored) + ")" : ""));
            }
            return result;
        }

        /// <summary>Full path with symbolic links resolved, so one library reached through several links is tried once.</summary>
        private static string ResolvedPath(string path)
        {
            try
            {
                var file = new FileInfo(path);
                string target = file.ResolveLinkTarget(true)?.FullName ?? file.FullName;
                string folder = Path.GetDirectoryName(target);
                string resolvedFolder = new DirectoryInfo(folder).ResolveLinkTarget(true)?.FullName ?? folder;
                return Path.Combine(resolvedFolder, Path.GetFileName(target));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { return Path.GetFullPath(path); }
        }

        private static string[] BuiltinsNames(int major, int minor) => Windows
            ? new[] { "nvrtc-builtins64_" + major + minor + ".dll" }
            : new[] { "libnvrtc-builtins.so." + major + "." + minor, "libnvrtc-builtins.so." + major, "libnvrtc-builtins.so" };

        /// <summary>First NVRTC whose output the driver accepts, probed with a tiny module (PTX, then CUBIN).</summary>
        private void SelectToolchain(List<string> candidates, int driverVersion)
        {
            var failures = new List<string>();
            foreach (string path in candidates)
            {
                IntPtr library = IntPtr.Zero, builtins = IntPtr.Zero;
                int major, minor;
                string builtinsPath;
                int[] supported;
                // Opening a candidate may fail (missing builtins, wrong bitness, no export); try the next one.
                try
                {
                    library = NativeLibrary.Load(path);
                    var version = (delegate* unmanaged<int*, int*, int>)Export(library, "nvrtcVersion");
                    int rc = version(&major, &minor);
                    if (rc != 0) throw new InvalidOperationException("nvrtcVersion failed: " + rc);
                    string folder = Path.GetDirectoryName(path);
                    string[] builtinsNames = BuiltinsNames(major, minor);
                    builtinsPath = builtinsNames.Select(n => Path.Combine(folder, n)).FirstOrDefault(File.Exists)
                        ?? throw new FileNotFoundException("Matching NVRTC builtins not found: " + Path.Combine(folder, builtinsNames[0]));
                    // NVRTC loads the builtins lazily by name; loading them by full path first makes that lookup succeed.
                    builtins = NativeLibrary.Load(builtinsPath);
                    supported = SupportedArchitectures(library);
                }
                catch (Exception error) when (error is DllNotFoundException || error is BadImageFormatException || error is EntryPointNotFoundException ||
                                              error is FileNotFoundException || error is InvalidOperationException)
                {
                    failures.Add(path + ": " + error.Message);
                    Release(ref library, ref builtins);
                    continue;
                }
                int target;
                if (supported == null) target = DeviceArch;
                else
                {
                    var choices = supported.Where(a => a > 0 && a <= DeviceArch).ToArray();
                    if (choices.Length == 0)
                    {
                        failures.Add(path + ": No compatible NVRTC virtual architecture for device compute_" + DeviceArch + " (CC " + DeviceArch / 10 + "." + DeviceArch % 10 +
                                     "); this NVRTC supports [" + string.Join(", ", supported) + "]; " + NvrtcRequirement(DeviceArch));
                        Release(ref library, ref builtins);
                        continue;
                    }
                    target = choices.Max();
                }
                int result;
                string kind;
                // A probe that fails for any other reason stops the search, as a real compile would fail the same way.
                try
                {
                    BindNvrtc(library);
                    (result, kind) = ProbeToolchain(target, supported);
                }
                catch
                {
                    Release(ref library, ref builtins);
                    throw;
                }
                if (RejectedImageCodes.Contains(result))
                {
                    failures.Add(path + ": NVRTC " + major + "." + minor + " output rejected by driver " + driverVersion + " (" + result + ")");
                    log("NVRTC candidate rejected: " + failures[^1]);
                    Release(ref library, ref builtins);
                    continue;
                }
                if (result != 0)
                {
                    Release(ref library, ref builtins);
                    throw new InvalidOperationException("Unexpected module probe error: " + result);
                }
                nvrtc = library; nvrtcBuiltins = builtins;
                NvrtcPath = path; NvrtcBuiltinsPath = builtinsPath; NvrtcVersion = (major, minor);
                SupportedArchs = supported; TargetArch = target; BinaryKind = kind;
                CompileOptions = OptionsFor(target, kind);
                return;
            }
            throw new InvalidOperationException("No compatible NVRTC toolchain: " + string.Join("; ", failures) + "; " + NvrtcRequirement(DeviceArch) +
                                                " and an NVIDIA driver at least as new as that NVRTC");
        }

        private static void Release(ref IntPtr library, ref IntPtr builtins)
        {
            if (library != IntPtr.Zero) { NativeLibrary.Free(library); library = IntPtr.Zero; }
            if (builtins != IntPtr.Zero) { NativeLibrary.Free(builtins); builtins = IntPtr.Zero; }
        }

        private static int[] SupportedArchitectures(IntPtr library)
        {
            if (!TryExport(library, "nvrtcGetNumSupportedArchs", out IntPtr countAddress) || !TryExport(library, "nvrtcGetSupportedArchs", out IntPtr listAddress)) return null;
            var countFn = (delegate* unmanaged<int*, int>)countAddress;
            var listFn = (delegate* unmanaged<int*, int>)listAddress;
            int count;
            int rc = countFn(&count);
            if (rc != 0 || count <= 0 || count >= 1024) throw new InvalidOperationException("nvrtcGetNumSupportedArchs failed: " + rc + ", count=" + count);
            var archs = new int[count];
            fixed (int* p = archs) rc = listFn(p);
            if (rc != 0) throw new InvalidOperationException("nvrtcGetSupportedArchs failed: " + rc);
            return archs;
        }

        private static string[] OptionsFor(int arch, string kind) =>
            new[] { "--gpu-architecture=" + (kind == "ptx" ? "compute" : "sm") + "_" + arch }.Concat(NvrtcOptions).ToArray();

        /// <summary>Load a tiny module as PTX, then as a CUBIN when the driver rejects the PTX (needs the same CC major).</summary>
        private (int, string) ProbeToolchain(int target, int[] supported)
        {
            var kinds = Environment.GetEnvironmentVariable("INU_GPU_FORCE_CUBIN") == "1" ? new[] { "cubin" } : new[] { "ptx", "cubin" };
            int result = 0;
            foreach (string kind in kinds)
            {
                if (kind == "cubin" && (nvrtcGetCUBIN == null || target / 10 != DeviceArch / 10)) break;
                byte[] image;
                try { image = CompileImage("extern \"C\" __global__ void probe() {}", OptionsFor(target, kind), kind, out _); }
                catch (InvalidOperationException error) when (supported == null && error.Message.Contains("invalid value for --gpu-architecture", StringComparison.OrdinalIgnoreCase))
                { result = 209; continue; }
                IntPtr module;
                int rc;
                fixed (byte* data = image) rc = cuModuleLoadData(&module, data);
                if (RejectedImageCodes.Contains(rc)) { result = rc; continue; }
                Check(rc, "cuModuleLoadData compatibility probe");
                Check(cuModuleUnload(module), "cuModuleUnload compatibility probe");
                return (0, kind);
            }
            return (result != 0 ? result : 209, null);
        }

        /// <summary>Compile with the selected options; returns PTX text with its trailing NUL, or CUBIN bytes.</summary>
        internal byte[] CompileImage(string source, out string compileLog) => CompileImage(source, CompileOptions, BinaryKind, out compileLog);

        private byte[] CompileImage(string source, string[] options, string kind, out string compileLog)
        {
            byte[] src = NullTerminated(source), program = NullTerminated(NvrtcProgram);
            byte[][] optionBytes = options.Select(NullTerminated).ToArray();
            IntPtr prog;
            int rc;
            fixed (byte* s = src, p = program) rc = nvrtcCreateProgram(&prog, s, p, 0, null, null);
            if (rc != 0) throw new InvalidOperationException("nvrtcCreateProgram: " + rc);
            var handles = new List<GCHandle>();
            try
            {
                byte** optionPointers = stackalloc byte*[optionBytes.Length];
                for (int i = 0; i < optionBytes.Length; i++)
                {
                    var handle = GCHandle.Alloc(optionBytes[i], GCHandleType.Pinned);
                    handles.Add(handle);
                    optionPointers[i] = (byte*)handle.AddrOfPinnedObject();
                }
                rc = nvrtcCompileProgram(prog, optionBytes.Length, optionPointers);
                nuint logSize;
                nvrtcGetProgramLogSize(prog, &logSize);
                var logBytes = new byte[Math.Max((long)logSize, 1)];
                fixed (byte* l = logBytes) nvrtcGetProgramLog(prog, l);
                int end = Array.IndexOf(logBytes, (byte)0);
                compileLog = Encoding.UTF8.GetString(logBytes, 0, end >= 0 ? end : logBytes.Length);
                if (rc != 0) throw new InvalidOperationException("nvrtcCompileProgram: " + rc + "; " + compileLog);
                var sizeFn = kind == "ptx" ? nvrtcGetPTXSize : nvrtcGetCUBINSize;
                var dataFn = kind == "ptx" ? nvrtcGetPTX : nvrtcGetCUBIN;
                nuint size;
                rc = sizeFn(prog, &size);
                if (rc != 0) throw new InvalidOperationException("nvrtcGet" + kind.ToUpperInvariant() + "Size: " + rc);
                var image = new byte[(long)size];
                fixed (byte* i = image) rc = dataFn(prog, i);
                if (rc != 0) throw new InvalidOperationException("nvrtcGet" + kind.ToUpperInvariant() + ": " + rc);
                return image;
            }
            finally
            {
                foreach (var handle in handles) handle.Free();
                nvrtcDestroyProgram(&prog);
            }
        }

        private static byte[] NullTerminated(string text)
        {
            byte[] raw = Encoding.UTF8.GetBytes(text);
            Array.Resize(ref raw, raw.Length + 1);
            return raw;
        }

        internal IntPtr LoadImage(byte[] image)
        {
            IntPtr module;
            int rc;
            fixed (byte* data = image) rc = cuModuleLoadData(&module, data);
            Check(rc, "cuModuleLoadData " + BinaryKind + " (driver must support the NVRTC output)");
            modules.Add(module);
            return module;
        }

        internal void UnloadModule(IntPtr module)
        {
            Check(cuModuleUnload(module), "cuModuleUnload");
            modules.Remove(module);
        }

        internal IntPtr Function(IntPtr module, string name)
        {
            IntPtr function;
            byte[] raw = NullTerminated(name);
            int rc;
            fixed (byte* n = raw) rc = cuModuleGetFunction(&function, module, n);
            Check(rc, "cuModuleGetFunction " + name);
            return function;
        }

        internal (ulong Address, long Bytes) Global(IntPtr module, string name)
        {
            ulong address;
            nuint bytes;
            byte[] raw = NullTerminated(name);
            int rc;
            fixed (byte* n = raw) rc = cuModuleGetGlobal(&address, &bytes, module, n);
            Check(rc, "cuModuleGetGlobal");
            return (address, (long)bytes);
        }

        internal (int MaxThreadsPerBlock, int LocalSizeBytes, int Registers) FunctionAttributes(IntPtr function)
        {
            int threads, local, registers;
            Check(cuFuncGetAttribute(&threads, 0, function), "cuFuncGetAttribute max_threads_per_block");
            Check(cuFuncGetAttribute(&local, 3, function), "cuFuncGetAttribute local_size_bytes");
            Check(cuFuncGetAttribute(&registers, 4, function), "cuFuncGetAttribute num_regs");
            if (threads < 1 || local < 0) throw new InvalidOperationException("Invalid kernel resource attributes");
            return (threads, local, registers);
        }

        internal (long Free, long Total) MemoryInfo()
        {
            nuint free, total;
            Check(cuMemGetInfo(&free, &total), "cuMemGetInfo");
            return ((long)free, (long)total);
        }

        internal ulong Buffer(long size)
        {
            ulong pointer;
            Check(cuMemAlloc(&pointer, (nuint)size), "cuMemAlloc");
            buffers.Add(pointer);
            return pointer;
        }

        internal void ReleaseBuffer(ulong pointer)
        {
            Check(cuMemFree(pointer), "cuMemFree");
            buffers.Remove(pointer);
        }

        /// <summary>Page-locked host memory mapped into the device address space; returns (host, device) pointers.</summary>
        internal (IntPtr Host, ulong Device) MappedBuffer(long size)
        {
            // Same code as a failed allocation, so the memory manager treats it like one.
            if (!CanMapHostMemory) throw new GpuCudaException(ErrorOutOfMemory, "cuMemHostAlloc mapped (device cannot map host memory)", "CUDA_ERROR_OUT_OF_MEMORY");
            void* host;
            Check(cuMemHostAlloc(&host, (nuint)size, 2), "cuMemHostAlloc mapped");
            ulong device;
            int rc = cuMemHostGetDevicePointer(&device, host, 0);
            if (rc != 0) { cuMemFreeHost(host); Check(rc, "cuMemHostGetDevicePointer"); }
            hostBuffers.Add((IntPtr)host);
            return ((IntPtr)host, device);
        }

        internal void ReleaseMappedBuffer(IntPtr host)
        {
            Check(cuMemFreeHost((void*)host), "cuMemFreeHost");
            hostBuffers.Remove(host);
        }

        internal void MemsetD8(ulong pointer, long size) => Check(cuMemsetD8(pointer, 0, (nuint)size), "cuMemsetD8");
        internal void CopyToDevice(ulong destination, void* source, long size, string operation) => Check(cuMemcpyHtoD(destination, source, (nuint)size), operation);
        internal void CopyToHost(void* destination, ulong source, long size, string operation) => Check(cuMemcpyDtoH(destination, source, (nuint)size), operation);
        internal void Synchronize(string operation) => Check(cuCtxSynchronize(), operation);

        internal void Launch(IntPtr function, uint grid, uint block, void** arguments, string operation) =>
            Check(cuLaunchKernel(function, grid, 1, 1, block, 1, 1, 0, IntPtr.Zero, arguments, null), operation);

        internal IntPtr CreateEvent()
        {
            IntPtr e;
            Check(cuEventCreate(&e, 0), "cuEventCreate");
            events.Add(e);
            return e;
        }

        internal void RecordEvent(IntPtr e, string operation) => Check(cuEventRecord(e, IntPtr.Zero), operation);
        internal void SynchronizeEvent(IntPtr e) => Check(cuEventSynchronize(e), "cuEventSynchronize");

        internal float ElapsedMs(IntPtr start, IntPtr end)
        {
            float ms;
            Check(cuEventElapsedTime(&ms, start, end), "cuEventElapsedTime");
            return ms;
        }

        public void Dispose()
        {
            if (driver == IntPtr.Zero) return;
            foreach (IntPtr e in events) cuEventDestroy(e);
            events.Clear();
            foreach (ulong b in buffers) cuMemFree(b);
            buffers.Clear();
            foreach (IntPtr h in hostBuffers) cuMemFreeHost((void*)h);
            hostBuffers.Clear();
            foreach (IntPtr m in modules) cuModuleUnload(m);
            modules.Clear();
            if (context != IntPtr.Zero) { cuCtxDestroy(context); context = IntPtr.Zero; }
            Release(ref nvrtc, ref nvrtcBuiltins);
            NativeLibrary.Free(driver);
            driver = IntPtr.Zero;
        }
    }
}
