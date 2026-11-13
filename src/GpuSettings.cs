using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace GRAL_2001
{
    // GPU settings file of a project, GPU.txt in the Computation folder (GRAL's working directory): one "key = value ! comment" per
    // line, for the GPU switch (use_gpu), reproducible sums and the device, backend, compiler folders, launch size and memory budgets.
    // INU_GPU_SETTINGS_FILE names another file, or "off" for none; a file that does not exist is no file: GPU off, every default.
    // For each key the environment variable wins, then the file, then the default. Every run reads the file, because it decides
    // whether the GPU is used; GRAL never writes it.
    internal static class GpuSettings
    {
        // Key in the file and the variable it stands for (null: the key exists only in the file)
        internal static readonly (string Key,string Variable)[] Keys=
        {
            ("use_gpu","INU_GPU_EXPERIMENTAL"),("reproducible_sums","INU_GPU_REPRODUCIBLE_SUMS"),
            ("device","INU_GPU_DEVICE"),("device_name",null),("backend","INU_GPU_BACKEND"),("allow_integrated",null),
            ("fallback_on_startup","INU_GPU_FALLBACK_ON_STARTUP"),("nvrtc_dir","INU_GPU_NVRTC_DIR"),("hiprtc_dir","INU_GPU_HIPRTC_DIR"),
            ("chunk","INU_GRAL_GPU_CHUNK"),("block","INU_GRAL_GPU_BLOCK"),("max_kernel_ms","INU_GPU_MAX_KERNEL_MS"),
            ("memory_budget_bytes","INU_GPU_MEMORY_BUDGET_BYTES"),("memory_reserve_bytes","INU_GPU_MEMORY_RESERVE_BYTES"),
            ("pinned_budget_bytes","INU_GPU_PINNED_BUDGET_BYTES"),("cache_dir","INU_GPU_CACHE_DIR")
        };
        private static readonly object sync=new object();
        private static Dictionary<string,string> file;
        /// <summary>The file that was looked at (full path); null when INU_GPU_SETTINGS_FILE=off.</summary>
        internal static string FilePath { get; private set; }
        /// <summary>The file for messages and the log: GPU.txt for the project's file in the working directory, else the full path.</summary>
        internal static string FileName { get; private set; }
        /// <summary>read, absent or off.</summary>
        internal static string FileStatus { get; private set; }
        /// <summary>
        /// When the project's GPU.txt is absent: a file of the working directory with that name in another case (gpu.txt), else null.
        /// It is not read, because file names are case-sensitive on Linux, as for GRAL's other input files; its name is kept for a hint.
        /// </summary>
        internal static string OtherCaseFile { get; private set; }
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
                    // The project's file: a run has the Computation folder as its working directory
                    path=Path.Combine(Directory.GetCurrentDirectory(),"GPU.txt");
                    FileName="GPU.txt";
                }
                else if(path.Equals("off",StringComparison.OrdinalIgnoreCase)) { FilePath=FileName=null; FileStatus="off"; file=new Dictionary<string,string>(); return; }
                else
                {
                    // A value that is no path at all is reported, with the text as given and the status of a file that is not there
                    FilePath=FileName=path; FileStatus="absent";
                    // The variable may name the project's own file (a front end can set it for every start): that is GPU.txt, without its path
                    try
                    {
                        path=FileName=Path.GetFullPath(path);
                        if(path.Equals(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),"GPU.txt")),OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) FileName="GPU.txt";
                    }
                    catch(Exception error) when(error is ArgumentException || error is IOException || error is NotSupportedException || error is System.Security.SecurityException)
                    { throw GpuCuda.Status(new ArgumentException("INU_GPU_SETTINGS_FILE is not a usable path: "+error.Message,error),"invalid_setting"); }
                }
                FilePath=path;
                var values=new Dictionary<string,string>(StringComparer.Ordinal);
                if(!File.Exists(path))
                {
                    if(FileName=="GPU.txt")
                        try { OtherCaseFile=Directory.EnumerateFiles(Path.GetDirectoryName(path)).Select(Path.GetFileName).FirstOrDefault(name=>name!="GPU.txt" && name.Equals("GPU.txt",StringComparison.OrdinalIgnoreCase)); }
                        catch(Exception error) when(error is IOException || error is UnauthorizedAccessException) { }
                    FileStatus="absent"; file=values; return;
                }
                FileStatus="read";
                byte[] bytes;
                try { bytes=File.ReadAllBytes(path); }
                catch(Exception error) when(error is IOException || error is UnauthorizedAccessException)
                { throw GpuCuda.Status(new ArgumentException("GPU settings file "+FileName+" cannot be read: "+error.Message,error),"invalid_setting"); }
                // The file is UTF-8 (with or without a byte order mark). A UTF-16 or UTF-32 file is refused, not guessed at: a front end
                // that reads the file as UTF-8 would otherwise see other settings than GRAL. Such a file has a UTF-16 mark or NUL bytes.
                if((bytes.Length>=2 && ((bytes[0]==0xFF && bytes[1]==0xFE) || (bytes[0]==0xFE && bytes[1]==0xFF))) || Array.IndexOf(bytes,(byte)0)>=0)
                    throw GpuCuda.Status(new ArgumentException("GPU settings file "+FileName+" is not a UTF-8 text file (UTF-16 or UTF-32, or it contains NUL bytes); save it as UTF-8"),"invalid_setting");
                // Lines end with CR LF, LF or CR, as File.ReadAllLines splits them
                var lines=new List<string>();
                using(var reader=new StringReader(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF')))
                    for(string text=reader.ReadLine();text!=null;text=reader.ReadLine()) lines.Add(text);
                for(int i=0;i<lines.Count;i++)
                {
                    string line=lines[i].Split('!')[0].Trim();
                    if(line.Length==0 || line[0]=='#') continue;
                    string where="GPU settings file "+FileName+" line "+(i+1).ToString(System.Globalization.CultureInfo.InvariantCulture)+": ";
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
                case "use_gpu":
                case "reproducible_sums":
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
            // The two switches keep the rules their variables always had: INU_GPU_EXPERIMENTAL counts only as 1 or 0 (another value
            // is ignored and reported with the decision), and blanks around INU_GPU_REPRODUCIBLE_SUMS do not count
            if(key=="use_gpu" && fromVariable!="1" && fromVariable!="0") fromVariable=null;
            if(key=="reproducible_sums") fromVariable=fromVariable?.Trim();
            if(!string.IsNullOrEmpty(fromVariable)) return (fromVariable,"env");
            return file.TryGetValue(key,out string fromFile) ? (fromFile,"file") : (fromVariable,null);
        }
        internal static string Value(string key) => Get(key).Value;
        /// <summary>The value the file gives a key, whatever its variable says; null when the file does not set it.</summary>
        internal static string FileValue(string key)
        {
            Load();
            return file.TryGetValue(key,out string value) ? value : null;
        }
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
        internal static string Label(string key) => Get(key).Source=="file" ? key+" in "+FileName : Keys.First(k=>k.Key==key).Variable ?? key;
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
