using System;
using System.IO;
namespace GRAL_2001
{
    // Strict opt-in for isolated experiments; no CPU fallback after partial GPU execution.
    internal static class GpuExperimental
    {
        internal static readonly bool Enabled=Environment.GetEnvironmentVariable("INU_GPU_EXPERIMENTAL")=="1";
        // Only a failure to start the GPU server, before any GPU situation, may turn the whole run into a CPU run.
        private static readonly bool FallbackOnStartup=Environment.GetEnvironmentVariable("INU_GPU_FALLBACK_ON_STARTUP")=="1";
        private static GpuSituationDriver driver;
        private static FileStream metricsLock;
        private static bool started;
        private static string startupFailure;
        private static string lastFallbackReason;
        internal static bool CanRun(int first,int count)
        {
            if(startupFailure!=null) return false;
            string reason=GpuSituationDriver.GetUnsupportedReason(first,count);
            if(reason==null) { lastFallbackReason=null; return Start(); }
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
        private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing "+name);
        // Starts the server and runs its startup probe at the first GPU-eligible situation.
        private static bool Start()
        {
            if(driver!=null) return true;
            try { driver=CreateDriver(); started=true; return true; }
            catch(Exception error) when(error is not OperationCanceledException)
            {
                string message="GPU startup failed: "+error.Message;
                if(started || !FallbackOnStartup)
                {
                    Console.WriteLine(message);
                    throw new InvalidOperationException(message+(started?"":" (set INU_GPU_FALLBACK_ON_STARTUP=1 to run on the CPU instead)"),error);
                }
                startupFailure=message;
                Console.WriteLine("GPU fallback to CPU for the whole run: "+message);
                ProgramWriters.LogfileGralCoreWrite("GPU fallback to CPU for the whole run: "+message);
                return false;
            }
        }
        private static GpuSituationDriver CreateDriver()
        {
            string metrics=GpuLogFiles.Claim(Required("INU_GPU_METRICS"),out metricsLock);
            try { return new GpuSituationDriver(Required("INU_GPU_PYTHON"),Required("INU_GPU_SERVER"),Required("INU_GPU_MANIFEST"),metrics,Required("INU_GPU_STDERR")); }
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
            try { driver.RunSituation(first,count); }
            catch(Exception runError)
            {
                try { Close(); }
                catch(Exception cleanupError) { throw new AggregateException("GPU execution and cleanup failed",runError,cleanupError); }
                throw;
            }
        }
        internal static void Close()
        {
            var current=driver;driver=null;
            try { current?.Dispose(); }
            finally { ReleaseMetricsLock(); }
        }
    }
}
