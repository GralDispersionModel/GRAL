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
            // Windows: FileShare.Read keeps the log readable during the run and still blocks a second writer.
            // Linux: share modes are advisory only, so FileShare.None is used to make a second process fail over to its own file.
            FileShare share=Program.RunOnUnix ? FileShare.None : FileShare.Read;
            CreateFolder(path);
            try { actualPath=path;return new FileStream(path,FileMode.Append,FileAccess.Write,share); }
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
            CreateFolder(path);
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
        // A log path in a folder that does not exist yet is created rather than failing the GPU start.
        private static void CreateFolder(string path) => Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        internal static string UniquePath(string path)
        {
            string folder=Path.GetDirectoryName(Path.GetFullPath(path));
            string name=Path.GetFileNameWithoutExtension(path)+"."+Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+
                        DateTime.UtcNow.ToString("yyyyMMddHHmmssfff",System.Globalization.CultureInfo.InvariantCulture)+Path.GetExtension(path);
            return Path.Combine(folder,name);
        }
    }
}
