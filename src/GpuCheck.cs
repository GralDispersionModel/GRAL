using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace GRAL_2001
{
    // GRAL --gpu-check: starts the GPU backend as a GPU run would (driver, device, runtime compiler, probe compile) and
    // prints one line "GRAL_GPU_CHECK {json}" with a status such as ok, no_driver, no_device, only_integrated, no_nvrtc,
    // no_hiprtc, toolchain_rejected or no_manifest. Reads no project files and writes nothing; exit code 0 means ready.
    internal static class GpuCheck
    {
        internal const string Argument="--gpu-check";
        internal static int Run()
        {
            var report=new Dictionary<string,object>{["schema"]=1};
            var log=new List<string>();
            int exitCode=0;
            try
            {
                string manifest=Path.GetFullPath(GpuExperimental.ManifestPath());
                report["manifest"]=manifest;
                using var gpu=new GpuCuda(Path.GetDirectoryName(manifest),log.Add);
                report["backend"]=gpu.Hip ? "hip" : "cuda";
                foreach(var item in gpu.DeviceInfo) report[item.Key]=item.Value;
                report["status"]="ok";
            }
            catch(Exception error)
            {
                report["status"]=StatusOf(error);
                report["message"]=error.Message;
                exitCode=2;
            }
            report["log"]=log;
            Console.WriteLine("GRAL_GPU_CHECK "+JsonSerializer.Serialize(report));
            return exitCode;
        }
        // The innermost tagged error decides, so "No usable GPU" reports why the HIP attempt failed.
        private static string StatusOf(Exception error)
        {
            string status="failed";
            for(Exception e=error;e!=null;e=e.InnerException)
                if(e.Data["GpuStatus"] is string tag) status=tag;
            return status;
        }
    }
}
