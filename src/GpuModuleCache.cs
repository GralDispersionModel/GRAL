using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GRAL_2001
{
    /// <summary>Bounded on-disk cache of the compiled kernel module (PTX or CUBIN); the folder comes from Directory().</summary>
    internal static class GpuModuleCache
    {
        internal const int Schema = 2;
        private const int MaxImage = 64 * 1024 * 1024;
        private const int MaxEntry = 96 * 1024 * 1024;

        internal static string Digest(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

        private static unsafe uint GetDriveType(char* rootPath)
        {
            IntPtr kernel32 = NativeLibrary.Load("kernel32.dll");
            try { return ((delegate* unmanaged<char*, uint>)NativeLibrary.GetExport(kernel32, "GetDriveTypeW"))(rootPath); }
            finally { NativeLibrary.Free(kernel32); }
        }

        /// <summary>Local absolute directories only: removable, fixed or RAM disk on Windows, no network shares.</summary>
        private static unsafe bool LocalDirectory(string path)
        {
            if (!Path.IsPathFullyQualified(path)) return false;
            if (!OperatingSystem.IsWindows()) return true;
            string root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal) || root.StartsWith("//", StringComparison.Ordinal)) return false;
            fixed (char* r = root + "\0") { uint type = GetDriveType(r); return type == 2 || type == 3 || type == 6; }
        }

        /// <summary>
        /// Cache folder: INU_GPU_CACHE_DIR or the cache_dir key of GPU.txt, or by default a per-user local folder
        /// (LocalApplicationData/GRAL/GpuModuleCache); the value off disables the cache. Null when disabled.
        /// </summary>
        internal static string Directory()
        {
            string directory = GpuSettings.Value("cache_dir");
            if (directory != null && (directory.Length == 0 || directory.Equals("off", StringComparison.OrdinalIgnoreCase))) return null;
            if (directory != null) return directory;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrEmpty(local) ? null : Path.Combine(local, "GRAL", "GpuModuleCache");
        }

        /// <summary>Cache file and key for an identity, or (null, status) when the cache is disabled or the directory is unusable.</summary>
        internal static (string Path, string KeyOrStatus) CachePath(object identity)
        {
            string directory = Directory();
            if (directory == null) return (null, "disabled");
            if (!Path.IsPathFullyQualified(directory) || !LocalDirectory(directory)) return (null, "invalid_directory");
            string full;
            try { full = ResolveLinks(Path.GetFullPath(directory)); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { return (null, "invalid_directory"); }
            if (!LocalDirectory(full)) return (null, "invalid_directory");
            string key = Digest(JsonSerializer.SerializeToUtf8Bytes(identity));
            return (Path.Combine(full, key + ".json"), key);
        }

        /// <summary>The path with every existing symbolic link or junction resolved, so a link to a network share is seen as one.</summary>
        private static string ResolveLinks(string full)
        {
            string root = Path.GetPathRoot(full);
            string current = root;
            foreach (string part in full.Substring(root.Length).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                var info = new DirectoryInfo(current);
                if (info.Exists && info.LinkTarget != null && info.ResolveLinkTarget(true) is FileSystemInfo target) current = target.FullName;
            }
            return current;
        }

        private static bool ValidImage(byte[] image, string kind)
        {
            if (image.Length <= 1 || image.Length > MaxImage) return false;
            if (kind == "ptx") return image[^1] == 0 && Array.IndexOf(image, (byte)0) == image.Length - 1;
            return (kind == "cubin" || kind == "hsaco") && image.Length >= 4 && image[0] == 0x7f && image[1] == (byte)'E' && image[2] == (byte)'L' && image[3] == (byte)'F';
        }

        internal static (byte[] Image, string Status) Read(string path, string key, string kind)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return (null, "miss");
                if (info.Length > MaxEntry) throw new InvalidDataException("entry too large");
                using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
                var e = doc.RootElement;
                if (e.ValueKind != JsonValueKind.Object || e.GetProperty("schema").GetInt32() != Schema || e.GetProperty("key").GetString() != key)
                    throw new InvalidDataException("schema or key mismatch");
                if ((e.TryGetProperty("kind", out var k) ? k.GetString() : "ptx") != kind) throw new InvalidDataException("binary kind mismatch");
                int size = e.GetProperty("size").GetInt32();
                if (size <= 1 || size > MaxImage) throw new InvalidDataException("invalid image size");
                byte[] image = Convert.FromBase64String(e.GetProperty("image").GetString());
                if (image.Length != size || Digest(image) != e.GetProperty("sha256").GetString() || !ValidImage(image, kind))
                    throw new InvalidDataException("invalid image content");
                return (image, "hit");
            }
            catch (FileNotFoundException) { return (null, "miss"); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is FormatException ||
                                          error is InvalidOperationException || error is System.Collections.Generic.KeyNotFoundException || error is InvalidDataException ||
                                          error is ArgumentException || error is IndexOutOfRangeException)
            { return (null, "read_error"); }
        }

        internal static string Write(string path, string key, byte[] image, string kind)
        {
            if ((kind != "ptx" && kind != "cubin" && kind != "hsaco") || !ValidImage(image, kind)) return "write_error";
            string temp = null;
            try
            {
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
                var entry = new { schema = Schema, key, kind, size = image.Length, sha256 = Digest(image), image = Convert.ToBase64String(image) };
                temp = Path.Combine(Path.GetDirectoryName(path), ".module-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, entry);
                    stream.Flush(true);
                }
                File.Move(temp, path, true);
                temp = null;
                return "written";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { return "write_error"; }
            finally
            {
                if (temp != null) try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
