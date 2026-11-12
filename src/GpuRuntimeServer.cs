using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GRAL_2001
{
    /// <summary>
    /// In-process GPU runtime. It reads request frames (8-byte length + JSON header + raw array payload) written by
    /// GpuFieldExport and answers with response frames that GpuFieldImport applies. It runs on its own thread, which owns
    /// the CUDA context, and sees GRAL data only through the frames.
    /// </summary>
    internal sealed unsafe class GpuRuntimeServer
    {
        private static readonly string[] Outputs = { "Conz3dDouble", "Conz5d", "ReceptorConc", "ReceptorParticleMaxConc", "EmissionPerSG" };
        private static readonly string[] AllOutputs = Outputs.Concat(new[] { "Depo_conz", "Conz3dp", "Conz3dm", "ConzSsum", "LogReflexions", "Log_Timesteps" }).ToArray();
        private static readonly string[] ParticleArrays = { "SourceType", "Xcoord", "YCoord", "ZCoord", "ParticleMass", "ParticleSG", "ParticleSource", "ParticleMode", "ParticleVdep", "ParticleVsed" };
        private static readonly string[] ResidualArrays = { "_resI", "_resJ", "_resK", "_resSG", "_resConc", "_resSplit" };
        private static readonly string[] ScratchArrays = { "ParticleReceptorScratch", "ParticleSliceScratch" };
        private static readonly string[] WindowArrays = ParticleArrays.Concat(ResidualArrays).Concat(ScratchArrays).ToArray();
        private static readonly HashSet<string> ValidatedArrays = new HashSet<string> { "TransientDepoMode", "ParticleMode", "DecayRate", "TransientDepoVdep", "TransientDepoVsed", "ParticleVdep", "ParticleVsed", "WetDepoDropVelocity", "DZK" };
        private const int TransferBytes = 1024 * 1024;
        private const int MaxMemoryParticles = 65536;
        private const int AdaptiveStartPart = 256;
        private const long DefaultPinnedBudget = 256L * 1024 * 1024;
        private const int Int32Max = int.MaxValue;
        private const int Int32Min = int.MinValue;

        /// <summary>An array descriptor of a request frame, with its original JSON properties for the result frame.</summary>
        private sealed class Descriptor
        {
            internal string Name;
            internal long Bytes;
            internal string Dtype;
            internal int[] Shape;
            internal bool IsNull;
            internal bool ZeroFill;
            internal List<KeyValuePair<string, JsonElement>> Properties;
        }

        private sealed class Buffer { internal ulong Pointer; internal long Size; }

        /// <summary>A response stream failed after its header was written; the connection must close without an error frame.</summary>
        private sealed class FrameWriteException : Exception { internal FrameWriteException(Exception inner) : base("Response stream failed; connection must close", inner) { } }

        private readonly Stream input, output;
        private readonly Action<string> log;
        private readonly string stderrPath, runtimeDirectory, manifestPath;
        private readonly System.Threading.CancellationToken cancellation;
        private byte[] manifestBytes;
        private GpuStateLayout state;
        private readonly Dictionary<string, string> arrayCpp = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> wireDtypes = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> doubleStorage = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> inputScalars = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> requiredInitScalars = new HashSet<string>(StringComparer.Ordinal);

        private GpuCuda cuda;
        private IntPtr module, fnFresh, fnResidual, eventStart, eventEnd;
        private ulong stateAddress, hipTrapAddress, hipStatusAddress;
        // Reproducible sums (fixed random seeds; generated.cu, GRAL_REPRO_SUMS): the slot order of the kernel's acc_add<slot>
        // calls, the per-slot integer word buffers (three 64-bit words per element), the address of the kernel's gral_repro
        // constant and the finalize entry point of the loaded module.
        private static readonly string[] ReproSlots = { "Conz3dDouble", "Conz3dp", "Conz3dm", "Conz5d", "ConzSsum", "Depo_conz", "ReceptorConc", "EmissionPerSG" };
        private const string ReproDefine = "#define GRAL_REPRO_SUMS 0";
        private const int ReproConstantBytes = 8 * 8 * 2;
        private bool reproSums;
        private string reproSumsSource;
        private ulong reproAddress;
        private IntPtr fnReproFinalize;
        private readonly long[] reproWordElements = new long[ReproSlots.Length];
        // Run-configuration specialization (HIP): the specialized State fields per step, the kernel source, the general code
        // object and the key of the loaded module ("" = general kernel).
        private static readonly string[] SpecializationSteps = { "flags", "ints", "grid", "depo", "src" };
        private static readonly string[] SpecializedInts = { "ISTATIONAER", "IStatistics", "LogLevel", "Topo" };
        private static readonly string[] SpecializedGridInts = { "GrammSouth", "GrammWest", "IKOOAGRAL", "JKOOAGRAL", "NII", "NJJ", "NKK",
                                                                 "NKK_Transient", "NS", "NX", "NXL", "NY", "NYL", "ReceptorNumber", "TS_Count" };
        private const int MaxSpecializedImages = 6;
        private readonly List<string> boolFlags = new List<string>();
        private readonly HashSet<string> intScalars = new HashSet<string>(StringComparer.Ordinal);
        private string[] specializedBools = Array.Empty<string>(), specializedInts = Array.Empty<string>(), specializedGridInts = Array.Empty<string>();
        private readonly HashSet<string> specializationSteps = new HashSet<string>(StringComparer.Ordinal);
        private string generatedSource, loadedSpecialization = "";
        private byte[] generalImage;
        private bool specialize;
        // Source type shared by every used particle of the last fresh launch range: 0-255, MixedSourceTypes, or -1 (not known yet).
        private int freshSourceType = -1;
        private const int MixedSourceTypes = -2;
        private readonly HashSet<string> failedSpecializations = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> specializedImages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private Dictionary<string, object> toolchainDigests;
        private readonly Dictionary<string, Buffer> buffers = new Dictionary<string, Buffer>(StringComparer.Ordinal);
        private readonly Dictionary<string, IntPtr> mappedBuffers = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> hostArrays = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> validationArrays = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, Descriptor> descriptors = new Dictionary<string, Descriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> activeDepositionArrays = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> metrics = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> phaseCounts = new Dictionary<string, long> { { "fresh", 0 }, { "residual", 0 } };
        private readonly Dictionary<string, Dictionary<string, int>> kernelResources = new Dictionary<string, Dictionary<string, int>>();
        private readonly Dictionary<string, long> adaptivePart = new Dictionary<string, long>(StringComparer.Ordinal);
        private double kernelTotalMs, startupMs, lastKernelMs;
        private long residualTraceBase, deviceBudget, windowReserve, maxKernelMs;
        private (long Base, long Count)? residualErrorMapping;
        private bool executionFailed, adaptiveLaunch;
        private readonly byte[] transfer = new byte[TransferBytes];

        internal int ExitCode { get; private set; }

        internal GpuRuntimeServer(Stream input, Stream output, string manifestPath, string stderrPath, Action<string> log, System.Threading.CancellationToken cancellation)
        {
            this.input = input;
            this.output = output;
            // Logging must never fail the runtime: the host may already have closed the log after a timeout.
            this.log = line => { try { log(line); } catch (Exception error) when (error is ObjectDisposedException || error is IOException) { } };
            this.stderrPath = stderrPath;
            this.manifestPath = manifestPath;
            this.cancellation = cancellation;
            runtimeDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        }

        /// <summary>Reads the field manifest and builds the state layout; runs on the runtime thread so errors reach the log and the host.</summary>
        private void LoadManifest()
        {
            manifestBytes = File.ReadAllBytes(manifestPath);
            using var manifest = JsonDocument.Parse(manifestBytes);
            state = new GpuStateLayout(manifest.RootElement);
            foreach (JsonElement f in manifest.RootElement.GetProperty("fields").EnumerateArray())
            {
                string name = f.GetProperty("name").GetString(), cpp = f.GetProperty("cpp").GetString();
                bool constant = f.GetProperty("constant").GetBoolean();
                if (cpp.StartsWith("Arr<", StringComparison.Ordinal)) arrayCpp[name] = cpp;
                else if (!constant)
                {
                    inputScalars.Add(name); requiredInitScalars.Add(name);
                    if (cpp == "bool") boolFlags.Add(name);
                    if (cpp == "int") intScalars.Add(name);
                }
                if (f.TryGetProperty("wire_dtype", out var wire) && wire.ValueKind == JsonValueKind.String) wireDtypes[name] = wire.GetString();
            }
            foreach (var (name, cpp) in new[] { ("GridRecStart", "Arr<int,1>"), ("GridRecList", "Arr<int,1>"), ("Trace", "Arr<double,2>"),
                                                ("TraceIds", "Arr<int,1>"), ("StepTrace", "Arr<double,3>"), ("ResidualTrace", "Arr<double,2>") })
                arrayCpp[name] = cpp;
            foreach (string name in ResidualArrays) arrayCpp[name] = name == "_resConc" ? "Arr<float,1>" : "Arr<int,1>";
            foreach (var pair in wireDtypes)
                if (pair.Value == "Single" && arrayCpp.TryGetValue(pair.Key, out string arrayType) && arrayType.StartsWith("Arr<double,", StringComparison.Ordinal))
                    doubleStorage.Add(pair.Key);
            foreach (string name in new[] { "RnGSeed1", "RnGSeed2", "GridRecRow", "TraceEnabled" }) inputScalars.Add(name);
            foreach (string name in new[] { "RnGSeed1", "RnGSeed2", "GridRecRow" }) requiredInitScalars.Add(name);
            // TraceEnabled is a host-only State bool; it gates the trace stores in every loop step.
            specializedBools = boolFlags.Concat(state.Has("TraceEnabled") ? new[] { "TraceEnabled" } : Array.Empty<string>())
                .Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            // Run constants only: IWET (situation index) and MetProfileNumb (per situation) are never specialized.
            specializedInts = SpecializedInts.Where(intScalars.Contains).ToArray();
            specializedGridInts = SpecializedGridInts.Where(intScalars.Contains).ToArray();
        }

        // ------------------------------------------------------------------ startup

        /// <summary>The steps INU_GPU_SPECIALIZE asks for; throws on an unknown step. Also called by GRAL --gpu-check.</summary>
        internal static HashSet<string> SpecializationSetting()
        {
            var steps = new HashSet<string>(StringComparer.Ordinal);
            string setting = (Environment.GetEnvironmentVariable("INU_GPU_SPECIALIZE") ?? "").Trim().ToLowerInvariant();
            string usage = " (use 0, off, 1, on or a list of " + string.Join(", ", SpecializationSteps) + ")";
            if (setting.Length == 0 || setting == "1" || setting == "on") steps.UnionWith(SpecializationSteps);
            else if (setting != "0" && setting != "off")
            {
                foreach (string step in setting.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!SpecializationSteps.Contains(step, StringComparer.Ordinal))
                        throw new ArgumentException("INU_GPU_SPECIALIZE: unknown step '" + step + "'" + usage);
                    steps.Add(step);
                }
                if (steps.Count == 0) throw new ArgumentException("INU_GPU_SPECIALIZE: no step in '" + setting + "'" + usage);
            }
            return steps;
        }

        /// <summary>INU_GPU_REPRODUCIBLE_SUMS: empty, 0 or 1; throws on another value. Also called by GRAL --gpu-check.</summary>
        internal static string ReproducibleSumsSetting()
        {
            string setting = (Environment.GetEnvironmentVariable("INU_GPU_REPRODUCIBLE_SUMS") ?? "").Trim();
            if (setting.Length > 0 && setting != "0" && setting != "1")
                throw new ArgumentException("INU_GPU_REPRODUCIBLE_SUMS: use 0 or 1, not '" + setting + "'");
            return setting;
        }

        private void Canceled() => cancellation.ThrowIfCancellationRequested();

        private void Initialize()
        {
            LoadManifest();
            Canceled();
            cuda = new GpuCuda(runtimeDirectory, log);
            Canceled();
            // HIP only; the CUDA path compiles one general kernel. Read before the first compile, so a wrong value fails early.
            // INU_GPU_SPECIALIZE (any case): unset, 1 or on = all steps; 0 or off = general kernel; a comma list of steps (flags,
            // ints, grid, depo, src) enables only those.
            if (cuda.Hip) specializationSteps.UnionWith(SpecializationSetting());
            specialize = specializationSteps.Count > 0;
            // Order-independent sums when the run uses fixed random seeds (in.dat line 22, "Use fixed random seed value" = 1), unless
            // in.dat line 24 is 0; then, and in a native-seed run, the atomic floating-point sums. INU_GPU_REPRODUCIBLE_SUMS = 0 or 1
            // overrides line 24 for one run.
            string reproSetting = ReproducibleSumsSetting();
            reproSums = Program.UseFixedRndSeedVal && (reproSetting.Length > 0 ? reproSetting != "0" : Program.GpuReproducibleSums);
            reproSumsSource = !Program.UseFixedRndSeedVal ? "in.dat line 22: no fixed random seeds" : reproSetting.Length > 0 ? "INU_GPU_REPRODUCIBLE_SUMS=" + reproSetting :
                              Program.GpuReproducibleSums ? "in.dat line 22" : "in.dat line 24";
            metrics["reproducible_sums"] = reproSums;
            metrics["repro_finalize_ms"] = 0.0; metrics["repro_word_bytes"] = 0L;
            metrics["pipe_read_wait_ms"] = 0.0; metrics["h2d_ms"] = 0.0; metrics["d2h_ms"] = 0.0; metrics["kernel_device_ms"] = 0.0;
            metrics["kernel_launches"] = 0L; metrics["kernel_max_device_ms"] = 0.0; metrics["buffer_allocation_count"] = 0L; metrics["buffer_reuse_count"] = 0L;
            metrics["buffer_free_count"] = 0L; metrics["device_buffer_bytes"] = 0L; metrics["peak_device_buffer_bytes"] = 0L; metrics["zero_fill_bytes"] = 0L;
            metrics["zero_fill_count"] = 0L; metrics["zero_fill_ms"] = 0.0; metrics["h2d_bytes"] = 0L; metrics["d2h_bytes"] = 0L; metrics["status_d2h_bytes"] = 0L;
            metrics["status_d2h_ms"] = 0.0; metrics["state_d2h_bytes"] = 0L; metrics["state_d2h_ms"] = 0.0; metrics["init_count"] = 0L;
            metrics["mapped_host_bytes"] = 0L; metrics["peak_mapped_host_bytes"] = 0L; metrics["device_allocation_oom"] = 0L; metrics["particle_window_retries"] = 0L;
            metrics["fresh_memory_windows"] = 0L; metrics["residual_memory_windows"] = 0L; metrics["mapped_host_copy_bytes"] = 0L; metrics["mapped_host_copy_ms"] = 0.0;
            metrics["mapped_output_bytes"] = 0L; metrics["peak_mapped_output_bytes"] = 0L; metrics["mapped_scratch_bytes"] = 0L; metrics["peak_mapped_scratch_bytes"] = 0L;
            metrics["mapped_buffer_names"] = new List<string>();
            var start = Stopwatch.StartNew();
            metrics["context_ms"] = cuda.ContextMs; metrics["cache_read_ms"] = 0.0; metrics["nvrtc_compile_ms"] = 0.0; metrics["module_load_ms"] = 0.0;
            metrics["post_load_abi_ms"] = 0.0; metrics["cache_status"] = "disabled"; metrics["cache_write_status"] = "not_attempted";
            metrics["device"] = new Dictionary<string, object>(cuda.DeviceInfo);
            metrics["host"] = ".NET " + Environment.Version;
            string sourcePath = Path.Combine(runtimeDirectory, "generated.cu");
            string source = File.ReadAllText(sourcePath, Encoding.UTF8);
            if (reproSums)
            {
                int define = source.IndexOf(ReproDefine, StringComparison.Ordinal);
                if (define < 0 || source.IndexOf(ReproDefine, define + 1, StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("generated.cu: expected exactly one '" + ReproDefine + "'");
                source = source.Replace(ReproDefine, "#define GRAL_REPRO_SUMS 1", StringComparison.Ordinal);
            }
            generatedSource = source;
            string cachePath = null, key = null;
            byte[] image = null;
            var readClock = Stopwatch.StartNew();
            try
            {
                if (GpuModuleCache.Directory() != null)
                {
                    (cachePath, key) = GpuModuleCache.CachePath(ModuleIdentity(cuda.Hip ? GpuCuda.HipSource(source) : source));
                    if (cachePath != null) { string status; (image, status) = GpuModuleCache.Read(cachePath, key, cuda.BinaryKind); metrics["cache_status"] = status; }
                    else metrics["cache_status"] = key;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                metrics["cache_status"] = "identity_error";
                log((cuda.Hip ? "hsaco" : "PTX") + " cache identity error: " + error.Message);
            }
            metrics["cache_read_ms"] = readClock.Elapsed.TotalMilliseconds;
            if (image != null)
            {
                try { LoadAndValidate(image); }
                catch (Exception error) when (module == IntPtr.Zero)
                {
                    metrics["cache_status"] = "hit_rejected";
                    log((cuda.Hip ? "hsaco" : "PTX") + " cache entry rejected: " + error.Message);
                    image = null;
                }
            }
            if (image == null)
            {
                var compileClock = Stopwatch.StartNew();
                string compileLog;
                try { image = cuda.CompileImage(source, out compileLog); }
                finally { metrics["nvrtc_compile_ms"] = (double)metrics["nvrtc_compile_ms"] + compileClock.Elapsed.TotalMilliseconds; }
                LoadAndValidate(image);
                WriteDiagnostic("server_compile.log", compileLog);
                if (cachePath != null) metrics["cache_write_status"] = GpuModuleCache.Write(cachePath, key, image, cuda.BinaryKind);
            }
            else WriteDiagnostic("server_compile.log", cuda.Hip ? "Module cache hit; hiprtc compile skipped.\n" : "Module cache hit; NVRTC compile skipped.\n");
            WriteDiagnostic("state_layout.json", state.LayoutJson());
            Canceled();
            PrewarmKernels();
            generalImage = image;
            if (cuda.Hip)
            {
                metrics["specialization"] = specialize ? string.Join(",", SpecializationSteps.Where(specializationSteps.Contains)) : "disabled";
                metrics["specialization_flags"] = ""; metrics["specialization_status"] = "general"; metrics["specialization_switches"] = 0L;
                metrics["specialization_failures"] = 0L; metrics["specialization_ms"] = 0.0;
            }
            eventStart = cuda.CreateEvent();
            eventEnd = cuda.CreateEvent();
            startupMs = start.Elapsed.TotalMilliseconds;
            RefreshMemoryBudget();
            ConfigureLaunchSizing();
        }

        /// <summary>Module cache identity of a kernel source as it is compiled (after GpuCuda.HipSource on HIP).</summary>
        private Dictionary<string, object> ModuleIdentity(string compiledSource)
        {
            // The compiler libraries are hashed once per process (amd_comgr.dll alone is about 120 MB).
            toolchainDigests ??= new Dictionary<string, object>
            {
                {"nvrtc_sha256", GpuModuleCache.Digest(File.ReadAllBytes(cuda.NvrtcPath))},
                {"nvrtc_builtins_sha256", GpuModuleCache.Digest(File.ReadAllBytes(cuda.NvrtcBuiltinsPath))},
                {"comgr_sha256", cuda.Hip ? cuda.ComgrPaths.Select(p => GpuModuleCache.Digest(File.ReadAllBytes(p))).ToArray() : null}
            };
            // On HIP the key covers the source as compiled (after HipSource) and the comgr libraries that generate the code.
            var identity = new Dictionary<string, object>
            {
                {"schema", GpuModuleCache.Schema}, {"source_sha256", GpuModuleCache.Digest(Encoding.UTF8.GetBytes(compiledSource))},
                {"manifest_sha256", GpuModuleCache.Digest(manifestBytes)}, {"state_size", state.Size}, {"state_alignment", 8},
                {"fields", state.Fields.Select(f => new object[] { f.Name, f.Cpp, f.Offset, f.Size }).ToArray()},
                {"options", cuda.CompileOptions}, {"program", GpuCuda.NvrtcProgram}, {"nvrtc_version", new[] { cuda.NvrtcVersion.Major, cuda.NvrtcVersion.Minor }},
                {"binary_kind", cuda.BinaryKind}, {"nvrtc_sha256", toolchainDigests["nvrtc_sha256"]},
                {"nvrtc_builtins_sha256", toolchainDigests["nvrtc_builtins_sha256"]}
            };
            if (cuda.Hip)
            {
                identity["comgr_sha256"] = toolchainDigests["comgr_sha256"];
                // hiprtc and comgr also read these variables themselves; each can change the generated code.
                identity["hip_compiler_environment"] = new[] { "HIPRTC_COMPILE_OPTIONS_APPEND", "HIPRTC_LINK_OPTIONS_APPEND", "AMD_COMGR_DRIVER_OPTIONS_APPEND", "HIP_DEVICE_LIB_PATH" }
                    .Select(n => n + "=" + (Environment.GetEnvironmentVariable(n) ?? "")).ToArray();
            }
            return identity;
        }

        /// <summary>
        /// One specialization of the kernel for the current State values: literals for run-constant scalar fields (GpuCuda.Specialize),
        /// the AdaptiveRoughnessMax test, the dry-deposition reads when deposition is off, and the fresh source-type load when
        /// every used particle of the last fresh range has one type (a mixed range keeps the load: one entry per source type
        /// split the launches and was slower on B00). Key lists every specialized value; it names the code object.
        /// </summary>
        private sealed class SpecializationPlan
        {
            internal readonly List<string> Key = new List<string>();
            internal readonly List<KeyValuePair<string, string>> Literals = new List<KeyValuePair<string, string>>();
            internal bool? RoughnessPositive;
            internal bool NoDryDeposition;
            internal int SourceType = -1;
            internal string Name => string.Join(",", Key);
        }

        private SpecializationPlan PlanSpecialization(bool withSourceType)
        {
            var plan = new SpecializationPlan();
            void Literal(string name, string text, string keyValue) { plan.Literals.Add(new KeyValuePair<string, string>(name, text)); plan.Key.Add(name + "=" + keyValue); }
            static string IntText(int v) => v == int.MinValue ? "(-2147483647-1)" : "(" + v.ToString(CultureInfo.InvariantCulture) + ")";
            if (specializationSteps.Contains("flags"))
                foreach (string name in specializedBools) { bool v = B(name); Literal(name, v ? "true" : "false", v ? "1" : "0"); }
            if (specializationSteps.Contains("ints"))
            {
                foreach (string name in specializedInts) Literal(name, IntText(I(name)), I(name).ToString(CultureInfo.InvariantCulture));
                if (state.Has("AdaptiveRoughnessMax"))
                {
                    plan.RoughnessPositive = state.GetReal("AdaptiveRoughnessMax") > 0;
                    plan.Key.Add("AdaptiveRoughnessMax>0=" + (plan.RoughnessPositive.Value ? "1" : "0"));
                }
            }
            if (specializationSteps.Contains("grid"))
                foreach (string name in specializedGridInts) Literal(name, IntText(I(name)), I(name).ToString(CultureInfo.InvariantCulture));
            // Guard() accepts no nonzero dry-deposition mode without DepositionExist, so the mode reads are 0 for every launch.
            if (specializationSteps.Contains("depo") && state.Has("DepositionExist") && !B("DepositionExist"))
            {
                plan.NoDryDeposition = true;
                plan.Key.Add("DryDepositionModes=0");
            }
            if (withSourceType && specializationSteps.Contains("src") && freshSourceType >= 0)
            {
                plan.SourceType = freshSourceType;
                plan.Key.Add("FreshSourceType=" + freshSourceType.ToString(CultureInfo.InvariantCulture));
            }
            return plan;
        }

        private static long ReadParticle(byte[] data, long index, int width) => width switch
        {
            1 => data[index],
            2 => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan((int)(2 * index))),
            4 => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan((int)(4 * index))),
            8 => BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan((int)(8 * index))),
            _ => throw new ArgumentException("Unsupported particle array width " + width)
        };

        /// <summary>
        /// Source type shared by every used particle (ParticleSource != 0) in [first, first + count), or MixedSourceTypes.
        /// A range without used particles keeps the previous value: the kernel returns before the source-type load for them.
        /// </summary>
        private int UniformSourceType(long first, long count)
        {
            byte[] types = hostArrays["SourceType"], sources = hostArrays["ParticleSource"];
            int typeWidth = DtypeSize("SourceType"), sourceWidth = DtypeSize("ParticleSource");
            long found = -1;
            for (long i = first; i < first + count; i++)
            {
                if (ReadParticle(sources, i, sourceWidth) == 0) continue;
                long type = ReadParticle(types, i, typeWidth);
                if (type < 0 || type > 255) return MixedSourceTypes;
                if (found < 0) found = type;
                else if (found != type) return MixedSourceTypes;
            }
            return found < 0 ? freshSourceType : (int)found;
        }

        /// <summary>
        /// HIP: run the kernel compiled for the current run-configuration values (PlanSpecialization), so the compiler drops
        /// the code of features this run does not use. Called before every State upload, so the loaded kernel always matches
        /// the values of the next launch; if a value changes between situations, the kernel for the new values is loaded. Each
        /// key is compiled once per process (and kept in the module cache); after MaxSpecializedImages keys, new source-type
        /// variants are no longer built. If a specialized kernel cannot be built or loaded, that key runs on the general
        /// kernel. Invariant: loadedSpecialization names the loaded code object.
        /// </summary>
        private void EnsureSpecialized()
        {
            if (!specialize) return;
            var plan = PlanSpecialization(withSourceType: true);
            // Past the cap, a single-source-type variant that is not built yet is not compiled; the run-constant kernel without
            // the source-type step is used instead.
            if (plan.SourceType >= 0 && !specializedImages.ContainsKey(plan.Name) && specializedImages.Count >= MaxSpecializedImages)
                plan = PlanSpecialization(withSourceType: false);
            string key = plan.Name;
            string target = failedSpecializations.Contains(key) ? "" : key;
            if (target == loadedSpecialization) return;
            var clock = Stopwatch.StartNew();
            // SwitchModule restores the previous module when it fails, so a different handle after an error means a new
            // code object was loaded before the error (for example in the module-cache write).
            IntPtr entryModule = module;
            string status = "general";
            try
            {
                if (target.Length == 0) SwitchModule(generalImage);
                else status = LoadSpecialized(plan);
                loadedSpecialization = target;
                metrics["specialization_switches"] = (long)metrics["specialization_switches"] + 1;
            }
            catch (Exception error) when (!(error is OperationCanceledException) && target.Length > 0)
            {
                // Return to the general kernel if the loaded code object is specialized for other values or was loaded
                // during this call (a failure of that switch stops the run).
                failedSpecializations.Add(key);
                metrics["specialization_failures"] = (long)metrics["specialization_failures"] + 1;
                log("GPU kernel specialization for " + key + " failed; using the general kernel: " + error.Message);
                status = "failed";
                if (loadedSpecialization.Length > 0 || module != entryModule)
                {
                    SwitchModule(generalImage);
                    loadedSpecialization = "";
                    metrics["specialization_switches"] = (long)metrics["specialization_switches"] + 1;
                }
            }
            metrics["specialization_flags"] = loadedSpecialization; metrics["specialization_status"] = status;
            metrics["specialization_ms"] = (double)metrics["specialization_ms"] + clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>Builds (memory, module cache or hiprtc) and loads the kernel for one plan; returns how it was obtained.</summary>
        private string LoadSpecialized(SpecializationPlan plan)
        {
            string flags = plan.Name;
            if (specializedImages.TryGetValue(flags, out byte[] known))
            {
                SwitchModule(known);
                return "memory";
            }
            string source = GpuCuda.Specialize(generatedSource, plan.Literals);
            if (plan.RoughnessPositive.HasValue) source = GpuCuda.SpecializeAdaptiveRoughness(source, plan.RoughnessPositive.Value);
            if (plan.NoDryDeposition) source = GpuCuda.SpecializeNoDryDeposition(source);
            if (plan.SourceType >= 0) source = GpuCuda.SpecializeSourceType(source, plan.SourceType);
            string cachePath = null, key = null;
            byte[] image = null;
            try
            {
                if (GpuModuleCache.Directory() != null)
                {
                    (cachePath, key) = GpuModuleCache.CachePath(ModuleIdentity(GpuCuda.HipSource(source)));
                    if (cachePath != null) (image, _) = GpuModuleCache.Read(cachePath, key, cuda.BinaryKind);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                log("hsaco cache identity error for the specialized kernel: " + error.Message);
                cachePath = null; image = null;
            }
            string status = "cache_hit";
            if (image != null)
            {
                try { SwitchModule(image); }
                catch (Exception error) when (!(error is OperationCanceledException))
                {
                    log("hsaco cache entry for the specialized kernel rejected: " + error.Message);
                    image = null;
                }
            }
            if (image == null)
            {
                status = "compiled";
                image = cuda.CompileImage(source, out string compileLog);
                WriteDiagnostic("server_compile_specialized.log", "flags: " + flags + "\n" + compileLog);
                SwitchModule(image);
                if (cachePath != null) metrics["specialization_cache_write_status"] = GpuModuleCache.Write(cachePath, key, image, cuda.BinaryKind);
            }
            specializedImages[flags] = image;
            return status;
        }

        /// <summary>
        /// Loads another code object of the same kernel source, validates and prewarms it, and only then releases the previous
        /// one. On any failure the previous code object, its handles and the kernel resources stay in use.
        /// </summary>
        private void SwitchModule(byte[] image)
        {
            IntPtr oldModule = module, oldFresh = fnFresh, oldResidual = fnResidual, oldFinalize = fnReproFinalize;
            ulong oldState = stateAddress, oldTrap = hipTrapAddress, oldStatus = hipStatusAddress, oldRepro = reproAddress;
            var oldResources = new Dictionary<string, Dictionary<string, int>>(kernelResources);
            try
            {
                LoadAndValidate(image);
                PrewarmKernels(recordStartupMetrics: false);
            }
            catch
            {
                if (module != IntPtr.Zero && module != oldModule)
                {
                    IntPtr failed = module;
                    module = IntPtr.Zero;
                    try { cuda.UnloadModule(failed); }
                    catch (GpuCudaException unload) { log("Could not unload the rejected GPU module: " + unload.Message); }
                }
                module = oldModule; fnFresh = oldFresh; fnResidual = oldResidual; fnReproFinalize = oldFinalize;
                stateAddress = oldState; hipTrapAddress = oldTrap; hipStatusAddress = oldStatus; reproAddress = oldRepro;
                kernelResources.Clear();
                foreach (var pair in oldResources) kernelResources[pair.Key] = pair.Value;
                throw;
            }
            if (oldModule != IntPtr.Zero)
            {
                try { cuda.UnloadModule(oldModule); }
                catch (GpuCudaException unload) { log("Could not unload the previous GPU module: " + unload.Message); }
            }
        }

        private void WriteDiagnostic(string name, string text)
        {
            string target = !string.IsNullOrEmpty(stderrPath)
                ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(stderrPath)), Path.GetFileNameWithoutExtension(stderrPath) + "." + name)
                : Path.Combine(Path.GetTempPath(), "inu_gpu_" + Environment.ProcessId + "." + name);
            try { File.WriteAllText(target, text, new UTF8Encoding(false)); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { log("Could not write diagnostic file " + target + ": " + error.Message); }
        }

        private void LoadAndValidate(byte[] image)
        {
            module = IntPtr.Zero;
            var clock = Stopwatch.StartNew();
            try { module = cuda.LoadImage(image); }
            finally { metrics["module_load_ms"] = (double)metrics["module_load_ms"] + clock.Elapsed.TotalMilliseconds; }
            clock.Restart();
            try
            {
                cuda.Function(module, "gral_state_size");
                fnFresh = cuda.Function(module, "gral_fresh");
                fnResidual = cuda.Function(module, "gral_residual");
                long bytes;
                (stateAddress, bytes) = cuda.Global(module, "P");
                if (bytes != state.Size) throw new InvalidOperationException("State layout size: " + (cuda.Hip ? "HIP " : "CUDA ") + bytes + ", host " + state.Size);
                if (reproSums)
                {
                    fnReproFinalize = cuda.Function(module, "gral_repro_finalize");
                    long reproBytes;
                    (reproAddress, reproBytes) = cuda.Global(module, "gral_repro");
                    if (reproBytes != ReproConstantBytes) throw new InvalidOperationException("gral_repro size: " + reproBytes + ", host " + ReproConstantBytes);
                }
                if (cuda.Hip)
                {
                    hipTrapAddress = cuda.Global(module, GpuCuda.HipTrapFlag).Address;
                    // The HIP kernel writes the three State error fields to a separate symbol in the same order (GpuCuda.HipSource).
                    var (statusStart, statusSize) = StatusRange();
                    if (statusSize != 12 || state.Get("DeviceErrorPhase").Offset != statusStart + 4)
                        throw new InvalidOperationException("State error fields are not three consecutive ints");
                    long statusBytes;
                    (hipStatusAddress, statusBytes) = cuda.Global(module, GpuCuda.HipStatusSymbol);
                    if (statusBytes != 12) throw new InvalidOperationException("HIP status symbol size: " + statusBytes);
                }
            }
            catch
            {
                IntPtr failed = module;
                module = IntPtr.Zero;
                cuda.UnloadModule(failed);
                throw;
            }
            finally { metrics["post_load_abi_ms"] = (double)metrics["post_load_abi_ms"] + clock.Elapsed.TotalMilliseconds; }
        }

        private void PrewarmKernels(bool recordStartupMetrics = true)
        {
            // Launch the entry points with no active particles before field allocation to fix their resource footprint.
            // A module switch (run-configuration specialization) prewarms again but keeps the startup metrics.
            var clock = Stopwatch.StartNew();
            if (recordStartupMetrics)
            {
                metrics["kernel_prewarm_count"] = 0L;
                metrics["device_free_before_prewarm_bytes"] = cuda.MemoryInfo().Free;
            }
            int zero0 = 0, zero1 = 0;
            ulong* pointers = stackalloc ulong[ResidualArrays.Length];
            void** args = stackalloc void*[2 + ResidualArrays.Length];
            args[0] = &zero0; args[1] = &zero1;
            for (int i = 0; i < ResidualArrays.Length; i++) { pointers[i] = 0; args[2 + i] = &pointers[i]; }
            var entries = new List<(string, IntPtr)> { ("fresh", fnFresh), ("residual", fnResidual) };
            // Describe only the entry points of the loaded module (SwitchModule keeps its own copy for a rollback).
            kernelResources.Clear();
            foreach (var (phase, function) in entries)
            {
                var attributes = cuda.FunctionAttributes(function);
                kernelResources[phase] = new Dictionary<string, int> { { "max_threads_per_block", attributes.MaxThreadsPerBlock }, { "local_size_bytes", attributes.LocalSizeBytes }, { "registers", attributes.Registers } };
                uint block = (uint)Math.Min(32, attributes.MaxThreadsPerBlock);
                cuda.Launch(function, 1, block, args, "prewarm " + phase);
                cuda.Synchronize("synchronize prewarm " + phase);
                if (recordStartupMetrics) metrics["kernel_prewarm_count"] = (long)metrics["kernel_prewarm_count"] + 1;
            }
            metrics["kernel_resources"] = kernelResources;
            if (!recordStartupMetrics) return;
            metrics["device_free_after_prewarm_bytes"] = cuda.MemoryInfo().Free;
            metrics["kernel_prewarm_ms"] = clock.Elapsed.TotalMilliseconds;
        }

        private Dictionary<string, object> ProbeInfo()
        {
            var info = new Dictionary<string, object>(cuda.DeviceInfo);
            info["total_memory_mib"] = (long)cuda.DeviceInfo["total_memory_bytes"] / (1024 * 1024);
            info["target"] = cuda.TargetText;
            info["device_free_bytes"] = metrics["device_free_bytes"];
            info["device_budget_bytes"] = deviceBudget;
            info["adaptive_launch"] = adaptiveLaunch;
            info["max_kernel_ms"] = maxKernelMs;
            info["host"] = ".NET " + Environment.Version;
            info["startup_ms"] = startupMs;
            info["reproducible_sums"] = reproSums;
            info["reproducible_sums_source"] = reproSumsSource;
            return info;
        }

        // ------------------------------------------------------------------ launch sizing and memory budget

        private void ConfigureLaunchSizing()
        {
            maxKernelMs = MemorySetting("max_kernel_ms", 500, 0);
            adaptiveLaunch = cuda.KernelExecTimeout && maxKernelMs > 0;
            adaptivePart.Clear();
            lastKernelMs = 0;
            metrics["kernel_exec_timeout"] = cuda.KernelExecTimeout; metrics["adaptive_launch"] = adaptiveLaunch; metrics["max_kernel_ms"] = maxKernelMs;
            metrics["adaptive_part_min"] = null; metrics["adaptive_part_max"] = null; metrics["adaptive_part_final"] = new Dictionary<string, long>();
            metrics["launches_over_max_kernel_ms"] = 0L; metrics["slow_launches"] = new List<object>();
        }

        private void BeginAdaptive(string phase)
        {
            // A new situation can be much slower per particle; restart from a quarter of the learned size.
            if (adaptiveLaunch && adaptivePart.TryGetValue(phase, out long part)) adaptivePart[phase] = Math.Max(AdaptiveStartPart, part / 4);
        }

        private long LaunchPart(string phase, long chunk) =>
            !adaptiveLaunch ? chunk : Math.Min(chunk, adaptivePart.TryGetValue(phase, out long part) ? part : AdaptiveStartPart);

        private void AdaptLaunch(string phase, long part)
        {
            // Particles are seeded and use scratch rows per particle, so each particle's path does not depend on the split. With
            // reproducible sums the results do not depend on it either; the floating-point sums depend on the order of the
            // additions, which the split changes.
            if (!adaptiveLaunch) return;
            double elapsed = lastKernelMs, target = maxKernelMs;
            long current = adaptivePart.TryGetValue(phase, out long known) ? known : AdaptiveStartPart;
            if (elapsed > target)
            {
                metrics["launches_over_max_kernel_ms"] = (long)metrics["launches_over_max_kernel_ms"] + 1;
                var slow = (List<object>)metrics["slow_launches"];
                if (slow.Count < 16) slow.Add(new Dictionary<string, object> { { "phase", phase }, { "part", part }, { "planned", current }, { "device_ms", Math.Round(elapsed, 1) } });
            }
            long next;
            if (elapsed > 0.5 * target) next = Math.Max(1, Math.Min(current, (long)(part * 0.5 * target / elapsed)));
            else if (part >= current)
            {
                // Only a full-size launch is evidence for growth; a short tail launch is not.
                next = Math.Min(Math.Min(MaxMemoryParticles, 2 * part), (long)(part * 0.5 * target / Math.Max(elapsed, 1e-3)));
                next = Math.Max(next, current);
            }
            else next = current;
            adaptivePart[phase] = next;
            metrics["adaptive_part_min"] = metrics["adaptive_part_min"] is long low ? Math.Min(low, part) : part;
            metrics["adaptive_part_max"] = metrics["adaptive_part_max"] is long high ? Math.Max(high, part) : part;
            metrics["adaptive_part_final"] = new Dictionary<string, long>(adaptivePart);
        }

        // key: a key of the GPU settings file; its variable wins over the file (GpuSettings)
        private static long MemorySetting(string key, long fallback, long minimum) =>
            MemoryValue(GpuSettings.Value(key) ?? fallback.ToString(System.Globalization.CultureInfo.InvariantCulture), GpuSettings.Label(key), minimum);

        internal static long MemoryValue(string raw, string label, long minimum)
        {
            if (raw.Length == 0 || !raw.All(ch => ch >= '0' && ch <= '9') ||
                !long.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long value) || value < minimum)
                throw new ArgumentException(label + " must be an integer of at least " + minimum.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", got '" + raw + "'");
            return value;
        }

        private static long DefaultReserve(long total) => Math.Min(Math.Min(512L * 1024 * 1024, Math.Max(128L * 1024 * 1024, total / 10)), total / 4);

        private long DeviceBufferBytes { get => (long)metrics["device_buffer_bytes"]; set => metrics["device_buffer_bytes"] = value; }
        private void Add(string key, long value) => metrics[key] = (long)metrics[key] + value;
        private void AddMs(string key, Stopwatch clock) => metrics[key] = (double)metrics[key] + clock.Elapsed.TotalMilliseconds;
        private void Peak(string key, long value) => metrics[key] = Math.Max((long)metrics[key], value);

        private void RefreshMemoryBudget()
        {
            var (free, total) = cuda.MemoryInfo();
            long reserve = MemorySetting("memory_reserve_bytes", DefaultReserve(total), 0);
            long requested = MemorySetting("memory_budget_bytes", total, 4096);
            long owned = DeviceBufferBytes;
            deviceBudget = Math.Min(requested, owned + Math.Max(0, free - reserve));
            // Keep room for one fresh or residual window when placing full fields.
            windowReserve = Math.Min(MaxMemoryParticles * 43L + 43, Math.Max(43, deviceBudget / 4));
            metrics["device_free_bytes"] = free; metrics["device_total_bytes"] = total; metrics["device_reserve_bytes"] = reserve;
            metrics["device_budget_bytes"] = deviceBudget; metrics["particle_window_reserved_bytes"] = windowReserve;
        }

        // ------------------------------------------------------------------ buffers

        private int DtypeSize(string name) => GpuStateLayout.SizeOfDtype(descriptors[name].Dtype);

        private void Release(string name)
        {
            if (!buffers.Remove(name, out var old)) return;
            if (mappedBuffers.Remove(name, out IntPtr host))
            {
                cuda.ReleaseMappedBuffer(host);
                Add("mapped_host_bytes", -old.Size);
            }
            else
            {
                cuda.ReleaseBuffer(old.Pointer);
                DeviceBufferBytes -= old.Size;
            }
            Add("buffer_free_count", 1);
            UpdateMappedMetrics();
        }

        private void UpdateMappedMetrics()
        {
            metrics["mapped_buffer_names"] = mappedBuffers.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            long outputs = mappedBuffers.Keys.Where(AllOutputs.Contains).Sum(n => buffers[n].Size);
            metrics["mapped_output_bytes"] = outputs;
            Peak("peak_mapped_output_bytes", outputs);
            long scratch = mappedBuffers.Keys.Where(ScratchArrays.Contains).Sum(n => buffers[n].Size);
            metrics["mapped_scratch_bytes"] = scratch;
            Peak("peak_mapped_scratch_bytes", scratch);
        }

        private void SetStatePointer(string name, ulong pointer)
        {
            if (state.Has(name)) state.SetPointer(name, pointer);
        }

        private ulong Memory(string name, long size, bool window = false, bool forceMapped = false)
        {
            size = Math.Max(size, 1);
            buffers.TryGetValue(name, out var old);
            long limit = window ? deviceBudget : Math.Max(0, deviceBudget - windowReserve);
            bool mapped = mappedBuffers.ContainsKey(name);
            long needed = old != null && old.Size >= size ? 0 : size - (old != null && !mapped ? old.Size : 0);
            if (!window && !mapped && !forceMapped && !WindowArrays.Contains(name) && DeviceBufferBytes + needed > limit)
            {
                // Completed windows must not force persistent fields into mapped storage.
                if (old != null) { Release(name); old = null; needed = size; }
                foreach (string idle in ResidualArrays.Concat(ScratchArrays))
                {
                    Release(idle);
                    if (ScratchArrays.Contains(idle)) state.SetPointer(idle, 0);
                }
                var restore = new List<(string Name, int Width)>();
                foreach (string idle in ParticleArrays)
                {
                    if (DeviceBufferBytes + needed + restore.Sum(r => (long)r.Width) <= limit) break;
                    if (!buffers.ContainsKey(idle) || !hostArrays.ContainsKey(idle)) continue;
                    int width = DtypeSize(idle);
                    if (buffers[idle].Size <= width) continue;
                    Release(idle);
                    restore.Add((idle, width));
                }
                foreach (var (idle, width) in restore)
                {
                    ulong p = Memory(idle, width);
                    state.SetPointer(idle, p);
                    Upload(idle, hostArrays[idle], 0, width);
                }
            }
            if (old != null && old.Size >= size && (mapped || (!forceMapped && DeviceBufferBytes <= limit)))
            {
                Add("buffer_reuse_count", 1);
                return old.Pointer;
            }
            if (old != null) Release(name);
            ulong pointer = 0;
            bool allocated = false;
            if (!forceMapped && DeviceBufferBytes + size <= limit)
            {
                try { pointer = cuda.Buffer(size); allocated = true; }
                catch (GpuCudaException error) when (error.Code == GpuCuda.ErrorOutOfMemory)
                {
                    Add("device_allocation_oom", 1);
                    if (window) throw;
                }
            }
            else if (window && !forceMapped)
                throw new GpuCudaException(GpuCuda.ErrorOutOfMemory, "particle window exceeds device memory budget", "CUDA_ERROR_OUT_OF_MEMORY");
            if (!allocated)
            {
                var (host, device) = cuda.MappedBuffer(size);
                pointer = device;
                mappedBuffers[name] = host;
                Add("mapped_host_bytes", size);
                Peak("peak_mapped_host_bytes", (long)metrics["mapped_host_bytes"]);
            }
            else
            {
                DeviceBufferBytes += size;
                Peak("peak_device_buffer_bytes", DeviceBufferBytes);
            }
            buffers[name] = new Buffer { Pointer = pointer, Size = size };
            Add("buffer_allocation_count", 1);
            UpdateMappedMetrics();
            return pointer;
        }

        private void Upload(string name, byte[] data, int start, int length, long offset = 0)
        {
            if (length == 0) return;
            var clock = Stopwatch.StartNew();
            fixed (byte* source = &data[start])
            {
                if (mappedBuffers.TryGetValue(name, out IntPtr host))
                {
                    // One GPU owns updates between launches; the host touches this memory only after synchronization.
                    System.Buffer.MemoryCopy(source, (byte*)host + offset, length, length);
                    Add("mapped_host_copy_bytes", length);
                    AddMs("mapped_host_copy_ms", clock);
                }
                else
                {
                    cuda.CopyToDevice(buffers[name].Pointer + (ulong)offset, source, length, "cuMemcpyHtoD " + name);
                    Add("h2d_bytes", length);
                    AddMs("h2d_ms", clock);
                }
            }
        }

        private void Zero(string name, long size)
        {
            var clock = Stopwatch.StartNew();
            if (mappedBuffers.TryGetValue(name, out IntPtr host)) NativeMemoryClear(host, size);
            else cuda.MemsetD8(buffers[name].Pointer, size);
            Add("zero_fill_bytes", size);
            Add("zero_fill_count", 1);
            AddMs("zero_fill_ms", clock);
        }

        private static void NativeMemoryClear(IntPtr host, long size)
        {
            byte* p = (byte*)host;
            while (size > 0) { int part = (int)Math.Min(size, int.MaxValue); new Span<byte>(p, part).Clear(); p += part; size -= part; }
        }

        private long PrepareParticleWindows(string[] names, long count, bool fresh)
        {
            // All allocations finish before a trajectory can mutate shared output.
            RefreshMemoryBudget();
            long retained = buffers.Where(p => WindowArrays.Contains(p.Key) && !mappedBuffers.ContainsKey(p.Key)).Sum(p => p.Value.Size);
            long zeroBytes = fresh ? 0 : ParticleArrays.Where(hostArrays.ContainsKey).Sum(n => (long)DtypeSize(n));
            if (!fresh) SpillForWindow(43 + 24 - retained);
            long unit = names.Sum(n => (long)DtypeSize(n));
            long receptorColumns = (long)state.GetInt("ReceptorNumber") + 1, sliceColumns = state.GetInt("NS");
            long scratchUnit = receptorColumns * 8 + sliceColumns * 4;
            if (receptorColumns < 1 || receptorColumns > Int32Max || sliceColumns < 1 || sliceColumns > Int32Max) throw new ArgumentException("Invalid particle scratch dimensions");
            long extra = unit * (fresh ? 1 : 0);
            long minimum = unit + extra;
            bool mappedScratch = minimum + scratchUnit + (fresh ? 0 : 43) > deviceBudget;
            long perRow = unit + (mappedScratch ? 0 : scratchUnit);
            long target = Math.Min(windowReserve, Math.Min(count, MaxMemoryParticles) * perRow + extra);
            // Plan the same rows as releasing all windows; actual allocations still count retained bytes.
            SpillForWindow(Math.Max(perRow + extra, target) + zeroBytes - retained);
            long available = Math.Max(0, deviceBudget - DeviceBufferBytes + retained - zeroBytes);
            long capacity = Math.Min(Math.Min(MaxMemoryParticles, count), Math.Max(0, FloorDiv(available - extra, perRow)));
            // Host-mapped scratch and windows are bounded by a pinned host-memory budget.
            long pinnedBudget = MemorySetting("pinned_budget_bytes", DefaultPinnedBudget, 0);
            if (mappedScratch) capacity = Math.Min(capacity, Math.Max(1, pinnedBudget / scratchUnit));
            // When not even one particle window fits in device memory, use mapped host memory for the window arrays too.
            bool mappedWindows = capacity < 1 && count > 0;
            if (mappedWindows)
            {
                mappedScratch = true;
                capacity = Math.Min(Math.Min(MaxMemoryParticles, count), Math.Max(1, FloorDiv(pinnedBudget - extra, unit + scratchUnit)));
            }
            var required = new List<(string Name, long Size, bool Mapped)>();
            foreach (string name in names) required.Add((name, (capacity + (fresh ? 1 : 0)) * DtypeSize(name), mappedWindows));
            required.Add((ScratchArrays[0], capacity * receptorColumns * 8, mappedScratch));
            required.Add((ScratchArrays[1], capacity * sliceColumns * 4, mappedScratch));
            if (!fresh) foreach (string name in ParticleArrays) if (hostArrays.ContainsKey(name)) required.Add((name, DtypeSize(name), mappedWindows));
            long projected = DeviceBufferBytes;
            var replace = new List<string>();
            foreach (var (name, size, mapped) in required)
            {
                buffers.TryGetValue(name, out var old);
                bool wasMapped = mappedBuffers.ContainsKey(name);
                if (old != null && old.Size >= size && wasMapped == mapped) continue;
                replace.Add(name);
                projected += (mapped ? 0 : size) - (old != null && !wasMapped ? old.Size : 0);
            }
            foreach (string name in projected > deviceBudget ? WindowArrays : replace.ToArray()) Release(name);
            while (capacity > 0)
            {
                try
                {
                    if (!fresh)
                        foreach (string name in ParticleArrays)
                        {
                            if (!hostArrays.ContainsKey(name)) continue;
                            int width = DtypeSize(name);
                            ulong p = Memory(name, width, window: true, forceMapped: mappedWindows);
                            state.SetPointer(name, p);
                            Upload(name, hostArrays[name], 0, width);
                        }
                    foreach (string name in names)
                    {
                        ulong p = Memory(name, (capacity + (fresh ? 1 : 0)) * DtypeSize(name), window: true, forceMapped: mappedWindows);
                        SetStatePointer(name, p);
                    }
                    for (int s = 0; s < ScratchArrays.Length; s++)
                    {
                        long columns = s == 0 ? receptorColumns : sliceColumns;
                        long size = checked(capacity * columns * (s == 0 ? 8 : 4));
                        ulong p = Memory(ScratchArrays[s], size, window: true, forceMapped: mappedScratch);
                        state.SetPointer(ScratchArrays[s], p);
                        state.SetDims(ScratchArrays[s], new[] { (int)capacity, (int)columns });
                    }
                    metrics["particle_scratch_bytes_per_row"] = scratchUnit;
                    metrics["particle_scratch_capacity"] = capacity;
                    metrics["particle_scratch_mapped"] = mappedScratch;
                    metrics["particle_windows_mapped"] = mappedWindows;
                    metrics["pinned_budget_bytes"] = pinnedBudget;
                    return capacity;
                }
                catch (GpuCudaException error) when (error.Code == GpuCuda.ErrorOutOfMemory)
                {
                    foreach (string name in WindowArrays) Release(name);
                    if (capacity == 1 && !mappedScratch) mappedScratch = true;
                    else if (capacity == 1 && !mappedWindows) mappedWindows = true;
                    else capacity /= 2;
                    Add("particle_window_retries", 1);
                }
            }
            throw new InvalidOperationException("GPU memory cannot hold one particle window; results unavailable; no CPU trajectory fallback");
        }

        private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        private void SpillForWindow(long needed)
        {
            var candidates = buffers.Where(p => !mappedBuffers.ContainsKey(p.Key) && !WindowArrays.Contains(p.Key))
                                    .Select(p => (Size: p.Value.Size, Name: p.Key))
                                    .OrderByDescending(c => c.Size).ThenByDescending(c => c.Name, StringComparer.Ordinal).ToList();
            foreach (var (size, name) in candidates)
            {
                if (DeviceBufferBytes + needed <= deviceBudget) break;
                ulong old = buffers[name].Pointer;
                var (host, device) = cuda.MappedBuffer(size);
                try
                {
                    var clock = Stopwatch.StartNew();
                    cuda.CopyToHost((void*)host, old, size, "spill " + name);
                    AddMs("d2h_ms", clock);
                    Add("d2h_bytes", size);
                }
                catch
                {
                    cuda.ReleaseMappedBuffer(host);
                    throw;
                }
                cuda.ReleaseBuffer(old);
                buffers[name] = new Buffer { Pointer = device, Size = size };
                mappedBuffers[name] = host;
                DeviceBufferBytes -= size;
                Add("mapped_host_bytes", size);
                Peak("peak_mapped_host_bytes", (long)metrics["mapped_host_bytes"]);
                Add("buffer_allocation_count", 1);
                Add("buffer_free_count", 1);
                SetStatePointer(name, device);
                UpdateMappedMetrics();
            }
        }

        // ------------------------------------------------------------------ frames

        private static bool JsonInt(JsonElement e, out long value)
        {
            value = 0;
            if (e.ValueKind != JsonValueKind.Number) return false;
            string raw = e.GetRawText();
            if (raw.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0) return false;
            return long.TryParse(raw, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private void ReadExact(byte[] buffer, int count)
        {
            var clock = Stopwatch.StartNew();
            input.ReadExactly(buffer, 0, count);
            AddMs("pipe_read_wait_ms", clock);
        }

        private JsonDocument ReadHeader()
        {
            var length = new byte[8];
            int got = 0;
            while (got < 8)
            {
                int n = input.Read(length, got, 8 - got);
                if (n == 0)
                {
                    if (got == 0) return null;
                    throw new EndOfStreamException("Unexpected frame EOF at " + got + "/8");
                }
                got += n;
            }
            ulong size = BinaryPrimitives.ReadUInt64LittleEndian(length);
            if (size > 16 * 1024 * 1024) throw new InvalidDataException("Header too large");
            var raw = new byte[size];
            input.ReadExactly(raw, 0, (int)size);
            return JsonDocument.Parse(raw);
        }

        // Relaxed escaping keeps quotes and comparison signs readable in error text.
        private static readonly JsonSerializerOptions HeaderOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private void WriteFrame(Dictionary<string, object> header, IEnumerable<ReadOnlyMemory<byte>> payload = null)
        {
            long expected = Convert.ToInt64(header["payload_bytes"], System.Globalization.CultureInfo.InvariantCulture);
            byte[] raw = JsonSerializer.SerializeToUtf8Bytes(header, HeaderOptions);
            var length = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(length, (ulong)raw.Length);
            bool started = false;
            try
            {
                started = true;
                output.Write(length);
                output.Write(raw);
                long remaining = expected;
                if (payload != null)
                    foreach (var chunk in payload)
                    {
                        if (chunk.Length > remaining) throw new InvalidDataException("Response payload exceeds declared length");
                        remaining -= chunk.Length;
                        output.Write(chunk.Span);
                    }
                if (remaining != 0) throw new EndOfStreamException("Response payload ended " + remaining + " bytes early");
                output.Flush();
            }
            catch (Exception error) when (started) { throw new FrameWriteException(error); }
        }

        private void Receive(JsonElement header, string command)
        {
            if (!header.TryGetProperty("payload_bytes", out var totalElement) || !JsonInt(totalElement, out long total) || total < 0)
                throw new ArgumentException("Payload length must be a nonnegative integer");
            var list = new List<Descriptor>();
            long cursor = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            // Validate the complete frame before consuming bytes or changing device state.
            if (header.TryGetProperty("arrays", out var arrays))
                foreach (JsonElement d in arrays.EnumerateArray())
                {
                    string name = d.GetProperty("name").GetString();
                    JsonElement bytesElement = d.GetProperty("bytes"), offsetElement = d.GetProperty("offset");
                    if (!arrayCpp.ContainsKey(name) || !seen.Add(name)) throw new ArgumentException("Unknown input array: " + name);
                    if (!JsonInt(bytesElement, out long size) || size < 0 || !JsonInt(offsetElement, out long offset) || offset != cursor)
                        throw new ArgumentException("Array frame offset/length mismatch: " + name);
                    bool zeroFill = d.TryGetProperty("zero_fill", out var zeroElement);
                    bool isNull = d.TryGetProperty("is_null", out var nullElement) && nullElement.ValueKind == JsonValueKind.True;
                    if (d.TryGetProperty("is_null", out nullElement) && nullElement.ValueKind != JsonValueKind.True && nullElement.ValueKind != JsonValueKind.False)
                        throw new ArgumentException("Invalid array shape/null payload: " + name);
                    string dtype = d.TryGetProperty("dtype", out var dtypeElement) && dtypeElement.ValueKind == JsonValueKind.String ? dtypeElement.GetString() : null;
                    if (zeroFill && (zeroElement.ValueKind != JsonValueKind.True || command != "init" || name != "Conz5d" || dtype != "Single" ||
                                     !(d.TryGetProperty("is_null", out var zn) && zn.ValueKind == JsonValueKind.False) || size == 0))
                        throw new ArgumentException("Invalid zero_fill descriptor: " + name);
                    long wireSize = zeroFill ? 0 : size;
                    if (cursor + wireSize > total) throw new ArgumentException("Payload array exceeds frame length: " + name);
                    var shape = new List<int>();
                    bool validShape = true;
                    if (d.TryGetProperty("shape", out var shapeElement))
                        foreach (JsonElement dim in shapeElement.EnumerateArray())
                        {
                            if (!JsonInt(dim, out long value) || value < 0 || value > Int32Max) validShape = false;
                            else shape.Add((int)value);
                        }
                    var info = GpuStateLayout.ArrayInfo(arrayCpp[name]).Value;
                    if (!isNull)
                    {
                        string expected = wireDtypes.TryGetValue(name, out string wire) ? wire : GpuStateLayout.DtypeOf(info.Element);
                        if (dtype != expected || (shapeElement.ValueKind == JsonValueKind.Array ? shapeElement.GetArrayLength() : 0) != info.Rank)
                            throw new ArgumentException("Array dtype/rank mismatch: " + name);
                    }
                    if (!validShape || (isNull && size != 0)) throw new ArgumentException("Invalid array shape/null payload: " + name);
                    if (!isNull)
                    {
                        System.Numerics.BigInteger product = 1;
                        foreach (int dim in shape) product *= dim;
                        if (product * GpuStateLayout.SizeOfDtype(dtype) != size || (doubleStorage.Contains(name) && size > long.MaxValue / 2))
                            throw new ArgumentException("Shape/bytes mismatch: " + name);
                    }
                    cursor += wireSize;
                    var props = new List<KeyValuePair<string, JsonElement>>();
                    foreach (var p in d.EnumerateObject()) if (p.Name != "zero_fill") props.Add(new KeyValuePair<string, JsonElement>(p.Name, p.Value.Clone()));
                    list.Add(new Descriptor { Name = name, Bytes = size, Dtype = dtype, Shape = shape.ToArray(), IsNull = isNull, ZeroFill = zeroFill, Properties = props });
                }
            if (cursor != total) throw new ArgumentException("Payload length mismatch");
            var scalars = new List<(string, JsonElement)>();
            if (header.TryGetProperty("scalars", out var scalarObject))
                foreach (var p in scalarObject.EnumerateObject())
                {
                    if (!inputScalars.Contains(p.Name)) throw new ArgumentException("Unknown input scalar: " + p.Name);
                    if (!state.ValidScalar(p.Name, p.Value)) throw new ArgumentException("Invalid scalar value: " + p.Name);
                    scalars.Add((p.Name, p.Value.Clone()));
                }
            foreach (var d in list) Apply(d);
            foreach (var (name, value) in scalars) state.SetScalar(name, value);
        }

        private void Apply(Descriptor d)
        {
            string name = d.Name;
            long size = d.Bytes;
            if (d.ZeroFill)
            {
                ulong p = Memory(name, size);
                Zero(name, size);
                descriptors[name] = d;
                state.SetPointer(name, p);
                state.SetDims(name, d.Shape);
                return;
            }
            bool retained = ParticleArrays.Contains(name) || ResidualArrays.Contains(name);
            bool validated = ValidatedArrays.Contains(name);
            if (size > 0 && !d.IsNull && !retained && !validated)
            {
                int factor = doubleStorage.Contains(name) ? 2 : 1;
                ulong p = Memory(name, checked(size * factor));
                int transferSize = TransferBytes / factor;
                var converted = factor == 2 ? new byte[TransferBytes] : null;
                for (long offset = 0; offset < size; offset += transferSize)
                {
                    int part = (int)Math.Min(transferSize, size - offset);
                    Canceled();
                    ReadExact(transfer, part);
                    if (factor == 2)
                    {
                        int n = part / 4;
                        for (int i = 0; i < n; i++)
                        {
                            float value = BinaryPrimitives.ReadSingleLittleEndian(transfer.AsSpan(4 * i));
                            if (!float.IsFinite(value)) throw new ArgumentException("nonfinite Single input: " + name);
                            BinaryPrimitives.WriteDoubleLittleEndian(converted.AsSpan(8 * i), value);
                        }
                        Upload(name, converted, 0, 2 * part, offset * factor);
                    }
                    else Upload(name, transfer, 0, part, offset);
                }
                descriptors[name] = d;
                state.SetPointer(name, p);
                state.SetDims(name, d.Shape);
                return;
            }
            var data = new byte[size];
            if (size > 0) ReadExact(data, (int)size);
            if (size > 0)
            {
                if (name == "TransientDepoMode" || name == "ParticleMode")
                {
                    bool any = false;
                    int width = name == "TransientDepoMode" ? 4 : 1;
                    for (long i = 0; i < size / width; i++)
                    {
                        long value = width == 4 ? BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan((int)(4 * i))) : data[i];
                        if (value < 0 || value > 2) throw new ArgumentException("Invalid deposition mode: " + name);
                        any |= value != 0;
                    }
                    activeDepositionArrays[name] = any;
                }
                if (name == "DecayRate" || name == "TransientDepoVdep" || name == "TransientDepoVsed" || name == "ParticleVdep" || name == "ParticleVsed")
                {
                    bool isDouble = d.Dtype == "Double";
                    int width = isDouble ? 8 : 4;
                    for (long i = 0; i < size / width; i++)
                    {
                        double value = isDouble ? BinaryPrimitives.ReadDoubleLittleEndian(data.AsSpan((int)(8 * i))) : BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan((int)(4 * i)));
                        if (!double.IsFinite(value) || (name != "DecayRate" && value < 0)) throw new ArgumentException("Invalid physical input: " + name);
                    }
                }
            }
            if (d.IsNull || size == 0)
            {
                if (name == "TransientDepoMode" || name == "ParticleMode") activeDepositionArrays[name] = false;
                if (state.Has(name)) { state.SetPointer(name, 0); state.SetDims(name, new int[state.Get(name).Rank]); }
                descriptors[name] = d;
                Release(name);
                hostArrays.Remove(name);
                validationArrays.Remove(name);
                return;
            }
            descriptors[name] = d;
            ulong pointer;
            if (retained)
            {
                hostArrays[name] = data;
                if (ResidualArrays.Contains(name)) return;
                int elementSize = GpuStateLayout.SizeOfDtype(d.Dtype);
                pointer = Memory(name, elementSize, window: true);
                Upload(name, data, 0, elementSize);
            }
            else
            {
                if (name == "WetDepoDropVelocity" || name == "DZK") validationArrays[name] = data;
                pointer = Memory(name, size);
                Upload(name, data, 0, (int)size);
            }
            if (state.Has(name))
            {
                if (d.Shape.Length != state.Get(name).Rank) throw new ArgumentException("Array rank mismatch: " + name);
                state.SetPointer(name, pointer);
                state.SetDims(name, d.Shape);
            }
        }

        private void CheckInitFields(JsonElement header)
        {
            var given = new HashSet<string>(StringComparer.Ordinal);
            if (header.TryGetProperty("scalars", out var scalars)) foreach (var p in scalars.EnumerateObject()) given.Add(p.Name);
            var missing = requiredInitScalars.Where(n => !given.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (missing.Count > 0) throw new ArgumentException("Missing scalar fields: " + string.Join(",", missing));
        }

        // ------------------------------------------------------------------ guard

        private int I(string name) => state.GetInt(name);
        private double R(string name) => state.GetReal(name);
        private bool B(string name) => state.GetBool(name);

        private void Guard()
        {
            if ((I("Topo") != 0 && I("Topo") != 1) || !new[] { 0, 2, 3, 4 }.Contains(I("IStatistics"))) throw new ArgumentException("Unsupported topography or meteorology mode");
            if (I("ISTATIONAER") != 0 && I("ISTATIONAER") != 1) throw new ArgumentException("Unsupported runtime mode");
            if (I("NS") < 1 || I("ReceptorNumber") < 0) throw new ArgumentException("Invalid slice or receptor count");
            if (!B("UseFixedRndSeedVal") && (state.GetUInt("RnGSeed1") == 0 || state.GetUInt("RnGSeed2") == 0)) throw new ArgumentException("Nonfixed GPU execution requires nonzero master seeds");
            if (B("WetDeposition") && B("WetDopoDropDrift"))
            {
                descriptors.TryGetValue("WetDepoDropVelocity", out var descriptor);
                validationArrays.TryGetValue("WetDepoDropVelocity", out byte[] raw);
                if (descriptor == null || !descriptor.Shape.SequenceEqual(new[] { 10, 9 }) || raw == null) throw new ArgumentException("Wet droplet velocity table must have shape [10, 9]");
                bool single = descriptor.Dtype == "Single";
                int width = single ? 4 : 8;
                var values = new double[raw.Length / width];
                for (int i = 0; i < values.Length; i++) values[i] = single ? BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(4 * i)) : BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(8 * i));
                bool bad = values.Any(v => !double.IsFinite(v));
                for (int r = 0; r < 10 && !bad; r++)
                {
                    var row = values.Skip(r * 9).Take(9).ToArray();
                    if (row[0] <= 0 || row.Skip(1).Take(4).Any(v => v <= 0) || row.Skip(5).Any(v => v < 0)) bad = true;
                    if (r < 9 && values[r * 9] >= values[(r + 1) * 9]) bad = true;
                }
                if (bad) throw new ArgumentException("Invalid wet droplet velocity table values");
                if (!double.IsFinite(R("WetDepoPrecipitation")) || R("WetDepoPrecipitation") < 0 || !(R("WetDepoRW") >= 0 && R("WetDepoRW") <= 1))
                    throw new ArgumentException("Invalid wet droplet precipitation or removal probability");
                if (!validationArrays.TryGetValue("DZK", out byte[] dz)) throw new ArgumentException("Wet droplet drift requires DZK");
                int layers = dz.Length / 4, nkk = I("NKK");
                bool badLayer = layers <= nkk;
                for (int k = 1; k <= nkk && !badLayer; k++) { float v = BinaryPrimitives.ReadSingleLittleEndian(dz.AsSpan(4 * k)); if (!float.IsFinite(v) || v <= 0) badLayer = true; }
                if (badLayer) throw new ArgumentException("Invalid positive wet droplet layer thickness");
            }
            if (I("LogLevel") < 0 || I("LogLevel") > 3) throw new ArgumentException("Unsupported diagnostic log level");
            if (activeDepositionArrays.Values.Any(v => v) && !B("DepositionExist")) throw new ArgumentException("Active dry-deposition mode requires DepositionExist");
            foreach (string name in new[] { "NII", "NJJ", "NKK", "NXL", "NYL" }) if (I(name) <= 0) throw new ArgumentException("Invalid grid size: " + name);
            foreach (string name in new[] { "DXK", "DYK", "GralDx", "GralDy", "GralDz", "TAUS" }) if (!double.IsFinite(R(name)) || R(name) <= 0) throw new ArgumentException("Invalid positive physical scalar: " + name);
            foreach (string name in new[] { "AdaptiveRoughnessMax", "WetDepoRW", "VegetationDepoGasFactor", "VegetationDepoPMFactor" })
                if (!double.IsFinite(R(name)) || R(name) < 0) throw new ArgumentException("Invalid nonnegative physical scalar: " + name);

            void Require(string name, params long[] shape)
            {
                if (state.GetPointer(name) == 0) throw new ArgumentException("Missing or undersized required array: " + name);
                int[] dims = state.GetDims(name);
                for (int i = 0; i < Math.Min(dims.Length, shape.Length); i++)
                    if (dims[i] < shape[i]) throw new ArgumentException("Missing or undersized required array: " + name);
            }
            Require("SourceGroups", 1);
            long groups = state.GetDim("SourceGroups", 0);
            long fi = I("NII") + 2L, fj = I("NJJ") + 2L, ci = I("NXL") + 2L, cj = I("NYL") + 2L;
            long ns = I("NS"), nkkFlow = I("NKK"), profiles = I("MetProfileNumb");
            Require("AHK", fi, fj);
            Require("KKART", fi, fj);
            Require("HorSlices", ns);
            Require("SourceType", 1); // residual IntStandCalculate reads the inactive index zero
            Require("Conz3dDouble", ci, cj, ns, groups);
            if (I("Topo") == 1 || B("BuildingsExist"))
            {
                Require("UK", fi, fj, nkkFlow + 1);
                Require("VK", fi, fj, nkkFlow + 1);
                Require("WK", fi, fj, nkkFlow + 2);
                Require("HOKART", nkkFlow + 1);
                Require("DZK", nkkFlow + 1);
                Require("CUTK", fi, fj);
            }
            if ((I("Topo") == 0 && !B("BuildingsExist")) || I("IStatistics") == 0 || I("IStatistics") == 3)
            {
                if (profiles < 1) throw new ArgumentException("Invalid meteorology profile count");
                Require("MeasurementHeight", profiles + 1);
            }
            if (I("Topo") == 0 && !B("BuildingsExist")) { Require("ObsWindU", profiles + 1); Require("ObsWindV", profiles + 1); }
            if (I("IStatistics") == 0 || I("IStatistics") == 3) { Require("U0", profiles + 1); Require("V0", profiles + 1); }
            if (I("ISTATIONAER") == 0)
            {
                long transient = I("NKK_Transient");
                if (transient < 1) throw new ArgumentException("Invalid transient layer count");
                Require("Conz5d", fi, fj, transient + 2, groups);
                Require("HoKartTrans", transient + 1);
                Require("DZK_Trans", transient + 1);
                foreach (string name in new[] { "TransientDepoMode", "TransientDepoVdep", "TransientDepoVsed" }) Require(name, groups);
            }
            if (B("DepositionExist")) Require("COV", fi, fj);
            if (B("DepositionExist") || B("WetDeposition")) Require("Depo_conz", ci, cj, groups);
            if (B("Odour")) { Require("Conz3dp", ci, cj, ns, groups); Require("Conz3dm", ci, cj, ns, groups); }
            if (B("WriteVerticalConcentration"))
            {
                if (I("ISTATIONAER") != 0) throw new ArgumentException("Vertical concentration output requires transient mode");
                Require("ConzSsum", fi, fj, I("NKK_Transient") + 2L);
            }
            if (B("TunnelEntr")) Require("TUN_ENTR", fi, fj);
            if (B("TunnelOppLane")) Require("OPP_LANE", fi, fj);
            if (R("AdaptiveRoughnessMax") > 0) foreach (string name in new[] { "OLGral", "USternGral", "Z0Gral" }) Require(name, fi, fj);
            if (I("LogLevel") != 0) { Require("LogReflexions", 6); Require("Log_Timesteps", 6); }
            if (B("ReceptorsAvailable"))
            {
                long receptors = I("ReceptorNumber") + 1L;
                foreach (string name in new[] { "ReceptorX", "ReceptorY", "ReceptorZ", "ReceptorIInd", "ReceptorJInd", "ReceptorNearbyBuilding" }) Require(name, receptors);
                Require("ReceptorConc", receptors, groups);
                Require("ReceptorParticleMaxConc", receptors, groups);
            }
            if (I("GridRecRow") != 0)
            {
                if (I("GridRecRow") != I("NYL") + 1 || state.GetPointer("GridRecStart") == 0) throw new ArgumentException("Invalid receptor CSR row/start");
                if (state.GetDim("GridRecStart", 0) != (I("NXL") + 1L) * I("GridRecRow") + 1) throw new ArgumentException("Invalid receptor CSR dimensions");
            }
            if (state.GetPointer("Trace") != 0 && state.GetDim("Trace", 1) != 8) throw new ArgumentException("Fresh trace requires 8 columns");
            if (state.GetPointer("ResidualTrace") != 0 && state.GetDim("ResidualTrace", 1) != 16) throw new ArgumentException("Residual trace requires 16 columns");
        }

        // ------------------------------------------------------------------ execution

        private void Bind(bool specializeKernel = true)
        {
            CheckDeviceError();
            if (specializeKernel) EnsureSpecialized();
            fixed (byte* p = state.Data)
            {
                cuda.CopyToDevice(stateAddress, p, state.Size, "bind State");
                // The host error fields are zero here (CheckDeviceError), so this also clears the HIP status symbol.
                if (cuda.Hip) { var (start, size) = StatusRange(); cuda.CopyToDevice(hipStatusAddress, p + start, size, "bind HIP status"); }
            }
            if (reproSums) BindReproSums();
        }

        /// <summary>Current address of a slot's word buffer (SpillForWindow can move it to mapped host memory).</summary>
        private ulong ReproWords(int slot) => buffers["_repro_" + ReproSlots[slot]].Pointer;

        /// <summary>Writes the word buffers and the bound output arrays to the loaded module's gral_repro constant.</summary>
        private void BindReproSums()
        {
            byte* data = stackalloc byte[ReproConstantBytes];
            var span = new Span<byte>(data, ReproConstantBytes);
            for (int s = 0; s < ReproSlots.Length; s++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8 * s), reproWordElements[s] > 0 ? ReproWords(s) : 0);
                BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(64 + 8 * s), reproWordElements[s] > 0 ? state.GetPointer(ReproSlots[s]) : 0);
            }
            cuda.CopyToDevice(reproAddress, data, ReproConstantBytes, "bind gral_repro");
        }

        /// <summary>
        /// After every init: gives every bound output array a zeroed buffer of three 64-bit words per element, placed by the
        /// memory manager like the other device arrays. The words are zero between a result and the next init
        /// (gral_repro_finalize clears what it adds).
        /// </summary>
        private void PrepareReproSums()
        {
            if (!B("UseFixedRndSeedVal")) throw new InvalidOperationException("Reproducible sums require fixed random seeds");
            for (int s = 0; s < ReproSlots.Length; s++)
            {
                string name = ReproSlots[s], key = "_repro_" + name;
                long elements = 0;
                if (descriptors.TryGetValue(name, out var d) && !d.IsNull && d.Bytes > 0 && state.GetPointer(name) != 0)
                    elements = d.Bytes / GpuStateLayout.SizeOfDtype(d.Dtype);
                if (elements == reproWordElements[s]) continue;
                Release(key);
                metrics["repro_word_bytes"] = (long)metrics["repro_word_bytes"] - 24 * reproWordElements[s];
                reproWordElements[s] = 0;
                if (elements == 0) continue;
                Memory(key, 24 * elements);
                Zero(key, 24 * elements);
                reproWordElements[s] = elements;
                metrics["repro_word_bytes"] = (long)metrics["repro_word_bytes"] + 24 * elements;
            }
        }

        /// <summary>Adds each slot's integer totals once to its output array (in device memory or mapped host memory).</summary>
        private void FinalizeReproSums()
        {
            var clock = Stopwatch.StartNew();
            ulong words = 0, output = 0, count = 0;
            int single = 0;
            void** args = stackalloc void*[4];
            args[0] = &words; args[1] = &output; args[2] = &count; args[3] = &single;
            for (int s = 0; s < ReproSlots.Length; s++)
            {
                if (reproWordElements[s] == 0) continue;
                string name = ReproSlots[s];
                words = ReproWords(s); output = state.GetPointer(name);
                count = (ulong)reproWordElements[s];
                single = doubleStorage.Contains(name) || GpuStateLayout.SizeOfDtype(descriptors[name].Dtype) == 8 ? 0 : 1;
                Launch(fnReproFinalize, reproWordElements[s], args, 256);
            }
            metrics["repro_finalize_ms"] = (double)metrics["repro_finalize_ms"] + clock.Elapsed.TotalMilliseconds;
        }

        private void ReadState()
        {
            var clock = Stopwatch.StartNew();
            fixed (byte* p = state.Data)
            {
                cuda.CopyToHost(p, stateAddress, state.Size, "read State");
                if (cuda.Hip) { var (start, size) = StatusRange(); cuda.CopyToHost(p + start, hipStatusAddress, size, "read HIP status"); }
            }
            AddMs("state_d2h_ms", clock);
            Add("state_d2h_bytes", state.Size);
        }

        /// <summary>Byte range of DeviceError..DeviceErrorParticle in the State layout.</summary>
        private (int Start, int Size) StatusRange()
        {
            int start = state.Get("DeviceError").Offset;
            return (start, state.Get("DeviceErrorParticle").Offset + 4 - start);
        }

        private void CheckDeviceError()
        {
            int error = I("DeviceError");
            if (error == 0) return;
            string detail = error switch
            {
                2 => "droplet state invalid", 3 => "wet-deposition droplet did not reach ground (updraft >= fall speed)",
                5 => "reproducible sums: an addend is not finite or its magnitude is 2^62 or more (INU_GPU_REPRODUCIBLE_SUMS=0 turns reproducible sums off)",
                6 => "reproducible sums: a total exceeds the 64-bit integer range (INU_GPU_REPRODUCIBLE_SUMS=0 turns reproducible sums off)",
                _ => "nonfinite trajectory"
            };
            long particle = I("DeviceErrorParticle");
            if (I("DeviceErrorPhase") == 2 && particle < 0)
            {
                long relative = particle - Int32Min;
                if (residualErrorMapping == null || relative < 0 || relative >= residualErrorMapping.Value.Count)
                    throw new InvalidOperationException("Invalid residual device error mapping; whole run aborted; results unavailable");
                particle = residualErrorMapping.Value.Base + relative;
            }
            throw new InvalidOperationException("GPU " + detail + "; phase=" + I("DeviceErrorPhase") + ", particle=" + particle + "; whole run aborted; results unavailable");
        }

        // A HIP trap ends only its wavefront and the launch still succeeds; see GpuCuda.HipSource.
        private void CheckHipTrap()
        {
            int trapped;
            cuda.CopyToHost(&trapped, hipTrapAddress, sizeof(int), "read HIP trap flag");
            if (trapped != 0)
                throw new InvalidOperationException("GPU kernel reached a trap (Math.Abs of int.MinValue or Math.Sign of NaN, which throw on the CPU); whole run aborted; results unavailable");
        }

        private double Launch(IntPtr function, long count, void** args, int block)
        {
            var clock = Stopwatch.StartNew();
            cuda.RecordEvent(eventStart, "record start");
            cuda.Launch(function, (uint)((count + block - 1) / block), (uint)block, args, "cuLaunchKernel");
            cuda.RecordEvent(eventEnd, "record end");
            cuda.SynchronizeEvent(eventEnd);
            float elapsed = cuda.ElapsedMs(eventStart, eventEnd);
            metrics["kernel_device_ms"] = (double)metrics["kernel_device_ms"] + elapsed;
            Add("kernel_launches", 1);
            metrics["kernel_max_device_ms"] = Math.Max((double)metrics["kernel_max_device_ms"], elapsed);
            lastKernelMs = elapsed;
            var (statusStart, statusSize) = StatusRange();
            var download = Stopwatch.StartNew();
            fixed (byte* p = state.Data)
                cuda.CopyToHost(p + statusStart, cuda.Hip ? hipStatusAddress : stateAddress + (ulong)statusStart, statusSize, "read kernel status");
            AddMs("status_d2h_ms", download);
            Add("status_d2h_bytes", statusSize);
            CheckDeviceError();
            if (cuda.Hip) CheckHipTrap();
            double total = clock.Elapsed.TotalMilliseconds;
            kernelTotalMs += total;
            return total;
        }

        private Dictionary<string, object> Execute(JsonElement header)
        {
            CheckDeviceError();
            executionFailed = true;
            if (!JsonInt(header.GetProperty("count"), out long count) || count < 0 || count > Int32Max) throw new ArgumentException("Particle count must be an integer in [0, INT32_MAX]");
            if (!JsonInt(header.GetProperty("first"), out long first) || first < 0 || first > Int32Max) throw new ArgumentException("First particle must be an integer in [0, INT32_MAX]");
            string phase = header.GetProperty("phase").GetString();
            if (!phaseCounts.ContainsKey(phase)) throw new ArgumentException("Unknown phase");
            bool hasScalars = header.TryGetProperty("scalars", out var scalars) && scalars.ValueKind == JsonValueKind.Object;
            if (hasScalars && scalars.TryGetProperty("UseFixedRndSeedVal", out _)) throw new ArgumentException("Execute request cannot change seed mode");
            if (!B("UseFixedRndSeedVal"))
                foreach (string name in new[] { "RnGSeed1", "RnGSeed2" })
                    if (!hasScalars || !scalars.TryGetProperty(name, out var seed) || !JsonInt(seed, out long value) || value <= 0 || value >= 1L << 32)
                        throw new ArgumentException("Nonfixed execute request requires two nonzero master seeds");
            Receive(header, "execute");
            Guard();
            if (phase == "residual" && I("ISTATIONAER") != 0) throw new ArgumentException("Residual execution requires transient mode");
            if (phase == "fresh")
            {
                foreach (string name in ParticleArrays)
                {
                    descriptors.TryGetValue(name, out var descriptor);
                    long extent = descriptor != null && descriptor.Shape.Length > 0 ? descriptor.Shape[0] : 0;
                    if (first < 1 || !hostArrays.ContainsKey(name) || first + count > extent) throw new ArgumentException("Fresh particle range exceeds " + name);
                }
            }
            else
            {
                if (residualTraceBase < 0) throw new ArgumentException("Invalid residual trace base");
                foreach (string name in ResidualArrays)
                {
                    descriptors.TryGetValue(name, out var descriptor);
                    if (descriptor == null || descriptor.IsNull || descriptor.Shape.Length != 1 || descriptor.Shape[0] != count) throw new ArgumentException("Residual batch length mismatch: " + name);
                    hostArrays.TryGetValue(name, out byte[] data);
                    if (count > 0 && (data == null || data.Length != count * 4)) throw new ArgumentException("Residual batch buffer too small: " + name);
                }
            }
            double elapsed = 0;
            // Start conservatively; a single trajectory can still be long. Actual timing is recorded.
            long chunk = Math.Min(65536, Math.Max(1, header.TryGetProperty("chunk", out var chunkElement) ? (long)chunkElement.GetDouble() : 65536));
            long blockRequest = header.TryGetProperty("block", out var blockElement) ? (long)blockElement.GetDouble() : 128;
            if (blockRequest != 128 && blockRequest != 256) throw new ArgumentException("Supported block sizes: 128, 256");
            int block = (int)Math.Min(blockRequest, kernelResources[phase]["max_threads_per_block"]);
            metrics[phase + "_block_size"] = (long)block;
            BeginAdaptive(phase);
            // Kernel arguments: fresh (first, count); residual (count, base, six residual array pointers).
            int a0 = 0, a1 = 0;
            ulong* pointers = stackalloc ulong[ResidualArrays.Length];
            void** args = stackalloc void*[2 + ResidualArrays.Length];
            args[0] = &a0; args[1] = &a1;
            for (int i = 0; i < ResidualArrays.Length; i++) { pointers[i] = 0; args[2 + i] = &pointers[i]; }
            if (phase == "fresh" && count > 0 && specialize && specializationSteps.Contains("src")) freshSourceType = UniformSourceType(first, count);
            long capacity = count > 0 ? PrepareParticleWindows(phase == "fresh" ? ParticleArrays : ResidualArrays, count, phase == "fresh") : 0;
            for (long offset = 0; offset < count; offset += Math.Max(1, capacity))
            {
                long windowCount = Math.Min(capacity, count - offset);
                if (phase == "fresh")
                {
                    state.SetInt("ParticleWindowFirst", (int)(first + offset));
                    foreach (string name in ParticleArrays)
                    {
                        int width = DtypeSize(name);
                        byte[] data = hostArrays[name];
                        Upload(name, data, 0, width);
                        long begin = (first + offset) * width;
                        Upload(name, data, (int)begin, (int)(windowCount * width), width);
                    }
                }
                else
                    foreach (string name in ResidualArrays) Upload(name, hostArrays[name], (int)(offset * 4), (int)(windowCount * 4));
                Bind();
                long localOffset = 0;
                while (localOffset < windowCount)
                {
                    Canceled();
                    long part = Math.Min(LaunchPart(phase, chunk), windowCount - localOffset);
                    if (phase == "fresh")
                    {
                        a0 = (int)(first + offset + localOffset); a1 = (int)part;
                        elapsed += Launch(fnFresh, part, args, block);
                    }
                    else
                    {
                        long globalBase = residualTraceBase + offset + localOffset;
                        CheckDeviceError();
                        int encodedBase;
                        if (globalBase <= Int32Max)
                        {
                            part = Math.Min(part, Int32Max - globalBase + 1);
                            encodedBase = (int)globalBase;
                            residualErrorMapping = null;
                        }
                        else
                        {
                            encodedBase = Int32Min;
                            residualErrorMapping = (globalBase, part);
                        }
                        a0 = (int)part; a1 = encodedBase;
                        for (int i = 0; i < ResidualArrays.Length; i++) pointers[i] = buffers[ResidualArrays[i]].Pointer + (ulong)(localOffset * 4);
                        elapsed += Launch(fnResidual, part, args, block);
                    }
                    AdaptLaunch(phase, part);
                    localOffset += part;
                }
                Add(phase + "_memory_windows", 1);
                // Preserve scalar changes before the next window rebinds the pointer state.
                ReadState();
            }
            if (phase == "residual") residualTraceBase += count;
            phaseCounts[phase] += count;
            executionFailed = false;
            return new Dictionary<string, object>
            {
                {"status","ok"},{"kernel_ms",elapsed},{"phase",phase},{"count",count},{"payload_bytes",0L},
                {"memory_window_capacity",capacity},{"memory_windows",capacity > 0 ? (count + capacity - 1) / capacity : 0},
                {"block_size",(long)block},{"rng_seed1",state.GetUInt("RnGSeed1")},{"rng_seed2",state.GetUInt("RnGSeed2")},
                {"metrics_cumulative",new Dictionary<string, object>(metrics)}
            };
        }

        private void Result()
        {
            CheckDeviceError();
            if (executionFailed) throw new InvalidOperationException("Failed GPU execution; partial results unavailable");
            if (reproSums) FinalizeReproSums();
            var traceOutputs = new[] { "Trace", "StepTrace", "ResidualTrace" }.Where(n => B("TraceEnabled") && state.GetPointer(n) != 0);
            var outputs = (I("ISTATIONAER") == 0 ? Outputs : Outputs.Where(n => n != "Conz5d" && n != "EmissionPerSG")).ToList();
            if (B("DepositionExist") || B("WetDeposition")) outputs.Add("Depo_conz");
            if (B("Odour")) { outputs.Add("Conz3dp"); outputs.Add("Conz3dm"); }
            if (B("WriteVerticalConcentration")) outputs.Add("ConzSsum");
            if (I("LogLevel") != 0) { outputs.Add("LogReflexions"); outputs.Add("Log_Timesteps"); }
            var names = outputs.Concat(traceOutputs).ToList();
            var arrayDescriptors = new List<Dictionary<string, object>>();
            long cursor = 0;
            foreach (string name in names)
            {
                var d = descriptors[name];
                var copy = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var p in d.Properties) copy[p.Key] = p.Key == "offset" ? cursor : p.Value;
                if (!copy.ContainsKey("offset")) copy["offset"] = cursor;
                arrayDescriptors.Add(copy);
                cursor += d.Bytes;
            }
            var header = new Dictionary<string, object>
            {
                {"status","ok"},{"scalars",new Dictionary<string, object>()},{"arrays",arrayDescriptors},{"payload_bytes",cursor},
                {"kernel_total_ms",kernelTotalMs},{"phase_counts",new Dictionary<string, long>(phaseCounts)},
                {"metrics_cumulative",new Dictionary<string, object>(metrics)},{"metrics_scope","before_result_transfer"},{"metrics_follow",true}
            };
            WriteFrame(header, ResultChunks(names));
        }

        private void ReadOutput(string name, long offset, byte[] raw, int length)
        {
            var clock = Stopwatch.StartNew();
            fixed (byte* target = raw)
            {
                if (mappedBuffers.TryGetValue(name, out IntPtr host))
                {
                    System.Buffer.MemoryCopy((byte*)host + offset, target, length, length);
                    AddMs("mapped_host_copy_ms", clock);
                    Add("mapped_host_copy_bytes", length);
                }
                else
                {
                    cuda.CopyToHost(target, buffers[name].Pointer + (ulong)offset, length, "read " + name);
                    AddMs("d2h_ms", clock);
                    Add("d2h_bytes", length);
                }
            }
        }

        private IEnumerable<ReadOnlyMemory<byte>> ResultChunks(List<string> names)
        {
            var raw = new byte[TransferBytes];
            var single = new byte[TransferBytes / 2];
            foreach (string name in names)
            {
                long size = descriptors[name].Bytes;
                int factor = doubleStorage.Contains(name) ? 2 : 1;
                for (long offset = 0; offset < size * factor; offset += TransferBytes)
                {
                    int length = (int)Math.Min(TransferBytes, size * factor - offset);
                    Canceled();
                    ReadOutput(name, offset, raw, length);
                    if (factor == 2)
                    {
                        int n = length / 8;
                        for (int i = 0; i < n; i++)
                        {
                            double value = BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(8 * i));
                            if (!double.IsFinite(value)) throw new InvalidDataException("nonfinite Double output: " + name);
                            float narrowed = (float)value;
                            if (!float.IsFinite(narrowed)) throw new InvalidDataException("Single output overflow: " + name);
                            BinaryPrimitives.WriteSingleLittleEndian(single.AsSpan(4 * i), narrowed);
                        }
                        yield return new ReadOnlyMemory<byte>(single, 0, 4 * n);
                    }
                    else yield return new ReadOnlyMemory<byte>(raw, 0, length);
                }
            }
        }

        // ------------------------------------------------------------------ main loop

        /// <summary>Serve requests until close or end of input. Returns 0 then, or 1 after an error.</summary>
        internal int Run()
        {
            try
            {
                try
                {
                    Initialize();
                    while (true)
                    {
                        using var document = ReadHeader();
                        if (document == null) break;
                        JsonElement header = document.RootElement;
                        string command = header.GetProperty("command").GetString();
                        if (command != "init" && header.TryGetProperty("arrays", out var arrays) && arrays.EnumerateArray().Any(d => d.TryGetProperty("zero_fill", out _)))
                            throw new ArgumentException("zero_fill is only valid for init");
                        if (command == "close")
                        {
                            WriteFrame(new Dictionary<string, object> { { "status", "ok" }, { "payload_bytes", 0L } });
                            break;
                        }
                        if (command == "probe")
                        {
                            WriteFrame(new Dictionary<string, object> { { "status", "ok" }, { "frame", "probe" }, { "payload_bytes", 0L }, { "probe", ProbeInfo() } });
                            continue;
                        }
                        if (command == "init")
                        {
                            Add("init_count", 1);
                            residualTraceBase = 0;
                            RefreshMemoryBudget();
                            CheckInitFields(header);
                            Receive(header, "init");
                            Guard();
                            if (reproSums) PrepareReproSums();
                            // No kernel runs after init; the first execute loads the kernel for its own launch values.
                            Bind(specializeKernel: false);
                            WriteFrame(new Dictionary<string, object> { { "status", "ok" }, { "state_bytes", (long)state.Size }, { "payload_bytes", 0L }, { "startup_ms", startupMs }, { "metrics_cumulative", new Dictionary<string, object>(metrics) } });
                        }
                        else if (command == "execute") WriteFrame(Execute(header));
                        else if (command == "result")
                        {
                            Result();
                            WriteFrame(new Dictionary<string, object> { { "status", "ok" }, { "frame", "result_metrics" }, { "payload_bytes", 0L }, { "metrics_cumulative", new Dictionary<string, object>(metrics) } });
                        }
                        else throw new ArgumentException("Unknown command: " + command);
                    }
                    ExitCode = 0;
                }
                catch (FrameWriteException error)
                {
                    log(error.ToString());
                    ExitCode = 1;
                }
                catch (Exception error)
                {
                    log(error is OperationCanceledException ? "GPU run cancelled" : error.ToString());
                    // Stop reading first, so a client that is still writing a large frame fails instead of blocking, then answer.
                    try { input.Dispose(); } catch (IOException) { }
                    // settings_file_error: an unusable value of the GPU settings file, which must not end in a CPU fallback
                    var frame = new Dictionary<string, object> { { "status", "error" }, { "error", error.Message }, { "payload_bytes", 0L } };
                    if (GpuSettings.IsFromFile(error)) frame["settings_file_error"] = true;
                    try { WriteFrame(frame); }
                    catch (FrameWriteException) { }
                    ExitCode = 1;
                }
            }
            finally
            {
                try { cuda?.Dispose(); }
                catch (Exception error) { log((cuda?.Hip == true ? "HIP" : "CUDA") + " cleanup failed: " + error.Message); }
                try { output.Dispose(); } catch (IOException) { }
                try { input.Dispose(); } catch (IOException) { }
            }
            return ExitCode;
        }
    }
}
