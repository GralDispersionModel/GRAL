using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private const int AttrKernelExecTimeout = 17, AttrIntegrated = 18, AttrCanMapHostMemory = 19, AttrComputeMode = 20, AttrTccDriver = 35,
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
        internal Dictionary<string, object> DeviceInfo { get; }
        // GRAL --gpu-check only: list every device of the started backend in DeviceInfo["devices"]. A run does not list.
        private readonly bool listDevices;
        /// <summary>True when the AMD HIP runtime and hiprtc stand in for the CUDA driver and NVRTC.</summary>
        internal bool Hip { get; private set; }
        /// <summary>AMD GPU architecture name (for example gfx1201); null on the CUDA backend.</summary>
        internal string ArchName { get; private set; }
        internal string TargetText => Hip ? ArchName : (BinaryKind == "ptx" ? "compute_" : "sm_") + TargetArch;

        // True only when the NVIDIA driver library is missing or reports no device. Any other CUDA failure is reported,
        // never bypassed, so a broken NVIDIA driver does not silently move the run to another GPU.
        private bool cudaAbsent;
        private const int ErrorNoDevice = 100; // CUDA_ERROR_NO_DEVICE and hipErrorNoDevice

        /// <param name="checkReport">GRAL --gpu-check passes its report: the device information is written into it step by step,
        /// so that what was found is still there when a later step throws, and it also gets the list of all devices.</param>
        internal GpuCuda(string runtimeDirectory, Action<string> log, Dictionary<string, object> checkReport = null)
        {
            this.log = log;
            DeviceInfo = checkReport ?? new Dictionary<string, object>();
            listDevices = checkReport != null;
            try
            {
                DeviceInfo["warnings"] = Warnings;
                string backend = BackendValue(GpuSettings.Value("backend"), GpuSettings.Label("backend"));
                if (backend == null) InitializeAutomatic(runtimeDirectory);
                else if (backend == "cuda") Initialize(runtimeDirectory);
                else InitializeHip(runtimeDirectory, automatic: false);
            }
            catch { Dispose(); throw; }
        }

        /// <summary>Startup warnings that do not stop the run (for example a device of the settings file that is no longer there).</summary>
        internal List<string> Warnings { get; } = new List<string>();

        private void Warn(string message)
        {
            Warnings.Add(message);
            log("Warning: " + message);
        }

        /// <summary>cuda, hip, or null for the automatic choice (INU_GPU_BACKEND or the backend key of the settings file; case does not matter).</summary>
        internal static string BackendValue(string raw, string label)
        {
            string backend = raw?.Trim();
            if (string.IsNullOrEmpty(backend)) return null;
            if (backend.Equals("cuda", StringComparison.OrdinalIgnoreCase)) return "cuda";
            if (backend.Equals("hip", StringComparison.OrdinalIgnoreCase)) return "hip";
            throw new ArgumentException(label + " must be cuda or hip, got '" + backend + "'");
        }

        /// <summary>Syntax of a device ordinal (INU_GPU_DEVICE or the device key); empty means the default device.</summary>
        internal static System.Numerics.BigInteger OrdinalValue(string raw, string label)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            if (raw.All(ch => ch >= '0' && ch <= '9')) return System.Numerics.BigInteger.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
            throw new ArgumentException(label + " must be a nonnegative integer, got '" + raw + "'");
        }

        /// <summary>A compiler folder setting (INU_GPU_NVRTC_DIR, INU_GPU_HIPRTC_DIR or their keys) must be an absolute path.</summary>
        internal static void DirectoryValue(string raw, string label)
        {
            // A GPU.txt copied from the other operating system has a path that is not absolute here; say which form is expected
            if (raw.Length == 0 || !Path.IsPathFullyQualified(raw))
                throw new ArgumentException(label + " must be a nonempty absolute path" +
                                            (raw.Length == 0 ? "" : " of this system (" + (Windows ? @"with a drive letter, such as C:\folder" : "starting with /") + "), got '" + raw + "'"));
        }

        /// <summary>
        /// The device setting to use: INU_GPU_DEVICE as given. A device of the settings file that comes with a device_name is used
        /// only while the device at that ordinal still has that name; after a card swap or a renumbering the entry is ignored with a
        /// warning and the device is chosen automatically. Null or empty: automatic choice.
        /// </summary>
        private string DeviceSetting(int count)
        {
            var (raw, source) = GpuSettings.Get("device");
            string expected = GpuSettings.Value("device_name");
            // No device at all is reported as such by the caller (and lets the automatic choice go on to HIP); it is not a name mismatch
            if (source != "file" || string.IsNullOrEmpty(expected) || count < 1) return raw;
            string found = null;
            int handle;
            if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal) && ordinal < count && cuDeviceGet(&handle, ordinal) == 0)
            {
                byte* nameBuffer = stackalloc byte[256];
                if (cuDeviceGetName(nameBuffer, 256, handle) == 0) found = Marshal.PtrToStringUTF8((IntPtr)nameBuffer);
            }
            if (found != null && found.Trim() == expected) return raw;
            Warn("The GPU settings file " + GpuSettings.FileName + " selects device " + raw + " (" + expected + "), but " +
                 (found == null ? "there is no such device now" : "device " + raw + " is now " + found.Trim()) + "; the device is chosen automatically");
            return null;
        }

        /// <summary>
        /// Default backend choice: an NVIDIA device runs on CUDA. The AMD HIP runtime is tried only when the NVIDIA driver
        /// is missing or reports no device; any other CUDA problem is reported as before.
        /// </summary>
        private void InitializeAutomatic(string runtimeDirectory)
        {
            try { Initialize(runtimeDirectory); }
            catch (Exception cudaError) when (cudaAbsent && HipRuntimePresent())
            {
                // NVIDIA does not support unloading the driver after cuInit, so a loaded nvcuda stays loaded for the process.
                driver = IntPtr.Zero;
                log("No NVIDIA device (" + cudaError.Message + "); trying the AMD HIP runtime");
                try { InitializeHip(runtimeDirectory, automatic: true); }
                catch (Exception hipError)
                {
                    throw new InvalidOperationException("No usable GPU. CUDA: " + cudaError.Message + "; HIP: " + hipError.Message, hipError);
                }
            }
        }

        /// <summary>Marks a startup error with a short status for <c>GRAL --gpu-check</c> (no_driver, no_device, no_nvrtc, ...).</summary>
        internal static T Status<T>(T error, string status) where T : Exception
        {
            error.Data["GpuStatus"] = status;
            return error;
        }

        /// <summary>
        /// For GRAL --gpu-check: name, memory and integrated flag of every device of the started backend. Only driver queries that
        /// need no context; the device choice does not depend on it.
        /// </summary>
        private void ListDevices(int count)
        {
            if (!listDevices) return;
            var devices = new List<Dictionary<string, object>>();
            byte* nameBuffer = stackalloc byte[256];
            for (int i = 0; i < count; i++)
            {
                int handle;
                nuint total;
                if (cuDeviceGet(&handle, i) != 0) continue;
                string name = cuDeviceGetName(nameBuffer, 256, handle) == 0 ? Marshal.PtrToStringUTF8((IntPtr)nameBuffer)?.Trim() : null;
                // Only the integrated attribute may be null (the driver does not answer); a name or a size the driver does not give is "" or 0
                devices.Add(new Dictionary<string, object>
                {
                    ["backend"] = Hip ? "hip" : "cuda", ["ordinal"] = i, ["name"] = name ?? "",
                    ["total_memory_bytes"] = cuDeviceTotalMem(&total, handle) == 0 ? (long)total : 0L,
                    ["integrated"] = OptionalAttribute(Hip ? HipAttrIntegrated : AttrIntegrated, handle) is int integrated ? (object)(integrated != 0) : null,
                    ["selected"] = false
                });
            }
            DeviceInfo["devices"] = devices;
        }

        private void MarkSelectedDevice()
        {
            if (DeviceInfo.TryGetValue("devices", out object list))
                foreach (var device in (List<Dictionary<string, object>>)list) device["selected"] = (int)device["ordinal"] == DeviceOrdinal;
        }

        /// <summary>The NVIDIA driver library is installed (file test only, Windows).</summary>
        internal static bool CudaDriverPresent() => Windows && File.Exists(Path.Combine(Environment.SystemDirectory, DriverLibrary));

        /// <summary>Per-user folder for a runtime compiler (nvrtc or hiprtc) that GRAL itself never writes; searched last.</summary>
        internal static string UserToolchainFolder(string name)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return root.Length > 0 && Path.IsPathFullyQualified(root) ? Path.Combine(root, "GRAL", "GpuToolchain", name) : null;
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

        private static int DeviceOrdinalSetting(string raw, int count, bool hip = false)
        {
            string api = hip ? "HIP" : "CUDA", visible = hip ? "HIP_VISIBLE_DEVICES" : "CUDA_VISIBLE_DEVICES";
            // A null raw value comes from DeviceSetting after it ignored the file entry, so the label is taken only for a real value
            string label = string.IsNullOrEmpty(raw) ? "INU_GPU_DEVICE" : GpuSettings.Label("device");
            System.Numerics.BigInteger ordinal = OrdinalValue(raw, label);
            if (count < 1) throw Status(new InvalidOperationException("No " + api + " device is visible (check the " + (hip ? "AMD" : "NVIDIA") + " driver and " + visible + ")"), "no_device");
            if (ordinal >= count)
                throw GpuSettings.FromFile(new ArgumentException("Device " + ordinal + " (" + label + ") is out of range: " + count + " " + api + " device(s) visible (valid 0.." + (count - 1) + "; " + visible + " renumbers devices)"), "device");
            return (int)ordinal;
        }

        private void Initialize(string runtimeDirectory)
        {
            DeviceInfo["backend"] = "cuda";
            // On Windows the driver comes from System32 only, never from the project folder or PATH.
            try { driver = Windows ? NativeLibrary.Load(DriverLibrary, typeof(GpuCuda).Assembly, DllImportSearchPath.System32) : NativeLibrary.Load(DriverLibrary); }
            catch (Exception error) when (error is DllNotFoundException || error is BadImageFormatException)
            {
                // A driver library that exists but does not load is a broken driver, not a missing one.
                cudaAbsent = !Windows || !File.Exists(Path.Combine(Environment.SystemDirectory, DriverLibrary));
                throw Status(new InvalidOperationException("NVIDIA driver library " + DriverLibrary + " could not be loaded (" + error.Message + "); install the NVIDIA display driver", error),
                             cudaAbsent ? "no_driver" : "driver_error");
            }
            BindDriver();
            var clock = Stopwatch.StartNew();
            int initResult = cuInit(0);
            cudaAbsent = initResult == ErrorNoDevice;
            if (cudaAbsent) throw Status(new InvalidOperationException("The NVIDIA driver reports no CUDA device (check CUDA_VISIBLE_DEVICES)"), "no_device");
            Check(initResult, "cuInit");
            int driverVersion, count, device;
            Check(cuDriverGetVersion(&driverVersion), "cuDriverGetVersion");
            Check(cuDeviceGetCount(&count), "cuDeviceGetCount");
            cudaAbsent = count < 1;
            ListDevices(count);
            DeviceOrdinal = DeviceOrdinalSetting(DeviceSetting(count), count);
            MarkSelectedDevice();
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
            var candidates = NvrtcCandidates(GpuSettings.Value("nvrtc_dir"), runtimeDirectory);
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

        // ------------------------------------------------------------------ AMD HIP backend
        // hipDeviceAttribute_t values of HIP 7.x (hip_runtime_api.h); they differ from the CUDA attribute numbers above.
        private const int HipAttrCanMapHostMemory = 3, HipAttrComputeMode = 6, HipAttrIntegrated = 16, HipAttrKernelExecTimeout = 18,
                          HipAttrCcMajor = 23, HipAttrCcMinor = 61, HipAttrSingleToDoublePerfRatio = 77, HipAttrTccDriver = 80;
        // Only the HIP 7.x runtime is bound: the attribute numbers above were checked against the HIP 7.x header.
        private static readonly string[] HipRuntimeNames = { "amdhip64_7.dll" };
        private static readonly Regex HiprtcName = new Regex(@"^hiprtc[0-9]{4}\.dll$", RegexOptions.IgnoreCase);
        private static readonly Regex GfxName = new Regex(@"^gfx[0-9a-f]+$");
        /// <summary>AMD architectures whose GPU output was compared with the CPU output; others get a startup warning.</summary>
        internal static readonly string[] CheckedHipArchs = { "gfx1201" };
        // hiprtc equivalents of NvrtcOptions: no FMA contraction, IEEE fp32 division and square root. The kernel is C++11,
        // but the hiprtc runtime header that hiprtc always includes needs C++17.
        // GCNPreRAOptimizations miscompiles gral_fresh for gfx1201 with ROCm 10.1 hiprtc: with it, B00 situation 1 differs from
        // the CPU and CUDA outputs by NMAE 1.40 (seed noise is 0.11); without it, by 0.003 (-mllvm -opt-bisect-limit, 2026-11-07).
        // Without a launch bound the AMDGPU back end must allow 1024-thread blocks, which caps both kernels at 192 VGPRs and
        // spills gral_fresh heavily (560 B scratch per lane). GpuSituationDriver launches 128 or 256 threads, so a 256 bound
        // gives the full 256 VGPRs. The raised inline threshold inlines Zeitschleife_IntWindCalculate into gral_residual and on
        // the wet-deposition path of gral_fresh; the per-step call in gral_fresh stays a call because the AMDGPU inliner caps the
        // basic blocks of the result (amdgpu-inline-max-bb), and raising that cap was not faster on B00 (more spills).
        // Both options change code generation only; the GRZ output stays byte-identical (2026-11-07).
        internal static readonly string[] HiprtcOptions = { "-std=c++17", "-ffp-contract=off", "-fhip-fp32-correctly-rounded-divide-sqrt",
                                                            "-mllvm", "-amdgpu-enable-pre-ra-optimizations=0", "--gpu-max-threads-per-block=256",
                                                            "-mllvm", "-inline-threshold=50000" };
        private readonly List<IntPtr> hiprtcComgr = new List<IntPtr>();
        /// <summary>Code-object manager libraries loaded next to hiprtc; they generate the GPU code, so they enter the module cache key.</summary>
        internal string[] ComgrPaths { get; private set; } = Array.Empty<string>();

        internal static bool HipRuntimePresent()
        {
            if (!Windows) return false;
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            // A set INU_GPU_HIP_RUNTIME counts as present, so a wrong value is reported by InitializeHip instead of being ignored.
            return Environment.GetEnvironmentVariable("INU_GPU_HIP_RUNTIME") is string explicitPath && explicitPath.Length > 0
                || HipRuntimeNames.Any(n => File.Exists(Path.Combine(system, n)));
        }

        private void BindHip()
        {
            IntPtr d = driver;
            cuInit = (delegate* unmanaged<uint, int>)Export(d, "hipInit");
            cuDriverGetVersion = (delegate* unmanaged<int*, int>)Export(d, "hipDriverGetVersion");
            cuDeviceGetCount = (delegate* unmanaged<int*, int>)Export(d, "hipGetDeviceCount");
            cuDeviceGet = (delegate* unmanaged<int*, int, int>)Export(d, "hipDeviceGet");
            cuDeviceGetName = (delegate* unmanaged<byte*, int, int, int>)Export(d, "hipDeviceGetName");
            cuDeviceTotalMem = (delegate* unmanaged<nuint*, int, int>)Export(d, "hipDeviceTotalMem");
            cuDeviceGetAttribute = (delegate* unmanaged<int*, int, int, int>)Export(d, "hipDeviceGetAttribute");
            cuCtxCreate = (delegate* unmanaged<IntPtr*, uint, int, int>)Export(d, "hipCtxCreate");
            cuCtxDestroy = (delegate* unmanaged<IntPtr, int>)Export(d, "hipCtxDestroy");
            cuModuleLoadData = (delegate* unmanaged<IntPtr*, void*, int>)Export(d, "hipModuleLoadData");
            cuModuleUnload = (delegate* unmanaged<IntPtr, int>)Export(d, "hipModuleUnload");
            cuModuleGetFunction = (delegate* unmanaged<IntPtr*, IntPtr, byte*, int>)Export(d, "hipModuleGetFunction");
            cuModuleGetGlobal = (delegate* unmanaged<ulong*, nuint*, IntPtr, byte*, int>)Export(d, "hipModuleGetGlobal");
            cuFuncGetAttribute = (delegate* unmanaged<int*, int, IntPtr, int>)Export(d, "hipFuncGetAttribute");
            cuMemAlloc = (delegate* unmanaged<ulong*, nuint, int>)Export(d, "hipMalloc");
            cuMemFree = (delegate* unmanaged<ulong, int>)Export(d, "hipFree");
            cuMemGetInfo = (delegate* unmanaged<nuint*, nuint*, int>)Export(d, "hipMemGetInfo");
            cuMemHostAlloc = (delegate* unmanaged<void**, nuint, uint, int>)Export(d, "hipHostMalloc");
            cuMemFreeHost = (delegate* unmanaged<void*, int>)Export(d, "hipHostFree");
            cuMemHostGetDevicePointer = (delegate* unmanaged<ulong*, void*, uint, int>)Export(d, "hipHostGetDevicePointer");
            cuMemsetD8 = (delegate* unmanaged<ulong, byte, nuint, int>)Export(d, "hipMemsetD8");
            cuMemcpyHtoD = (delegate* unmanaged<ulong, void*, nuint, int>)Export(d, "hipMemcpyHtoD");
            cuMemcpyDtoH = (delegate* unmanaged<void*, ulong, nuint, int>)Export(d, "hipMemcpyDtoH");
            // hipCtxSynchronize returns hipErrorNotSupported on Windows; the context is the only one on this thread.
            cuCtxSynchronize = (delegate* unmanaged<int>)Export(d, "hipDeviceSynchronize");
            cuLaunchKernel = (delegate* unmanaged<IntPtr, uint, uint, uint, uint, uint, uint, uint, IntPtr, void**, void**, int>)Export(d, "hipModuleLaunchKernel");
            cuEventCreate = (delegate* unmanaged<IntPtr*, uint, int>)Export(d, "hipEventCreateWithFlags");
            cuEventRecord = (delegate* unmanaged<IntPtr, IntPtr, int>)Export(d, "hipEventRecord");
            cuEventSynchronize = (delegate* unmanaged<IntPtr, int>)Export(d, "hipEventSynchronize");
            cuEventElapsedTime = (delegate* unmanaged<float*, IntPtr, IntPtr, int>)Export(d, "hipEventElapsedTime");
            cuEventDestroy = (delegate* unmanaged<IntPtr, int>)Export(d, "hipEventDestroy");
            cuGetErrorName = (delegate* unmanaged<int, byte**, int>)Export(d, "hipDrvGetErrorName");
        }

        private void BindHiprtc(IntPtr library)
        {
            nvrtcCreateProgram = (delegate* unmanaged<IntPtr*, byte*, byte*, int, byte**, byte**, int>)Export(library, "hiprtcCreateProgram");
            nvrtcCompileProgram = (delegate* unmanaged<IntPtr, int, byte**, int>)Export(library, "hiprtcCompileProgram");
            nvrtcGetProgramLogSize = (delegate* unmanaged<IntPtr, nuint*, int>)Export(library, "hiprtcGetProgramLogSize");
            nvrtcGetProgramLog = (delegate* unmanaged<IntPtr, byte*, int>)Export(library, "hiprtcGetProgramLog");
            nvrtcGetPTXSize = null; nvrtcGetPTX = null;
            // A code object (ELF) takes the CUBIN slot of CompileImage.
            nvrtcGetCUBINSize = (delegate* unmanaged<IntPtr, nuint*, int>)Export(library, "hiprtcGetCodeSize");
            nvrtcGetCUBIN = (delegate* unmanaged<IntPtr, byte*, int>)Export(library, "hiprtcGetCode");
            nvrtcDestroyProgram = (delegate* unmanaged<IntPtr*, int>)Export(library, "hiprtcDestroyProgram");
        }

        private int? OptionalAttribute(int code, int device)
        {
            int value;
            return cuDeviceGetAttribute(&value, code, device) == 0 ? value : null;
        }

        private static string HipVersionText(int version) => (version / 10000000) + "." + (version / 100000 % 100) + "." + (version % 100000);

        /// <summary>gcnArchName of hipDeviceProp_tR0600 (char[256] at byte 1160 of the 1472-byte structure in the HIP 7.x header).</summary>
        private string HipArchName(int ordinal)
        {
            const int Offset = 1160, Length = 256;
            if (!TryExport(driver, "hipGetDevicePropertiesR0600", out IntPtr address)) throw new EntryPointNotFoundException("hipGetDevicePropertiesR0600");
            var properties = (delegate* unmanaged<byte*, int, int>)address;
            // Larger than the structure, so a runtime that writes a few more bytes cannot overrun the buffer.
            var buffer = new byte[4096];
            fixed (byte* b = buffer) Check(properties(b, ordinal), "hipGetDevicePropertiesR0600");
            int end = Array.IndexOf(buffer, (byte)0, Offset, Length);
            // Feature suffixes such as ":sramecc+:xnack-" are not part of the offload architecture.
            string text = Encoding.ASCII.GetString(buffer, Offset, (end < 0 ? Offset + Length : end) - Offset).Split(':')[0];
            if (!GfxName.IsMatch(text)) throw new InvalidOperationException("HIP device " + ordinal + " reports no gfx architecture name ('" + text + "')");
            return text;
        }

        /// <summary>Default HIP device: the first discrete GPU, so an integrated Radeon does not take the run by accident.</summary>
        private int DefaultHipOrdinal(int count, bool automatic)
        {
            for (int i = 0; i < count; i++)
            {
                int handle;
                if (cuDeviceGet(&handle, i) == 0 && OptionalAttribute(HipAttrIntegrated, handle) != 1) return i;
            }
            // allow_integrated = 1 in the settings file accepts the integrated GPU. The variable INU_GPU_BACKEND=hip accepts it too, as
            // before; backend = hip in the settings file does not: a front end writes it for "HIP, automatic device", and that choice
            // must not put the run on an integrated GPU without the question.
            if ((automatic || GpuSettings.Get("backend").Source == "file") && GpuSettings.Value("allow_integrated") != "1")
                throw Status(new InvalidOperationException("Only integrated AMD GPUs are visible; set allow_integrated = 1 in GPU.txt, or INU_GPU_BACKEND=hip (and INU_GPU_DEVICE), to run on one"), "only_integrated");
            return 0;
        }

        private void InitializeHip(string runtimeDirectory, bool automatic)
        {
            // The HIP path was built and tested on Windows only (AMD Adrenalin driver runtime + hiprtc).
            // The message names the setting that asked for HIP: the backend key of the settings file or the variable
            if (!Windows)
                throw Status(new PlatformNotSupportedException("The HIP backend is implemented for Windows only; " +
                                                               (GpuSettings.Get("backend").Source == "file" ? "set backend = cuda in " + GpuSettings.FileName + " (or remove the entry)"
                                                                                                             : "set INU_GPU_BACKEND=cuda (or unset it)") +
                                                               ", or use the CPU path"), "unsupported_platform");
            Hip = true;
            DeviceInfo["backend"] = "hip";
            string explicitRuntime = Environment.GetEnvironmentVariable("INU_GPU_HIP_RUNTIME");
            string loaded = null;
            if (explicitRuntime != null && explicitRuntime.Length > 0)
            {
                if (!Path.IsPathFullyQualified(explicitRuntime)) throw new ArgumentException("INU_GPU_HIP_RUNTIME must be an absolute path");
                if (!File.Exists(explicitRuntime)) throw Status(new FileNotFoundException("INU_GPU_HIP_RUNTIME points to a missing file: " + explicitRuntime), "invalid_setting");
                driver = NativeLibrary.Load(explicitRuntime);
                loaded = explicitRuntime;
            }
            else
            {
                // As for nvcuda.dll: the AMD display driver installs the HIP runtime in System32.
                foreach (string runtimeName in HipRuntimeNames)
                {
                    try { driver = NativeLibrary.Load(runtimeName, typeof(GpuCuda).Assembly, DllImportSearchPath.System32); loaded = runtimeName; break; }
                    catch (Exception error) when (error is DllNotFoundException || error is BadImageFormatException) { }
                }
                if (driver == IntPtr.Zero) throw Status(new InvalidOperationException("AMD HIP runtime (" + string.Join(", ", HipRuntimeNames) + ") not found in System32; install the AMD Adrenalin driver"), "no_driver");
            }
            BindHip();
            var clock = Stopwatch.StartNew();
            // hipInit answers hipErrorNoDevice (100) when the HIP runtime is installed but no AMD GPU is visible.
            int initResult = cuInit(0);
            if (initResult == ErrorNoDevice) throw Status(new InvalidOperationException("The AMD HIP runtime reports no device (check the AMD driver and HIP_VISIBLE_DEVICES)"), "no_device");
            Check(initResult, "hipInit");
            int driverVersion, count, device;
            Check(cuDriverGetVersion(&driverVersion), "hipDriverGetVersion");
            // hipGetDeviceCount answers hipErrorNoDevice (100) instead of a zero count when no AMD GPU is visible.
            int countResult = cuDeviceGetCount(&count);
            if (countResult == ErrorNoDevice) count = 0;
            else Check(countResult, "hipGetDeviceCount");
            ListDevices(count);
            string rawOrdinal = DeviceSetting(count);
            DeviceOrdinal = string.IsNullOrEmpty(rawOrdinal) && count > 0 ? DefaultHipOrdinal(count, automatic) : DeviceOrdinalSetting(rawOrdinal, count, hip: true);
            MarkSelectedDevice();
            Check(cuDeviceGet(&device, DeviceOrdinal), "hipDeviceGet");
            byte* nameBuffer = stackalloc byte[256];
            Check(cuDeviceGetName(nameBuffer, 256, device), "hipDeviceGetName");
            string name = Marshal.PtrToStringUTF8((IntPtr)nameBuffer) ?? "?";
            nuint total;
            Check(cuDeviceTotalMem(&total, device), "hipDeviceTotalMem");
            ArchName = HipArchName(DeviceOrdinal);
            int ccMajor = Attribute(HipAttrCcMajor, device, "compute capability major");
            int ccMinor = Attribute(HipAttrCcMinor, device, "compute capability minor");
            DeviceArch = 0;
            CanMapHostMemory = Attribute(HipAttrCanMapHostMemory, device, "canMapHostMemory") != 0;
            int computeMode = Attribute(HipAttrComputeMode, device, "compute mode");
            KernelExecTimeout = Attribute(HipAttrKernelExecTimeout, device, "kernel exec timeout") != 0;
            string modeText = computeMode switch { 0 => "default", 2 => "prohibited", 3 => "exclusive_process", _ => computeMode.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            DeviceInfo["ordinal"] = DeviceOrdinal;
            DeviceInfo["device_count"] = count;
            DeviceInfo["name"] = name;
            DeviceInfo["arch"] = ArchName;
            DeviceInfo["compute_capability"] = ccMajor + "." + ccMinor;
            DeviceInfo["total_memory_bytes"] = (long)total;
            DeviceInfo["driver_version"] = driverVersion;
            // The key keeps its CUDA name so existing log readers work; the value is the HIP runtime version.
            DeviceInfo["driver_cuda"] = "HIP " + HipVersionText(driverVersion);
            DeviceInfo["hip_runtime_library"] = loaded;
            DeviceInfo["compute_mode"] = modeText;
            DeviceInfo["kernel_exec_timeout"] = KernelExecTimeout;
            // NVIDIA-only attributes: the AMD runtime answers hipErrorInvalidValue, so they are reported as absent.
            DeviceInfo["tcc_driver"] = OptionalAttribute(HipAttrTccDriver, device) is int tcc && tcc != 0;
            DeviceInfo["fp32_to_fp64_perf_ratio"] = OptionalAttribute(HipAttrSingleToDoublePerfRatio, device);
            DeviceInfo["can_map_host_memory"] = CanMapHostMemory;
            DeviceInfo["platform"] = Windows ? "win32" : OperatingSystem.IsLinux() ? "linux" : RuntimeInformation.OSDescription;
            DeviceInfo["arch_checked"] = CheckedHipArchs.Contains(ArchName);
            if (computeMode == 2)
                throw new InvalidOperationException("HIP device " + DeviceOrdinal + " (" + name + ") is in prohibited compute mode; select another device with INU_GPU_DEVICE");
            // hipCtxCreate makes the context current; hipSetDevice keeps the runtime's per-thread device in step on multi-GPU machines.
            if (TryExport(driver, "hipSetDevice", out IntPtr setDevice)) Check(((delegate* unmanaged<int, int>)setDevice)(DeviceOrdinal), "hipSetDevice");
            IntPtr ctx;
            // 8 = hipDeviceMapHost, the same value as CU_CTX_MAP_HOST.
            Check(cuCtxCreate(&ctx, 8, device), "hipCtxCreate");
            context = ctx;
            SelectHiprtc(HiprtcCandidates(GpuSettings.Value("hiprtc_dir"), runtimeDirectory));
            DeviceInfo["nvrtc_version"] = "hiprtc " + NvrtcVersion.Major + "." + NvrtcVersion.Minor;
            DeviceInfo["nvrtc_path"] = NvrtcPath;
            DeviceInfo["hiprtc_comgr"] = ComgrPaths;
            DeviceInfo["target_arch"] = ArchName;
            DeviceInfo["binary_kind"] = BinaryKind;
            log("HIP device " + DeviceOrdinal + " " + ArchName + "; hiprtc " + NvrtcVersion.Major + "." + NvrtcVersion.Minor + " (" + NvrtcPath + "); " + name + "; " +
                ((long)total / (1024 * 1024)) + " MiB; HIP runtime " + HipVersionText(driverVersion) + " (" + loaded + "); kernel_exec_timeout=" + (KernelExecTimeout ? 1 : 0) +
                "; fp32:fp64=" + (DeviceInfo["fp32_to_fp64_perf_ratio"] is int ratio ? ratio + ":1" : "n/a") + "; compute_mode=" + modeText);
            if (!CheckedHipArchs.Contains(ArchName))
                log("Warning: " + ArchName + " has not been compared with the CPU path; only " + string.Join(", ", CheckedHipArchs) + " has");
            ContextMs = clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>hiprtc libraries to try: INU_GPU_HIPRTC_DIR alone when set; otherwise the runtime folder's <c>hiprtc</c> subfolder, then HIP_PATH, then %LOCALAPPDATA%\GRAL\GpuToolchain\hiprtc.</summary>
        internal static List<string> HiprtcCandidates(string explicitDirectory, string runtimeDirectory)
        {
            var directories = new List<string>();
            if (explicitDirectory != null)
            {
                DirectoryValue(explicitDirectory, GpuSettings.Label("hiprtc_dir"));
                directories.Add(explicitDirectory);
            }
            else
            {
                if (runtimeDirectory != null) directories.Add(Path.Combine(runtimeDirectory, "hiprtc"));
                if (Environment.GetEnvironmentVariable("HIP_PATH") is string root && root.Length > 0 && Path.IsPathFullyQualified(root)) directories.Add(Path.Combine(root, "bin"));
                if (UserToolchainFolder("hiprtc") is string user) directories.Add(user);
            }
            var result = new List<string>();
            foreach (string directory in directories)
            {
                string[] files;
                try { files = Directory.Exists(directory) ? Directory.GetFiles(directory, "hiprtc*.dll") : Array.Empty<string>(); }
                catch (Exception error) when (error is UnauthorizedAccessException || error is IOException) { continue; }
                result.AddRange(files.Where(p => HiprtcName.IsMatch(Path.GetFileName(p))).OrderByDescending(p => p, StringComparer.Ordinal));
            }
            if (result.Count == 0)
                throw GpuSettings.FromFile(Status(new FileNotFoundException("hiprtc library not found in " + string.Join(", ", directories) + "; install the HIP SDK or the ROCm Python " +
                                                       "packages and set INU_GPU_HIPRTC_DIR to the folder that holds hiprtcNNNN.dll"), explicitDirectory == null ? "no_hiprtc" : "invalid_setting"), "hiprtc_dir");
            return result;
        }

        /// <summary>First hiprtc whose code object the HIP runtime accepts, probed with a tiny module.</summary>
        private void SelectHiprtc(List<string> candidates)
        {
            var failures = new List<string>();
            foreach (string path in candidates)
            {
                IntPtr library = IntPtr.Zero, builtins = IntPtr.Zero;
                var comgr = new List<IntPtr>();
                void ReleaseCandidate()
                {
                    Release(ref library, ref builtins);
                    foreach (IntPtr handle in comgr) NativeLibrary.Free(handle);
                    comgr.Clear();
                }
                int major, minor;
                string folder = Path.GetDirectoryName(path);
                string builtinsPath = Path.Combine(folder, "hiprtc-builtins" + Path.GetFileName(path).Substring("hiprtc".Length));
                string[] comgrPaths = Array.Empty<string>();
                try
                {
                    // hiprtc needs the code-object manager (amd_comgr.dll, or amd_comgr_N.dll in the HIP SDK) from its own folder.
                    comgrPaths = Directory.GetFiles(folder, "amd_comgr*.dll").OrderBy(p => p, StringComparer.Ordinal).ToArray();
                    if (comgrPaths.Length == 0) throw new FileNotFoundException("amd_comgr*.dll not found next to " + path);
                    if (!File.Exists(builtinsPath)) throw new FileNotFoundException("Matching hiprtc builtins not found: " + builtinsPath);
                    // hiprtc finds comgr and its builtins by name; loading them by full path first makes that lookup use this folder.
                    foreach (string comgrPath in comgrPaths) comgr.Add(NativeLibrary.Load(comgrPath));
                    builtins = NativeLibrary.Load(builtinsPath);
                    library = NativeLibrary.Load(path);
                    var version = (delegate* unmanaged<int*, int*, int>)Export(library, "hiprtcVersion");
                    int rc = version(&major, &minor);
                    if (rc != 0) throw new InvalidOperationException("hiprtcVersion failed: " + rc);
                    BindHiprtc(library);
                }
                catch (Exception error) when (error is DllNotFoundException || error is BadImageFormatException || error is EntryPointNotFoundException ||
                                              error is IOException || error is UnauthorizedAccessException || error is InvalidOperationException)
                {
                    failures.Add(path + ": " + error.Message);
                    ReleaseCandidate();
                    continue;
                }
                // INU_GPU_HIPRTC_EXTRA (space separated) appends diagnostic compiler options; it also enters the module cache key.
                string extra = Environment.GetEnvironmentVariable("INU_GPU_HIPRTC_EXTRA");
                string[] options = new[] { "--offload-arch=" + ArchName }.Concat(HiprtcOptions)
                    .Concat(string.IsNullOrWhiteSpace(extra) ? Array.Empty<string>() : extra.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
                byte[] image;
                int load;
                try
                {
                    image = CompileImage("extern \"C\" __global__ void probe() {}", options, "hsaco", out _);
                    IntPtr module;
                    fixed (byte* data = image) load = cuModuleLoadData(&module, data);
                    if (load == 0) Check(cuModuleUnload(module), "hipModuleUnload compatibility probe");
                }
                catch (Exception error) when (error is InvalidOperationException || error is GpuCudaException)
                {
                    failures.Add(path + ": probe failed: " + error.Message);
                    ReleaseCandidate();
                    continue;
                }
                catch
                {
                    ReleaseCandidate();
                    throw;
                }
                // As for CUDA: only an image the runtime cannot use moves on to the next hiprtc; any other code is reported.
                // HIP uses the same numbers (hipErrorInvalidImage 200, hipErrorNoBinaryForGpu 209, hipErrorInvalidKernelFile 218).
                if (load != 0 && RejectedImageCodes.Contains(load))
                {
                    failures.Add(path + ": hiprtc " + major + "." + minor + " output rejected by the HIP runtime (" + load + ")");
                    ReleaseCandidate();
                    continue;
                }
                if (load != 0)
                {
                    ReleaseCandidate();
                    Check(load, "hipModuleLoadData compatibility probe");
                }
                nvrtc = library; nvrtcBuiltins = builtins; hiprtcComgr.AddRange(comgr);
                NvrtcPath = path; NvrtcBuiltinsPath = builtinsPath; NvrtcVersion = (major, minor); ComgrPaths = comgrPaths;
                SupportedArchs = null; TargetArch = 0; BinaryKind = "hsaco";
                CompileOptions = options;
                return;
            }
            throw Status(new InvalidOperationException("No compatible hiprtc toolchain for " + ArchName + ": " + string.Join("; ", failures)), "hiprtc_rejected");
        }

        internal int[] SupportedArchs { get; private set; }

        /// <summary>
        /// NVRTC libraries to try, newest name first within each directory: INU_GPU_NVRTC_DIR alone when set; otherwise the
        /// runtime folder's <c>nvrtc</c> subfolder (also as a pip <c>--target</c> install of the NVRTC wheel), then the CUDA toolkits, then (Windows)
        /// %LOCALAPPDATA%\GRAL\GpuToolchain\nvrtc, or (Linux) /usr/local/cuda, /usr/local/cuda-&lt;version&gt;, the library folders of
        /// distribution packages and, last, the library the loader itself finds (LD_LIBRARY_PATH, ldconfig).
        /// </summary>
        internal static List<string> NvrtcCandidates(string explicitDirectory, string runtimeDirectory)
        {
            var directories = new List<string>();
            var ignored = new List<string>();
            if (explicitDirectory != null)
            {
                DirectoryValue(explicitDirectory, GpuSettings.Label("nvrtc_dir"));
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
                if (Windows && UserToolchainFolder("nvrtc") is string user) directories.Add(user);
                if (!Windows)
                {
                    // Where a toolkit or a distribution package puts NVRTC when no variable names it.
                    directories.Add("/usr/local/cuda/lib64");
                    foreach (string root in VersionedToolkits()) directories.Add(Path.Combine(root, "lib64"));
                    directories.Add("/usr/lib/x86_64-linux-gnu");
                    directories.Add("/usr/lib64");
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
            // Linux, last: the library the loader itself finds (LD_LIBRARY_PATH, ldconfig entries), newest name first.
            if (explicitDirectory == null && !Windows)
                foreach (string name in new[] { "libnvrtc.so.13", "libnvrtc.so.12" })
                    if (LoaderPath(name) is string path && seen.Add(ResolvedPath(path))) result.Add(path);
            if (result.Count == 0)
            {
                if (explicitDirectory != null) throw GpuSettings.FromFile(Status(new FileNotFoundException(GpuSettings.Label("nvrtc_dir") + " contains no NVRTC library: " + explicitDirectory), "invalid_setting"), "nvrtc_dir");
                string bundled = runtimeDirectory == null ? "" : Path.Combine(runtimeDirectory, "nvrtc") + ", ";
                string notAbsolute = ignored.Count > 0 ? " (ignored because not absolute: " + string.Join(", ", ignored) + ")" : "";
                // Linux has other variables, folders and library names than Windows
                if (!Windows)
                    throw Status(new FileNotFoundException("NVRTC library not found in " + bundled + "$CUDA_PATH or $CUDA_HOME (lib64, lib, targets/x86_64-linux/lib), " +
                                                           "/usr/local/cuda/lib64, /usr/local/cuda-*/lib64, /usr/lib/x86_64-linux-gnu, /usr/lib64 or the loader search path " +
                                                           "(LD_LIBRARY_PATH, ldconfig); set nvrtc_dir in GPU.txt, or INU_GPU_NVRTC_DIR, to the folder that holds " +
                                                           "libnvrtc.so.12 and libnvrtc-builtins.so.12.*, install the CUDA toolkit" +
                                                           (bundled.Length > 0 ? ", or fill the first folder with pip install --target <that folder> nvidia-cuda-nvrtc-cu12" : "") +
                                                           notAbsolute), "no_nvrtc");
                throw Status(new FileNotFoundException("NVRTC library not found in " + bundled +
                                                       "CUDA_PATH or CUDA_PATH_V*" + (Windows && UserToolchainFolder("nvrtc") is string user ? " or " + user : "") +
                                                       "; copy the NVRTC libraries (or pip install --target that folder nvidia-cuda-nvrtc-cu12), " +
                                                       "install the CUDA toolkit, or set INU_GPU_NVRTC_DIR" + notAbsolute), "no_nvrtc");
            }
            return result;
        }

        /// <summary>Linux: the toolkit folders /usr/local/cuda-&lt;major&gt;[.&lt;minor&gt;], newest first.</summary>
        private static IEnumerable<string> VersionedToolkits()
        {
            var found = new List<(int Major, int Minor, string Path)>();
            try
            {
                foreach (string path in Directory.GetDirectories("/usr/local", "cuda-*"))
                {
                    Match version = Regex.Match(Path.GetFileName(path), @"^cuda-([0-9]{1,4})(\.([0-9]{1,4}))?$");
                    if (version.Success)
                        found.Add((int.Parse(version.Groups[1].Value, CultureInfo.InvariantCulture), version.Groups[3].Success ? int.Parse(version.Groups[3].Value, CultureInfo.InvariantCulture) : -1, path));
                }
            }
            catch (Exception error) when (error is UnauthorizedAccessException || error is IOException) { }
            return found.OrderByDescending(t => t.Major).ThenByDescending(t => t.Minor).Select(t => t.Path);
        }

        /// <summary>
        /// Linux: the file the loader finds for a library name, or null. The library is opened by name and closed again; its file
        /// is read from /proc/self/maps, so that it is then tried by its path like every other candidate (the builtins library is
        /// looked for next to it, and both files enter the module cache key).
        /// </summary>
        private static string LoaderPath(string name)
        {
            if (!NativeLibrary.TryLoad(name, out IntPtr handle)) return null;
            try
            {
                foreach (string line in File.ReadLines("/proc/self/maps"))
                {
                    // Address range, permissions, offset, device, inode, then the file with its full path
                    int start = line.IndexOf('/');
                    if (start < 0) continue;
                    string file = Path.GetFileName(line.Substring(start));
                    if (file == name || file.StartsWith(name + ".", StringComparison.Ordinal)) return line.Substring(start);
                }
                return null;
            }
            catch (Exception error) when (error is UnauthorizedAccessException || error is IOException) { return null; }
            finally { NativeLibrary.Free(handle); }
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
            throw Status(new InvalidOperationException("No compatible NVRTC toolchain: " + string.Join("; ", failures) + "; " + NvrtcRequirement(DeviceArch) +
                                                       " and an NVIDIA driver at least as new as that NVRTC"), "nvrtc_rejected");
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

        /// <summary>Compile with the selected options; returns PTX text with its trailing NUL, or CUBIN or HIP code-object bytes.</summary>
        internal byte[] CompileImage(string source, out string compileLog) =>
            CompileImage(Hip ? HipSource(source) : source, CompileOptions, BinaryKind, out compileLog);

        /// <summary>Device flag that the HIP source sets before each trap; the host reads it after every launch.</summary>
        internal const string HipTrapFlag = "gral_hip_trap";

        /// <summary>
        /// HIP copy of State.DeviceError, DeviceErrorPhase and DeviceErrorParticle (in that order). The HIP source places
        /// State in constant memory, which the kernel cannot write, so the kernel records errors here instead.
        /// </summary>
        internal const string HipStatusSymbol = "gral_hip_status";

        private const string StateDeclaration = "extern \"C\" {__device__ State P;}";

        // gfx12 has no FP64 global atomic add, so atomicAdd(double*) is a compare-and-swap loop that serializes on a shared
        // address; EmissionPerSG takes one add per fresh particle at one address per source group. Lanes of a wavefront that
        // share the address of the first remaining lane add their values in lane order and issue one atomic. When no lane
        // shares that address, every remaining lane adds on its own as before. All lanes of a group get the leader's pre-add
        // value back, unlike atomicAdd; every double atomic_add call in generated.cu discards the return value.
        private const string HipAtomicAddDouble =
            "template<class V>__device__ double atomic_add(double* p,V v){\n" +
            " double x=(double)v;\n" +
            " unsigned long long a=(unsigned long long)p;\n" +
            " for(;;){\n" +
            "  unsigned int lo=__builtin_amdgcn_readfirstlane((unsigned int)a),hi=__builtin_amdgcn_readfirstlane((unsigned int)(a>>32));\n" +
            "  bool mine=(unsigned int)a==lo&&(unsigned int)(a>>32)==hi;\n" +
            "  unsigned long long group=__ballot(mine);\n" +
            "  if(__popcll(group)<2)break;\n" +
            "  if(mine){\n" +
            "   int leader=__ffsll((unsigned long long)group)-1;\n" +
            "   double s=0;\n" +
            "   for(unsigned long long g=group;g;g&=g-1)s+=__shfl(x,__ffsll((unsigned long long)g)-1);\n" +
            "   double old=0;\n" +
            "   if(__lane_id()==leader)old=atomicAdd(p,s);\n" +
            "   return __shfl(old,leader);\n" +
            "  }\n" +
            " }\n" +
            " return atomicAdd(p,x);\n" +
            "}\n";

        /// <summary>
        /// Replaces every read of the given scalar State fields (`P.Name`) with a literal of the host value, so the compiler
        /// removes the branches of features that are off and folds run constants. The State layout is unchanged; the host still
        /// uploads the fields. Assignments (`P.Name = ...`) are left alone; the generated kernel has none for these fields, and
        /// any other write form fails to compile, which falls back to the general kernel.
        /// </summary>
        internal static string Specialize(string source, IEnumerable<KeyValuePair<string, string>> literals)
        {
            foreach (var (name, literal) in literals)
                source = Regex.Replace(source, @"(?<![\w.])P\." + Regex.Escape(name) + @"\b(?!\s*=(?!=))", literal, RegexOptions.CultureInvariant);
            return source;
        }

        /// <summary>Replaces each `P.AdaptiveRoughnessMax > 0` test by its host value; the kernel reads the field nowhere else.</summary>
        internal static string SpecializeAdaptiveRoughness(string source, bool positive)
        {
            const string test = "P.AdaptiveRoughnessMax > 0";
            if (!source.Contains(test, StringComparison.Ordinal)) throw new InvalidOperationException("Specialization: no AdaptiveRoughnessMax test");
            source = source.Replace(test, positive ? "true" : "false", StringComparison.Ordinal);
            if (Regex.IsMatch(source, @"(?<![\w.])P\.AdaptiveRoughnessMax\b", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Specialization: AdaptiveRoughnessMax is read in another form");
            return source;
        }

        /// <summary>
        /// Without DepositionExist the host accepts no nonzero dry-deposition mode (GpuRuntimeServer.Guard, GpuSituationDriver),
        /// so the fresh mode read becomes 0 and the residual mode test becomes false; the deposition code drops out.
        /// </summary>
        internal static string SpecializeNoDryDeposition(string source)
        {
            int fresh = 0, residual = 0;
            source = Regex.Replace(source, @"(?<![\w.])P\.ParticleMode\[ParticleIndex\(nteil\)\]", _ => { fresh++; return "0"; }, RegexOptions.CultureInvariant);
            source = Regex.Replace(source, @"(?<![\w.])P\.TransientDepoMode != nullptr", _ => { residual++; return "false"; }, RegexOptions.CultureInvariant);
            if (fresh != 1 || residual != 1 || Regex.IsMatch(source, @"(?<![\w.])P\.ParticleMode\b", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Specialization: unexpected deposition-mode reads " + fresh + "/" + residual);
            return source;
        }

        /// <summary>
        /// When every used particle of the launch has the same source type, the load of that type in Zeitschleife_Calculate
        /// (after its unused-slot return) becomes the value. The read in Zeitschleife_IntStandCalculate stays: the residual
        /// kernel calls that function too, with particle index 0, so a literal there would change residual particles.
        /// </summary>
        internal static string SpecializeSourceType(string source, int type)
        {
            int count = 0;
            source = Regex.Replace(source, @"(\bSourceType_\d+) = P\.SourceType\[ParticleIndex\(nteil\)\];",
                m => { count++; return m.Groups[1].Value + " = " + type.ToString(CultureInfo.InvariantCulture) + ";"; }, RegexOptions.CultureInvariant);
            if (count != 1) throw new InvalidOperationException("Specialization: expected one fresh source-type load, found " + count);
            return source;
        }

        /// <summary>
        /// The kernel source is CUDA C++. HIP-only changes (the CUDA source is untouched; only (3) can change results: it adds a
        /// per-wavefront partial sum once, so a double sum can round differently in the last bits than a one-at-a-time order):
        /// (1) hiprtc rejects the PTX trap instruction, and on the Windows HIP runtime __builtin_trap() ends only the trapping
        /// wavefront while the launch still reports success, so each trap first sets a device flag that the host checks after
        /// every launch; (2) State moves to constant memory, so its uniform fields load through scalar memory instead of one
        /// vector load and wait per read, and the three error fields are written to HipStatusSymbol; (3) double atomic adds
        /// are aggregated per wavefront (HipAtomicAddDouble). Throws if the source no longer has the expected State form.
        /// </summary>
        internal static string HipSource(string source)
        {
            int at = source.IndexOf(StateDeclaration, StringComparison.Ordinal);
            if (at < 0 || source.IndexOf(StateDeclaration, at + 1, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("HIP source: expected exactly one '" + StateDeclaration + "'");
            string hip = source.Replace("asm(\"trap;\")", "{atomicExch(&" + HipTrapFlag + ",1);__threadfence();__builtin_trap();}", StringComparison.Ordinal)
                .Replace(StateDeclaration, "extern \"C\" {__constant__ State P;}", StringComparison.Ordinal)
                .Replace("&P.DeviceError,", "&" + HipStatusSymbol + "[0],", StringComparison.Ordinal)
                .Replace("P.DeviceErrorPhase=", HipStatusSymbol + "[1]=", StringComparison.Ordinal)
                .Replace("P.DeviceErrorParticle=", HipStatusSymbol + "[2]=", StringComparison.Ordinal);
            if (hip.Contains("P.DeviceError", StringComparison.Ordinal))
                throw new InvalidOperationException("HIP source: a State.DeviceError access is not in a recognized form");
            return "extern \"C\" {__device__ int " + HipTrapFlag + "=0;__device__ int " + HipStatusSymbol + "[3];}\n" + HipAtomicAddDouble + hip;
        }

        private byte[] CompileImage(string source, string[] options, string kind, out string compileLog)
        {
            byte[] src = NullTerminated(source), program = NullTerminated(NvrtcProgram);
            byte[][] optionBytes = options.Select(NullTerminated).ToArray();
            IntPtr prog;
            int rc;
            fixed (byte* s = src, p = program) rc = nvrtcCreateProgram(&prog, s, p, 0, null, null);
            if (rc != 0) throw new InvalidOperationException((Hip ? "hiprtcCreateProgram: " : "nvrtcCreateProgram: ") + rc);
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
                if (rc != 0) throw new InvalidOperationException((Hip ? "hiprtcCompileProgram: " : "nvrtcCompileProgram: ") + rc + "; " + compileLog);
                var sizeFn = kind == "ptx" ? nvrtcGetPTXSize : nvrtcGetCUBINSize;
                var dataFn = kind == "ptx" ? nvrtcGetPTX : nvrtcGetCUBIN;
                nuint size;
                rc = sizeFn(prog, &size);
                string getter = Hip ? "hiprtcGetCode" : "nvrtcGet" + kind.ToUpperInvariant();
                if (rc != 0) throw new InvalidOperationException(getter + "Size: " + rc);
                var image = new byte[(long)size];
                fixed (byte* i = image) rc = dataFn(prog, i);
                if (rc != 0) throw new InvalidOperationException(getter + ": " + rc);
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
            Check(rc, Hip ? "hipModuleLoadData hsaco (the HIP runtime must accept the hiprtc output)" : "cuModuleLoadData " + BinaryKind + " (driver must support the NVRTC output)");
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
            foreach (IntPtr handle in hiprtcComgr) NativeLibrary.Free(handle);
            hiprtcComgr.Clear();
            NativeLibrary.Free(driver);
            driver = IntPtr.Zero;
        }
    }
}
