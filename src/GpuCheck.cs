using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace GRAL_2001
{
    // GRAL --gpu-check [folder]: checks the GPU runtime files and starts the GPU backend as a GPU run would (GPU settings file,
    // driver, device, runtime compiler, probe compile). The settings file is the one INU_GPU_SETTINGS_FILE names, else GPU.txt in
    // the current directory, which is the folder when one is given (the Computation folder, as in a run); use_gpu need not be 1,
    // the check tells whether the GPU could start. Any other argument is a usage error (exit code 2), never a run.
    // Prints one line "GRAL_GPU_CHECK {json}" with the status ok, no_manifest,
    // no_driver, driver_error, no_device, only_integrated, no_nvrtc, no_hiprtc, nvrtc_rejected, hiprtc_rejected, invalid_setting
    // or failed. Besides the fields of the chosen device the line has settings_file, settings_file_status, settings (value and
    // source of every key that is set), devices (all devices of the started backend), other_backends (installed, not started)
    // and warnings; the device fields found so far are kept when a later step fails. Settings that only a run uses
    // (specialization, reproducible sums, chunk and block size, kernel-time limit, memory budgets) get their syntax checks
    // once the backend has started; the module cache folder and in.dat are not looked at. Reads no other project file and
    // writes nothing; exit code 0 means status ok, 2 anything else.
    internal static class GpuCheck
    {
        internal const string Argument="--gpu-check";
        internal static int Run(string[] args)
        {
            string[] rest=args.Where(argument=>argument!=Argument).ToArray();
            string problem=rest.Length>1 || args.Length-rest.Length>1 ? "too many arguments" : rest.Length==1 && !Directory.Exists(rest[0]) ? "not a folder: "+rest[0] : null;
            if(problem==null && rest.Length==1)
                try { Directory.SetCurrentDirectory(rest[0]); }
                catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException) { problem=error.Message; }
            if(problem!=null)
            {
                Console.Error.WriteLine("Usage: GRAL "+Argument+" [Computation folder] ("+problem+")");
                return 2;
            }
            var report=new Dictionary<string,object>{["schema"]=1};
            var log=new List<string>();
            int exitCode=0;
            try
            {
                string manifest=Path.GetFullPath(GpuExperimental.ManifestPath());
                report["manifest"]=manifest;
                GpuSettings.Load();
                // A run reads both files first; an unreadable or invalid manifest stops it at the first GPU situation
                if(!File.Exists(manifest) || !File.Exists(Path.Combine(Path.GetDirectoryName(manifest),"generated.cu")))
                    throw GpuCuda.Status(new FileNotFoundException("fields_manifest.json or generated.cu not found in "+Path.GetDirectoryName(manifest)),"no_manifest");
                try { using var json=JsonDocument.Parse(File.ReadAllBytes(manifest)); json.RootElement.GetProperty("fields"); }
                catch(Exception error) when(error is JsonException || error is KeyNotFoundException || error is InvalidOperationException)
                {
                    throw GpuCuda.Status(new InvalidDataException("Invalid "+manifest+": "+error.Message,error),"no_manifest");
                }
                // The backend writes the device information, the device list and its warnings into the report as it finds them
                using var gpu=new GpuCuda(Path.GetDirectoryName(manifest),log.Add,report);
                // Settings that only a run uses: the same syntax checks, so that a bad value shows before the run
                foreach(string key in new[]{"chunk","block","max_kernel_ms","memory_budget_bytes","memory_reserve_bytes","pinned_budget_bytes"})
                    if(GpuSettings.Value(key) is string value) GpuSettings.Validate(key,value,GpuSettings.Label(key));
                GpuRuntimeServer.ReproducibleSumsSetting();
                GpuRuntimeServer.SpecializationSetting();
                report["status"]="ok";
            }
            catch(Exception error)
            {
                report["status"]=StatusOf(error);
                report["message"]=error.Message;
                exitCode=2;
            }
            var settings=new Dictionary<string,object>();
            // An invalid file has no settings to list. When the check stopped before it read the file (no manifest), the file is read
            // here, so that the two keys below are always filled; its error is then reported unless there is an earlier one.
            try { foreach(var (key,value,source) in GpuSettings.Active()) settings[key]=new Dictionary<string,string>{["value"]=value,["source"]=source}; }
            catch(Exception error)
            {
                settings.Clear();
                if(exitCode==0) { report["status"]=StatusOf(error); report["message"]=error.Message; exitCode=2; }
            }
            report["settings_file"]=GpuSettings.FilePath;
            report["settings_file_status"]=GpuSettings.FileStatus;
            report["settings"]=settings;
            if(!report.ContainsKey("devices")) report["devices"]=Array.Empty<object>();
            // A backend whose runtime library is installed but which was not started; it is not loaded for this test
            var others=new List<string>();
            if(report.TryGetValue("backend",out object backend))
            {
                if((string)backend=="cuda" && GpuCuda.HipRuntimePresent()) others.Add("hip");
                // Without the backend setting hip, CUDA was tried first and found no device, so it is not offered again
                if((string)backend=="hip" && settings.TryGetValue("backend",out object chosen) && "hip".Equals(((Dictionary<string,string>)chosen)["value"]?.Trim(),StringComparison.OrdinalIgnoreCase) &&
                   GpuCuda.CudaDriverPresent()) others.Add("cuda");
            }
            report["other_backends"]=others;
            var warnings=new List<string>(report.TryGetValue("warnings",out object found) ? (IEnumerable<string>)found : Array.Empty<string>());
            // A settings file in another case is not read (Linux file names are case-sensitive)
            if(GpuSettings.OtherCaseFile is string otherCase) warnings.Add(otherCase+" in "+Path.GetDirectoryName(GpuSettings.FilePath)+" is not read: the file name must be GPU.txt");
            report["warnings"]=warnings;
            report["log"]=log;
            Console.WriteLine("GRAL_GPU_CHECK "+GpuJson.Text(report));
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
