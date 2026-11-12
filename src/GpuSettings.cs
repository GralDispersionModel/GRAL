using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace GRAL_2001
{
    // Per-user GPU settings file, <LocalApplicationData>/GRAL/GpuSettings.txt: one "key = value ! comment" per line, for the settings
    // that belong to the PC and not to a project (device, backend, compiler folders, launch size, memory budgets). INU_GPU_SETTINGS_FILE
    // names another file, or "off" for none; a file that does not exist is no file. For each key the environment variable wins, then
    // the file, then the default. The GPU switch and reproducible sums are not here: they change results and belong to in.dat.
    // A run reads the file only when the GPU is asked for; GRAL never writes it.
    internal static class GpuSettings
    {
        // Key in the file and the variable it stands for (null: the key exists only in the file)
        internal static readonly (string Key,string Variable)[] Keys=
        {
            ("device","INU_GPU_DEVICE"),("device_name",null),("backend","INU_GPU_BACKEND"),("allow_integrated",null),
            ("fallback_on_startup","INU_GPU_FALLBACK_ON_STARTUP"),("nvrtc_dir","INU_GPU_NVRTC_DIR"),("hiprtc_dir","INU_GPU_HIPRTC_DIR"),
            ("chunk","INU_GRAL_GPU_CHUNK"),("block","INU_GRAL_GPU_BLOCK"),("max_kernel_ms","INU_GPU_MAX_KERNEL_MS"),
            ("memory_budget_bytes","INU_GPU_MEMORY_BUDGET_BYTES"),("memory_reserve_bytes","INU_GPU_MEMORY_RESERVE_BYTES"),
            ("pinned_budget_bytes","INU_GPU_PINNED_BUDGET_BYTES"),("cache_dir","INU_GPU_CACHE_DIR")
        };
        private static readonly object sync=new object();
        private static Dictionary<string,string> file;
        /// <summary>The file that was looked at; null when INU_GPU_SETTINGS_FILE=off or no per-user folder exists.</summary>
        internal static string FilePath { get; private set; }
        /// <summary>read, absent or off.</summary>
        internal static string FileStatus { get; private set; }
        /// <summary>
        /// Reads the file once. An unknown key, a line without "=" or an invalid value is an ArgumentException with the status
        /// invalid_setting that names the file, the line and the key; a later call throws it again.
        /// </summary>
        internal static void Load()
        {
            lock(sync)
            {
                if(file!=null) return;
                // Blanks around the value do not count, and a blank value is the same as an unset variable
                string path=Environment.GetEnvironmentVariable("INU_GPU_SETTINGS_FILE")?.Trim();
                if(string.IsNullOrEmpty(path))
                {
                    string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    path=string.IsNullOrEmpty(local) ? null : Path.Combine(local,"GRAL","GpuSettings.txt");
                }
                else if(path.Equals("off",StringComparison.OrdinalIgnoreCase)) { FilePath=null; FileStatus="off"; file=new Dictionary<string,string>(); return; }
                else
                {
                    // A value that is no path at all is reported, with the text as given and the status of a file that is not there
                    FilePath=path; FileStatus="absent";
                    try { path=Path.GetFullPath(path); }
                    catch(Exception error) when(error is ArgumentException || error is IOException || error is NotSupportedException || error is System.Security.SecurityException)
                    { throw GpuCuda.Status(new ArgumentException("INU_GPU_SETTINGS_FILE is not a usable path: "+error.Message,error),"invalid_setting"); }
                }
                FilePath=path;
                var values=new Dictionary<string,string>(StringComparer.Ordinal);
                if(path==null || !File.Exists(path)) { FileStatus="absent"; file=values; return; }
                FileStatus="read";
                string[] lines;
                try { lines=File.ReadAllLines(path); }
                catch(Exception error) when(error is IOException || error is UnauthorizedAccessException)
                { throw GpuCuda.Status(new ArgumentException("GPU settings file "+path+" cannot be read: "+error.Message,error),"invalid_setting"); }
                for(int i=0;i<lines.Length;i++)
                {
                    string line=lines[i].Split('!')[0].Trim();
                    if(line.Length==0 || line[0]=='#') continue;
                    string where="GPU settings file "+path+" line "+(i+1).ToString(System.Globalization.CultureInfo.InvariantCulture)+": ";
                    int equals=line.IndexOf('=');
                    if(equals<0) throw GpuCuda.Status(new ArgumentException(where+"'"+line+"' is not of the form key = value"),"invalid_setting");
                    string key=line.Substring(0,equals).Trim().ToLowerInvariant(),value=line.Substring(equals+1).Trim();
                    if(!Keys.Any(k=>k.Key==key))
                        throw GpuCuda.Status(new ArgumentException(where+"unknown key '"+key+"' (keys: "+string.Join(", ",Keys.Select(k=>k.Key))+")"),"invalid_setting");
                    // An empty value means "not set"
                    if(value.Length==0) { values.Remove(key); continue; }
                    try { Validate(key,value,key); }
                    catch(ArgumentException error) { throw GpuCuda.Status(new ArgumentException(where+error.Message,error),"invalid_setting"); }
                    values[key]=value;
                }
                file=values;
            }
        }
        /// <summary>The checks the variables get where they are used, so that a value is judged the same way from either source.</summary>
        internal static void Validate(string key,string value,string label)
        {
            switch(key)
            {
                case "device": GpuCuda.OrdinalValue(value,label); break;
                case "backend": GpuCuda.BackendValue(value,label); break;
                case "allow_integrated":
                case "fallback_on_startup": if(value!="0" && value!="1") throw new ArgumentException(label+" must be 0 or 1, got '"+value+"'"); break;
                case "nvrtc_dir":
                case "hiprtc_dir": GpuCuda.DirectoryValue(value,label); break;
                case "chunk": GpuSituationDriver.IntValue(value,label,65536,1,65536); break;
                case "block": GpuSituationDriver.BlockValue(value,label); break;
                case "max_kernel_ms":
                case "memory_reserve_bytes":
                case "pinned_budget_bytes": GpuRuntimeServer.MemoryValue(value,label,0); break;
                case "memory_budget_bytes": GpuRuntimeServer.MemoryValue(value,label,4096); break;
            }
        }
        /// <summary>Value and source ("env" or "file") of a key; (null or empty, null) when neither sets it.</summary>
        internal static (string Value,string Source) Get(string key)
        {
            Load();
            string variable=Keys.First(k=>k.Key==key).Variable;
            string fromVariable=variable==null ? null : Environment.GetEnvironmentVariable(variable);
            if(!string.IsNullOrEmpty(fromVariable)) return (fromVariable,"env");
            return file.TryGetValue(key,out string fromFile) ? (fromFile,"file") : (fromVariable,null);
        }
        internal static string Value(string key) => Get(key).Value;
        /// <summary>
        /// Marks an error about a value that proved unusable only when it was used (a device ordinal beyond the last device, a compiler
        /// folder without a compiler), when that value comes from the settings file: like every invalid value of the file it stops
        /// the run even when startup fallback is on. The same error from a variable stays an ordinary startup failure.
        /// </summary>
        internal static T FromFile<T>(T error,string key) where T:Exception
        {
            if(Get(key).Source=="file") error.Data["GpuSettingsFile"]=true;
            return error;
        }
        internal static bool IsFromFile(Exception error)
        {
            for(;error!=null;error=error.InnerException) if(error.Data["GpuSettingsFile"] is true) return true;
            return false;
        }
        /// <summary>Name of a setting for a message: the variable, or the key and the file when the value comes from the file.</summary>
        internal static string Label(string key) => Get(key).Source=="file" ? key+" in "+FilePath : Keys.First(k=>k.Key==key).Variable ?? key;
        /// <summary>Every key that has a value, with its source, in the order of the key list.</summary>
        internal static IEnumerable<(string Key,string Value,string Source)> Active()
        {
            foreach(var (key,_) in Keys)
            {
                var (value,source)=Get(key);
                if(source!=null) yield return (key,value,source);
            }
        }
    }
}
