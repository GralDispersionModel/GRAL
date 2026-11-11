using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace GRAL_2001
{
    // GRAL --gpu-check: checks the GPU runtime files and starts the GPU backend as a GPU run would (driver, device, runtime
    // compiler, probe compile). Prints one line "GRAL_GPU_CHECK {json}" with a status such as ok, no_manifest, no_driver,
    // no_device, only_integrated, no_nvrtc, no_hiprtc, nvrtc_rejected, hiprtc_rejected or invalid_setting. Settings that only
    // the runtime reads during a run (specialization, reproducible sums, chunk and block size, kernel-time limit, memory
    // budgets, module cache) are not checked. Reads no project files and writes nothing; exit code 0 means that the GPU
    // backend starts.
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
                // A run reads both files first; an unreadable or invalid manifest stops it at the first GPU situation
                if(!File.Exists(manifest) || !File.Exists(Path.Combine(Path.GetDirectoryName(manifest),"generated.cu")))
                    throw GpuCuda.Status(new FileNotFoundException("fields_manifest.json or generated.cu not found in "+Path.GetDirectoryName(manifest)),"no_manifest");
                try { using var json=JsonDocument.Parse(File.ReadAllBytes(manifest)); json.RootElement.GetProperty("fields"); }
                catch(Exception error) when(error is JsonException || error is KeyNotFoundException || error is InvalidOperationException)
                {
                    throw GpuCuda.Status(new InvalidDataException("Invalid "+manifest+": "+error.Message,error),"no_manifest");
                }
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
        // The innermost tagged error decides, so "No usable GPU" reports why the HIP attempt failed. Invalid INU_GPU_* values
        // are ArgumentExceptions.
        private static string StatusOf(Exception error)
        {
            string status=null;
            bool setting=false;
            for(Exception e=error;e!=null;e=e.InnerException)
            {
                if(e.Data["GpuStatus"] is string tag) status=tag;
                setting|=e is ArgumentException;
            }
            return status ?? (setting ? "invalid_setting" : "failed");
        }
    }
}
