using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GRAL_2001;
using GP = GRAL_2001.Program;

internal static class Harness
{
    static readonly List<object> Results = new();
    static int assertions;
    static TextWriter original = Console.Out;
    static string root = "";
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static void Test(string name, Action action)
    {
        var before = assertions;
        try { action(); Results.Add(new { name, status = "pass", assertions = assertions - before }); original.WriteLine("PASS " + name); }
        catch (Exception ex) { Results.Add(new { name, status = "fail", error = ex.ToString(), assertions = assertions - before }); original.WriteLine("FAIL " + name + " " + ex.Message); }
    }
    static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, name + " not rejected");
    }
    static void Config(IEnumerable<int> groups) => GP.ConfigureSourceGroups(string.Join(",", groups));
    static T Field<T>(string name) => (T)typeof(GP).GetField(name)!.GetValue(null)!;
    static void StaticReader(string name)
    {
        Type t = typeof(GP).Assembly.GetType("GRAL_2001." + name)!;
        t.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
    }
    static int[] ManyGroups() => Enumerable.Range(1, 297).Concat(new[] { 1294, 1001, 1295 }).ToArray();
    static void Sources(string kind)
    {
        int[] groups = ManyGroups(); Config(groups);
        GP.IKOOAGRAL = 0; GP.JKOOAGRAL = 0; GP.XsiMinGral = 0; GP.XsiMaxGral = 1000; GP.EtaMinGral = 0; GP.EtaMaxGral = 1000;
        GP.PS_Count = 0; GP.LS_Count = 0; GP.AS_Count = 0; GP.TS_Count = 0; GP.Odour = false;
        string file, cls, field; int headerCount; Func<int, string> row;
        switch (kind)
        {
            case "point": file = "point.dat"; cls = "ReadPointSources"; field = "PS"; headerCount = 2; row = g => $"100,100,10,1,0,0,0,1,1,293,{g}"; break;
            case "line": file = "line.dat"; cls = "ReadLineSources"; field = "LS"; headerCount = 5; row = g => $"1,1,{g},100,100,1,110,100,1,2,2,0,0,100"; break;
            case "area": file = "cadastre.dat"; cls = "ReadAreaSources"; field = "AS"; headerCount = 1; row = g => $"100,100,1,10,10,2,1,0,0,0,{g}"; break;
            default: file = "portals.dat"; cls = "ReadTunnelPortals"; field = "TS"; headerCount = 2; row = g => $"100,100,110,100,1,3,1,0,0,0,{g},0,1"; break;
        }
        File.WriteAllLines(file, Enumerable.Repeat("header", headerCount).Concat(groups.Select(row)).Append(row(1291)));
        StaticReader(cls);
        Check(Field<int>(field + "_Count") == 300, kind + " source count");
        int[] ids = Field<int[]>(field + "_SG"); double[] rates = Field<double[]>(field + "_ER");
        for (int i = 0; i < groups.Length; i++) { Check(ids[i + 1] == groups[i], kind + " SG mismatch index " + i); Check(Math.Abs(rates[i + 1] - 1) < 1e-6, kind + " emission mismatch"); }
    }
    static SourceGroupBuffer<float>[][][] Float4(int nx, int ny, int nz, int sg) => Enumerable.Range(0, nx).Select(_ => Enumerable.Range(0, ny).Select(_ => Enumerable.Range(0, nz).Select(_ => new SourceGroupBuffer<float>(sg)).ToArray()).ToArray()).ToArray();
    static SourceGroupBuffer<double>[][] Double3(int nx, int ny, int sg) => Enumerable.Range(0, nx).Select(_ => Enumerable.Range(0, ny).Select(_ => new SourceGroupBuffer<double>(sg)).ToArray()).ToArray();
    static void OutputSetup(int[] groups, int nx = 1)
    {
        Config(groups); GP.NXL = nx; GP.NYL = nx; GP.NS = 9; GP.GralDx = 10; GP.GralDy = 10;
        GP.IKOOAGRAL = 0; GP.JKOOAGRAL = 0; GP.IOUTPUT = 0; GP.ResultFileHeader = -1; GP.ResultFileZipped = false;
        GP.Conz3d = Float4(nx + 2, nx + 2, 9, groups.Length); GP.Depo_conz = Double3(nx + 2, nx + 2, groups.Length);
        GP.Conz3dp = Float4(nx + 2, nx + 2, 9, groups.Length); GP.Conz3dm = Float4(nx + 2, nx + 2, 9, groups.Length);
        GP.Q_cv0 = Enumerable.Range(0, 9).Select(_ => new float[groups.Length]).ToArray();
        GP.DisConcVar = Enumerable.Range(0, 9).Select(_ => new float[groups.Length]).ToArray();
        GP.DepositionExist = true; GP.WetDeposition = false; GP.Odour = false;
        if (nx > 0) for (int g = 0; g < groups.Length; g++) { GP.Depo_conz[1][1][g] = g + 2; for (int s = 0; s < 9; s++) GP.Conz3d[1][1][s][g] = g + 1; }
    }
    static void VerifyGrid(Stream stream, float expected)
    {
        using var br = new BinaryReader(stream);
        Check(br.ReadInt32() == -1, "binary header"); br.ReadInt32(); br.ReadInt32();
        Check(br.ReadSingle() == expected, "binary value"); Check(stream.Position == stream.Length, "unexpected trailing binary records");
    }
    static void Output(bool zipped)
    {
        var groups = new[] { 1, 99, 100, 101, 102, 255, 256, 744, 1294, 1001, 1295 }; OutputSetup(groups);
        GP.ResultFileZipped = zipped;
        int weather = zipped ? 3 : 2; string zip = "output.grz";
        new ProgramWriters().Write2DConcentrations(weather, zip);
        if (zipped)
        {
            using var archive = ZipFile.OpenRead(zip); Check(archive.Entries.Count == groups.Length * 10, "archive entry count");
            for (int g = 0; g < groups.Length; g++)
            {
                for (int s = 1; s <= 9; s++) { var entry = archive.GetEntry($"{weather:00000}-{s}{SourceGroupFileName.Encode(groups[g])}.con"); Check(entry != null, "zip con filename"); using var ms = new MemoryStream(); using (var es = entry!.Open()) es.CopyTo(ms); ms.Position = 0; VerifyGrid(ms, g + 1); }
                var dep = archive.GetEntry($"{weather:00000}-{SourceGroupFileName.Encode(groups[g])}.dep"); Check(dep != null, "zip dep filename"); using var dm = new MemoryStream(); using (var ds = dep!.Open()) ds.CopyTo(dm); dm.Position = 0; VerifyGrid(dm, g + 2);
            }
        }
        else
        {
            for (int g = 0; g < groups.Length; g++)
            {
                for (int s = 1; s <= 9; s++) { string file = $"{weather:00000}-{s}{SourceGroupFileName.Encode(groups[g])}.con"; Check(File.Exists(file), "con filename " + file); VerifyGrid(File.OpenRead(file), g + 1); }
                string dep = $"{weather:00000}-{SourceGroupFileName.Encode(groups[g])}.dep"; Check(File.Exists(dep), "dep filename"); VerifyGrid(File.OpenRead(dep), g + 2);
            }
        }
    }
    static void OdourNames()
    {
        // A zero-cell fixture exercises only filename/archive routing. It does not validate odour physics.
        int[] groups = { 99, 100, 256, 1001, 1295 }; OutputSetup(groups, 0); GP.Odour = true;
        new ProgramWriters().Write2DConcentrations(4, "unused.grz");
        foreach (int g in groups) for (int s = 1; s <= 9; s++) { string file = $"00004-{s}{SourceGroupFileName.Encode(g)}.odr"; Check(File.Exists(file), "odour filename " + file); Check(new FileInfo(file).Length == 4, "odour empty fixture header"); }
        GP.Odour = false;
    }
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--memory") return StorageTests.Benchmark(args);
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        root = Path.GetFullPath(args.Length > 0 ? args[0] : "results"); Directory.CreateDirectory(root); Directory.SetCurrentDirectory(root);
        GP.WaitForConsoleKey = false; GP.IOUTPUT = 1;
        using var log = new StreamWriter("harness_core.log", false); Console.SetOut(log); Console.SetError(log);
        Test("storage_blocks_accumulation_clear_and_parallel", () => StorageTests.Verify(Check));
        Test("configuration_sparse_ids", () => { Config(new[] { 1001, 1, 1295, 256 }); Check(GP.SourceGroups.SequenceEqual(new[] { 1001, 1, 1295, 256 }), "ordering"); Check(GP.DecayRate.Length == 4, "sparse compact decay"); Check(GP.Get_Internal_SG_Number(1295) == 2, "int32 mapping"); Check(GP.Get_Internal_SG_Number(2) == -1, "unknown mapping"); });
        Test("configuration_300_count", () => { Config(ManyGroups()); Check(GP.SourceGroups.Count == 300, "300 groups"); for (int i = 0; i < 300; i++) Check(GP.Get_Internal_SG_Number(ManyGroups()[i]) == i, "mapping"); });
        Test("configuration_invalid_atomic", () => { Config(new[] { 1001, 1 }); foreach (string value in new[] { "1296", "1,1", "0,1", "-1,1", "2147483648", "abc", "", "! empty" }) { Reject(() => GP.ConfigureSourceGroups(value), value); Check(GP.SourceGroups.SequenceEqual(new[] { 1001, 1 }), "invalid mutated list"); Check(GP.Get_Internal_SG_Number(1001) == 0, "invalid mutated map"); } });
        Test("configuration_comments", () => { GP.ConfigureSourceGroups("1001; 1;1295 ! 2,2"); Check(GP.SourceGroups.Count == 3, "comments"); });
        foreach (string kind in new[] { "point", "line", "area", "portal" }) Test(kind + "_300_sources", () => Sources(kind));
        Test("decay_sparse_headers", () => { Config(new[] { 1001, 1, 1295, 256 }); File.WriteAllText("Pollutant.txt", "NOx\n0\n0\n0.05\n1001:0.2\t1295:0.3\t1293:9\n"); new ProgramReaders().ReadPollutantTXT(); Check(GP.DecayRate.SequenceEqual(new[] { .2, .05, .3, .05 }), "decay mapping/defaults"); });
        Test("timeseries_sparse_reverse_unknown", () => { Config(new[] { 1001, 1, 1295 }); GP.ISTATIONAER = 0; File.WriteAllText("emissions_timeseries.txt", "Day,Hour,1292,1295,1,1001\n1,0,7,0.25,0.5,0.75\n1,1,8,1,0,2\n"); new ProgramReaders().ReadEmissionTimeseries(); Check(GP.EmFacTimeSeries[0, 0] == .75f, "sg1001"); Check(GP.EmFacTimeSeries[0, 1] == .5f, "sg1"); Check(GP.EmFacTimeSeries[0, 2] == .25f, "sgmax"); Check(GP.EmFacTimeSeries[1, 0] == 2, "hour2"); });
        Test("timeseries_300_columns", () => { var groups = ManyGroups(); Config(groups); File.WriteAllText("emissions_timeseries.txt", "Day,Hour," + string.Join(",", groups.Reverse()) + "\n1,0," + string.Join(",", groups.Reverse().Select((_, i) => i % 2)) + "\n"); new ProgramReaders().ReadEmissionTimeseries(); for (int i = 0; i < 300; i++) Check(GP.EmFacTimeSeries[0, i] == (299 - i) % 2, "300 modulation"); });
        Test("timeseries_duplicate_rejected", () => { Config(new[] { 1 }); File.WriteAllText("emissions_timeseries.txt", "Day,Hour,1,1\n1,0,1,1\n"); Reject(() => new ProgramReaders().ReadEmissionTimeseries(), "duplicate timeseries"); });
        Test("timeseries_nan_rejected", () => { Config(new[] { 1 }); File.WriteAllText("emissions_timeseries.txt", "Day,Hour,1\n1,0,NaN\n"); Reject(() => new ProgramReaders().ReadEmissionTimeseries(), "nan timeseries"); });
        Test("con_dep_binary_and_names", () => Output(false));
        Test("con_dep_archive_and_names", () => Output(true));
        Test("odour_filename_only", OdourNames);
        log.Flush(); Console.SetOut(original); Console.SetError(original);
        string assembly = typeof(GP).Assembly.Location;
        var report = new { assembly, assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))), assertions, tests = Results, scope = "Compiled assembly component tests. Odour fixture tests filename routing only. Full dispersion/restart/GUI behavior is outside this harness." };
        File.WriteAllText("harness_results.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        int failed = Results.Count(r => JsonSerializer.Serialize(r).Contains("\"status\":\"fail\""));
        original.WriteLine($"tests {Results.Count} assertions {assertions} failed {failed}"); return failed == 0 ? 0 : 1;
    }
}
