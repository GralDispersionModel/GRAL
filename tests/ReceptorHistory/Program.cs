using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

class Tests
{
    static Action<string,string[],Encoding,int,string?,string?,bool> rewrite = null!;
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static void Legacy(string path, string[] header, Encoding encoding, int row, string? replacement, string? statistics, bool filter)
    {
        var content = new List<string>();
        if (File.Exists(path))
        {
            using var reader = new StreamReader(path);
            for(int i=0;i<header.Length;i++) reader.ReadLine();
            string? line;
            while((line=reader.ReadLine()) != null)
                if (!filter || !line.StartsWith("Est. statistical error")) content.Add(line);
        }
        while(content.Count < row) content.Add(" ");
        if (replacement != null) content[row-1]=replacement;
        using(var writer = new StreamWriter(path,false,encoding))
        {
            foreach(var line in header) writer.WriteLine(line);
            foreach(var line in content) writer.WriteLine(line);
            if(statistics != null) { writer.WriteLine(""); writer.WriteLine(statistics); }
        }
        GC.KeepAlive(content);
    }
    static void Main(string[] a)
    {
        var core=Assembly.LoadFrom(Path.GetFullPath(a[0]));
        rewrite=core.GetType("GRAL_2001.ProgramWriters")!.GetMethod("RewriteReceptorHistory",BindingFlags.NonPublic|BindingFlags.Static)!
            .CreateDelegate<Action<string,string[],Encoding,int,string?,string?,bool>>();
        var folder=Path.GetFullPath(a[1]); Directory.CreateDirectory(folder);
        if(a.Length>2) { Benchmark(folder,a[2]); return; }
        foreach(var encoding in new Encoding[] { new UTF8Encoding(false),Encoding.Unicode })
        foreach(var filter in new[]{false,true})
        foreach(var initial in new[]{"missing","empty","short","rows","trailer"})
        foreach(var row in new[]{1,2,5,9})
        foreach(var stats in new[]{false,true})
        {
            string stem=$"{encoding.CodePage}_{filter}_{initial}_{row}_{stats}";
            string old=Path.Combine(folder,stem+"_legacy.dat"), current=Path.Combine(folder,stem+"_stream.dat");
            string[] header=Enumerable.Range(0,filter?5:6).Select(i=>$"Header {i}\t\u00b5g/m\u00b3\t\ud55c\uae00").ToArray();
            if(initial!="missing")
            {
                var lines=initial=="empty"?Array.Empty<string>():initial=="short"?header.Take(2).ToArray():header.Concat(new[]{"old1"," ","old3","old4"}).ToArray();
                if(initial=="trailer")lines=lines.Concat(new[]{"","Est. statistical error [%]\t1%"}).ToArray();
                File.WriteAllLines(old,lines,encoding);File.Copy(old,current);
            }
            string? replacement=stats?null:"new\t1.25\t-0\t";
            string? trailer=stats?"Est. statistical error [%]\tNA\t2.1%":null;
            Legacy(old,header,encoding,row,replacement,trailer,filter);
            rewrite(current,header,encoding,row,replacement,trailer,filter);
            Check(Hash(old)==Hash(current),stem);
        }
        string locked=Path.Combine(folder,"locked.dat");File.WriteAllText(locked,"keep original");string before=Hash(locked);
        using(var held=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            bool failed=false;
            try { rewrite(locked,new[]{"header"},Encoding.UTF8,1,"new",null,false); } catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) { failed=true; }
            Check(failed,"locked destination must fail");
        }
        Check(Hash(locked)==before,"locked original preserved");
        bool invalid=false;try{rewrite(locked,new[]{"header"},Encoding.UTF8,0,"bad",null,false);}catch(ArgumentOutOfRangeException){invalid=true;}
        Check(invalid && Hash(locked)==before,"invalid row preserved");
        string readBlocked=Path.Combine(folder,"read_locked.dat");File.WriteAllText(readBlocked,"read lock");
        using(var held=new FileStream(readBlocked,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            bool failed=false;try{rewrite(readBlocked,new[]{"header"},Encoding.UTF8,1,"bad",null,false);}catch(Exception e) when(e is IOException || e is UnauthorizedAccessException){failed=true;}
            Check(failed,"unreadable history fails before publication");
        }
        Check(File.ReadAllText(readBlocked)=="read lock","unreadable original preserved");
        Check(!Directory.EnumerateFiles(folder,"*.tmp").Any(),"temporary files cleaned");
        File.WriteAllText(Path.Combine(folder,"results.json"),JsonSerializer.Serialize(new{status="pass",checks,core_sha256=Hash(a[0])},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS receptor history checks={checks}");
    }
    static void Benchmark(string folder,string mode)
    {
        string path=Path.Combine(folder,"history.dat");
        string[] header=Enumerable.Range(0,5).Select(i=>$"header{i}").ToArray();
        string line=new string('1',4095)+'\t'; const int rows=12000;
        using(var write=new StreamWriter(path,false,Encoding.Unicode))
        {
            foreach(var h in header)write.WriteLine(h);
            for(int i=0;i<rows;i++)write.WriteLine(line);
        }
        // Warm both paths on small files before the measured large history update.
        string warm=Path.Combine(folder,"warm.dat");Legacy(warm,header,Encoding.Unicode,1,"warm",null,true);rewrite(warm,header,Encoding.Unicode,1,"warm",null,true);
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        long startManaged=GC.GetTotalMemory(false), peakManaged=startManaged, allocated=GC.GetTotalAllocatedBytes(true);
        using var cancel=new CancellationTokenSource();
        var monitor=Task.Run(()=>{while(!cancel.IsCancellationRequested){long m=GC.GetTotalMemory(false); if(m>peakManaged)peakManaged=m;Thread.Sleep(1);}});
        var timer=Stopwatch.StartNew();
        if(mode=="baseline") Legacy(path,header,Encoding.Unicode,rows/2,"replacement",null,true);
        else rewrite(path,header,Encoding.Unicode,rows/2,"replacement",null,true);
        timer.Stop();cancel.Cancel();monitor.Wait();
        long totalAllocated=GC.GetTotalAllocatedBytes(true)-allocated;
        long peakWorkingSet=Process.GetCurrentProcess().PeakWorkingSet64;
        // Streaming hash keeps file validation out of the measured memory peak.
        using var file=File.OpenRead(path);string hash=Convert.ToHexString(SHA256.HashData(file));
        var report=new{status="pass",mode,rows,row_characters=4096,bytes=file.Length,sha256=hash,seconds=timer.Elapsed.TotalSeconds,peak_working_set=peakWorkingSet,sampled_peak_managed=peakManaged,managed_before=startManaged,total_allocated_bytes=totalAllocated};
        File.WriteAllText(Path.Combine(folder,"benchmark.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(report));
    }
}
