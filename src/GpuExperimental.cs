using System;
using System.IO;
namespace GRAL_2001
{
    // GPU path for the particle dispersion; no CPU fallback after partial GPU execution: a GPU error then ends the run (exit code 1).
    internal static class GpuExperimental
    {
        private static int enabledState=-1;
        // use_gpu = 1 in the project's GPU.txt switches the GPU on; INU_GPU_EXPERIMENTAL=1 or 0 overrides it. The first query reads
        // the file (an invalid file is an ArgumentException, whatever the decision would be) and writes to the GRAL log which of
        // the two decided, and to the console as well unless both leave the GPU off.
        internal static bool Enabled
        {
            get
            {
                if(enabledState<0)
                {
                    bool fileOn=GpuSettings.FileValue("use_gpu")=="1";
                    string variable=Environment.GetEnvironmentVariable("INU_GPU_EXPERIMENTAL");
                    bool on=variable=="1" || (variable!="0" && fileOn);
                    bool byVariable=variable=="1" || variable=="0";
                    string source=byVariable ? "INU_GPU_EXPERIMENTAL="+variable+(fileOn ? ", GPU.txt asks for the GPU" : string.Empty) : GpuSettings.FileStatus=="read" ? "GPU.txt" : "no GPU.txt";
                    // A value such as "true" or "yes" does not switch anything; say so instead of ignoring it without a word
                    bool ignored=!byVariable && !string.IsNullOrEmpty(variable);
                    if(ignored) source+="; INU_GPU_EXPERIMENTAL="+variable+" is ignored, use 1 or 0";
                    string message="GPU for the particle dispersion: "+(on ? "on" : "off")+" ("+source+")";
                    if(on || byVariable || fileOn || ignored) Console.WriteLine(message);
                    ProgramWriters.LogfileGralCoreWrite(message);
                    enabledState=on ? 1 : 0;
                }
                return enabledState==1;
            }
        }
        // Only a failure to start the GPU server, before any GPU situation, may turn the whole run into a CPU run
        // (INU_GPU_FALLBACK_ON_STARTUP=1 or fallback_on_startup = 1 in GPU.txt).
        private static bool FallbackOnStartup => GpuSettings.Value("fallback_on_startup")=="1";
        private static GpuSituationDriver driver;
        private static FileStream metricsLock;
        private static bool started;
        private static string startupFailure;
        private static string lastFallbackReason;
        private static int gpuSituations,cpuSituations;
        internal static bool CanRun(int first,int count)
        {
            if(startupFailure!=null) { cpuSituations++; return false; }
            string reason=GpuSituationDriver.GetUnsupportedReason(first,count);
            if(reason==null)
            {
                lastFallbackReason=null;
                if(Start()) return true;
                cpuSituations++;
                return false;
            }
            cpuSituations++;
            if(reason!=lastFallbackReason)
            {
                // Keep the reason in the GRAL log as well, so a CPU situation in a GPU run is visible afterwards.
                Console.WriteLine("GPU fallback to CPU: "+reason);
                ProgramWriters.LogfileGralCoreWrite("GPU fallback to CPU from situation "+Program.IWET.ToString(System.Globalization.CultureInfo.InvariantCulture)+": "+reason);
                if(reason.Contains("non-finite",StringComparison.Ordinal))ProgramWriters.LogfileProblemreportWrite("GPU fallback to CPU: "+reason);
            }
            lastFallbackReason=reason;
            return false;
        }
        // Unset settings get defaults, so the GPU switch (use_gpu in GPU.txt or INU_GPU_EXPERIMENTAL) is the only setting a normal run needs.
        private static string Setting(string name,string fallback)
        {
            string value=Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
        // INU_GPU_MANIFEST, else the runtime files shipped with this GRAL build: in its gpu\runtime folder, which the build writes, then next to the executable.
        internal static string ManifestPath()
        {
            string value=Environment.GetEnvironmentVariable("INU_GPU_MANIFEST");
            if(!string.IsNullOrEmpty(value)) return value;
            foreach(string folder in new[]{Path.Combine(AppContext.BaseDirectory,"gpu","runtime"),AppContext.BaseDirectory})
                if(File.Exists(Path.Combine(folder,"fields_manifest.json")) && File.Exists(Path.Combine(folder,"generated.cu"))) return Path.Combine(folder,"fields_manifest.json");
            throw GpuCuda.Status(new FileNotFoundException("INU_GPU_MANIFEST is not set, and fields_manifest.json with generated.cu is neither next to GRAL nor in its "+
                                                           Path.Combine("gpu","runtime")+" folder"),"no_manifest");
        }
        // Starts the server and runs its startup probe at the first GPU-eligible situation.
        private static bool Start()
        {
            if(driver!=null) return true;
            // Outside the try block: an invalid GPU settings file stops the run even when startup fallback is on (Program.Main has
            // already read the file for the GPU switch and stopped there; this call only keeps the rule with the code that needs it)
            GpuSettings.Load();
            try { driver=CreateDriver(); started=true; return true; }
            catch(Exception error) when(error is not OperationCanceledException)
            {
                string message="GPU startup failed: "+error.Message;
                // Program.Main reports this on the console and in the logs and ends the run with exit code 1
                // An unusable value of the GPU settings file stops the run like every other invalid value of that file
                bool fromFile=GpuSettings.IsFromFile(error);
                if(started || !FallbackOnStartup || fromFile)
                    throw new InvalidOperationException(message+(started || fromFile?"":" (set INU_GPU_FALLBACK_ON_STARTUP=1, or fallback_on_startup = 1 in GPU.txt, to run on the CPU instead)"),error);
                startupFailure=message;
                Console.WriteLine("GPU fallback to CPU for the whole run: "+message);
                ProgramWriters.LogfileGralCoreWrite("GPU fallback to CPU for the whole run: "+message);
                return false;
            }
        }
        private static GpuSituationDriver CreateDriver()
        {
            // Default logs go to the working directory, which is the Computation folder when GRAL is started with the project path.
            string metrics=GpuLogFiles.Claim(Setting("INU_GPU_METRICS","GRAL_GPU_metrics.jsonl"),out metricsLock);
            try { return new GpuSituationDriver(ManifestPath(),metrics,Setting("INU_GPU_STDERR","GRAL_GPU_runtime.log")); }
            catch { ReleaseMetricsLock(); throw; }
        }
        private static void ReleaseMetricsLock()
        {
            var current=metricsLock;metricsLock=null;
            current?.Dispose();
        }
        internal static void Run(int first,int count)
        {
            GpuSituationDriver.Guard(first,count);
            if(driver==null)
            {
                driver=CreateDriver();started=true;
            }
            try { driver.RunSituation(first,count); gpuSituations++; }
            catch(Exception runError)
            {
                try { Close(); }
                catch(Exception cleanupError) { throw new AggregateException("GPU execution and cleanup failed",runError,cleanupError); }
                throw;
            }
        }
        // After a successful GPU start: what ran, for the GRAL log. The console already has the device line.
        internal static void LogStart(System.Text.Json.JsonElement probe)
        {
            string Text(string name) => probe.TryGetProperty(name,out var value) ? value.ToString() : "?";
            bool hip=Text("backend")=="hip";
            ProgramWriters.LogfileGralCoreWrite("GPU start: backend "+(hip ? "hip" : "cuda")+", device "+Text("ordinal")+" ("+Text("name")+"), "+Text("total_memory_mib")+" MiB, "+
                                                (hip ? Text("driver_cuda")+", "+Text("nvrtc_version") : "driver CUDA "+Text("driver_cuda")+", NVRTC "+Text("nvrtc_version"))+
                                                ", "+Text("binary_kind")+" "+Text("target"));
            ProgramWriters.LogfileGralCoreWrite("GPU reproducible sums: "+(Text("reproducible_sums")=="True" ? "on" : "off")+" ("+Text("reproducible_sums_source")+")");
            if(probe.TryGetProperty("warnings",out var warnings) && warnings.ValueKind==System.Text.Json.JsonValueKind.Array)
                foreach(var warning in warnings.EnumerateArray())
                {
                    Console.WriteLine("GPU warning: "+warning.GetString());
                    ProgramWriters.LogfileGralCoreWrite("GPU warning: "+warning.GetString());
                }
            // The project's own file is written as GPU.txt; the full path only when INU_GPU_SETTINGS_FILE names another file
            ProgramWriters.LogfileGralCoreWrite("GPU settings file: "+(GpuSettings.FileStatus=="read" ? GpuSettings.FileName+" (read)" : "none ("+GpuSettings.FileStatus+")"));
            // Every setting that is not at its default, with the source that decided
            var listed=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var (key,value,source) in GpuSettings.Active())
            {
                string variable=System.Linq.Enumerable.First(GpuSettings.Keys,k=>k.Key==key).Variable;
                if(source=="env") listed.Add(variable);
                ProgramWriters.LogfileGralCoreWrite("GPU setting "+key+" = "+value+" ("+(source=="env" ? "env "+variable : "file")+")");
            }
            // Every other GPU variable of this process, so that a value inherited from the calling program or the user environment is visible.
            var variables=new System.Collections.Generic.SortedDictionary<string,string>(StringComparer.Ordinal);
            foreach(System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                if(entry.Key is string name && !listed.Contains(name) && (name.StartsWith("INU_GPU_",StringComparison.OrdinalIgnoreCase) || name.StartsWith("INU_GRAL_GPU_",StringComparison.OrdinalIgnoreCase)))
                    variables[name]=entry.Value as string;
            foreach(var variable in variables) ProgramWriters.LogfileGralCoreWrite("GPU variable "+variable.Key+"="+variable.Value);
        }
        // One line at the end of a run that asked for the GPU: where the situations were computed.
        internal static void LogSummary(bool failed)
        {
            if(enabledState!=1) return;
            string message="GPU run summary: "+gpuSituations.ToString(System.Globalization.CultureInfo.InvariantCulture)+" situation(s) computed on the GPU, "+
                           cpuSituations.ToString(System.Globalization.CultureInfo.InvariantCulture)+" on the CPU"+
                           (startupFailure!=null ? " (the whole run fell back to the CPU)" : cpuSituations>0 ? " (per-situation fallback)" : string.Empty)+
                           (failed ? "; the run was stopped by a GPU error, and the situation that failed is counted in neither" : string.Empty);
            Console.WriteLine(message);
            ProgramWriters.LogfileGralCoreWrite(message);
        }
        internal static void Close()
        {
            var current=driver;driver=null;
            try { current?.Dispose(); }
            finally { ReleaseMetricsLock(); }
        }
    }
}
