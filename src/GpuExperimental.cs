using System;
namespace GRAL_2001
{
    // Strict opt-in for isolated experiments; no CPU fallback after partial GPU execution.
    internal static class GpuExperimental
    {
        internal static readonly bool Enabled=Environment.GetEnvironmentVariable("INU_GPU_EXPERIMENTAL")=="1";
        private static GpuSituationDriver driver;
        private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing "+name);
        internal static void Run(int first,int count)
        {
            GpuSituationDriver.Guard(first,count);
            if(driver==null)
            {
                driver=new GpuSituationDriver(Required("INU_GPU_PYTHON"),Required("INU_GPU_SERVER"),Required("INU_GPU_MANIFEST"),Required("INU_GPU_METRICS"),Required("INU_GPU_STDERR"));
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
            current?.Dispose();
        }
    }
}
