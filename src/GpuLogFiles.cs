using System;
using System.IO;
namespace GRAL_2001
{
    // GPU log and metrics files: a rerun after a crash appends, a concurrent GRAL process gets its own file.
    internal static class GpuLogFiles
    {
        // Opens path for appending as the only writer; if another process writes it, opens a suffixed file instead.
        internal static FileStream OpenExclusive(string path,out string actualPath)
        {
            try { actualPath=path;return new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read); }
            catch(IOException) when(File.Exists(path))
            {
                actualPath=UniquePath(path);
                Console.WriteLine("GPU log "+path+" is in use by another process; writing "+actualPath);
                return new FileStream(actualPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
            }
        }
        // Claims path for this process with a lock file that disappears when the process ends; returns the path to write.
        internal static string Claim(string path,out FileStream lockFile)
        {
            lockFile=TryLock(path);
            if(lockFile!=null)return path;
            string unique=UniquePath(path);
            lockFile=TryLock(unique) ?? throw new IOException("Cannot claim GPU log file "+unique);
            Console.WriteLine("GPU log "+path+" is in use by another process; writing "+unique);
            return unique;
        }
        private static FileStream TryLock(string path)
        {
            string lockPath=path+".lock";
            try { return new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1,FileOptions.DeleteOnClose); }
            catch(IOException) when(File.Exists(lockPath)) { return null; }
        }
        internal static string UniquePath(string path)
        {
            string folder=Path.GetDirectoryName(Path.GetFullPath(path));
            string name=Path.GetFileNameWithoutExtension(path)+"."+Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+
                        DateTime.UtcNow.ToString("yyyyMMddHHmmssfff",System.Globalization.CultureInfo.InvariantCulture)+Path.GetExtension(path);
            return Path.Combine(folder,name);
        }
    }
}
