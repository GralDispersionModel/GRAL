using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Harness
{
    static Type program = null!;
    static Assembly assembly = null!;
    static readonly string[] kinds = { "PS", "LS", "TS", "AS" };
    static readonly List<object> results = new();
    static int assertions;
    static TextWriter output = Console.Out;
    static object? Get(string name) => program.GetField(name)!.GetValue(null);
    static void Set(string name, object? value) => program.GetField(name)!.SetValue(null, value);
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static object? Call(string type, string method, params object[] args)
    {
        try { return assembly.GetType("GRAL_2001." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null) { throw ex.InnerException; }
    }
    static void Element(string field, int index, object value)
    {
        Array a = (Array)Get(field)!;
        a.SetValue(Convert.ChangeType(value, a.GetType().GetElementType()!), index);
    }
    static int[] Counts(string kind) => (int[])Get(kind + "_PartNumb")!;
    static void Reset(int[]? ids = null)
    {
        ids ??= new[] { 1, 2 };
        var config = program.GetMethod("ConfigureSourceGroups");
        if (config != null) config.Invoke(null, new object[] { string.Join(",", ids) });
        else Set("SourceGroups", ids.ToList());
        Set("TPS", 100f); Set("TAUS", 30f); Set("GridVolume", 400f); Set("NTEILMAX", 3000);
        Set("ISTATIONAER", 0); Set("IWET", 1); Set("EmissionTimeseriesExist", true);
        Set("EmFacTimeSeries", new float[,] { { 1, 0 }, { 0, 1 }, { 0, 0 }, { .5f, 2 }, { 0, 0 } });
        Set("TransientDepo", null); Set("LogLevel", 0); Set("IOUTPUT", 1); Set("WaitForConsoleKey", false);
        foreach (string kind in kinds) Sources(kind, Array.Empty<double>(), Array.Empty<int>());
    }
    static void Sources(string kind, double[] rates, int[] groups)
    {
        Set(kind + "_Count", rates.Length);
        foreach (var field in program.GetFields().Where(f => f.Name.StartsWith(kind + "_") && f.FieldType.IsArray && f.FieldType.GetArrayRank() == 1 && !f.FieldType.GetElementType()!.IsArray))
            field.SetValue(null, Array.CreateInstance(field.FieldType.GetElementType()!, rates.Length + 1));
        Set(kind + "_PartSum", 0);
        for (int i = 0; i < rates.Length; i++) { Element(kind + "_ER", i + 1, rates[i]); Element(kind + "_SG", i + 1, groups[i]); }
    }
    static int Allocate() { int total = (int)Call("ParticleManagement", "Calculate")!; Set("NTEILMAX", total); return total; }
    static void Test(string name, Action action)
    {
        int before = assertions;
        try { action(); results.Add(new { name, status = "pass", assertions = assertions - before }); output.WriteLine("PASS " + name); }
        catch (Exception ex) { results.Add(new { name, status = "fail", error = ex.ToString(), assertions = assertions - before }); output.WriteLine("FAIL " + name + " " + ex.Message); }
    }
    static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string dll = Path.GetFullPath(args[0]); string root = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(root); Directory.SetCurrentDirectory(root);
        assembly = Assembly.LoadFrom(dll); program = assembly.GetType("GRAL_2001.Program")!;
        using var log = new StreamWriter("engine.log"); Console.SetOut(log);
        if (args.Contains("--baseline"))
        {
            Reset(); foreach (string kind in kinds) Sources(kind, new[] { 2d, 0, 5, 3 }, new[] { 1, 1, 2, 1 });
            int total = Allocate(); var allocations = kinds.ToDictionary(k => k, k => Counts(k));
            string? zeroError = null;
            try { Reset(); Sources("AS", new[] { 0d }, new[] { 1 }); Allocate(); } catch (Exception ex) { zeroError = ex.GetType().Name; }
            File.WriteAllText("baseline_reproduction.json", JsonSerializer.Serialize(new { total, allocations, all_zero_error = zeroError }, new JsonSerializerOptions { WriteIndented = true }));
            Console.SetOut(output); output.WriteLine("BASELINE " + JsonSerializer.Serialize(new { total, allocations, all_zero_error = zeroError })); return 0;
        }
        foreach (string kind in kinds) Test(kind + "_zero_rate_and_zero_factor", () =>
        {
            Reset(); Sources(kind, new[] { 2d, 0, 5, 3 }, new[] { 1, 1, 2, 1 });
            Check(Allocate() == 3000, "active budget"); Check(Counts(kind).SequenceEqual(new[] { 0, 1200, 0, 0, 1800 }), "zero source or group received particles");
        });
        Test("mixed_types_current_hour_and_budget_reset", () =>
        {
            Reset(); foreach (string kind in kinds) Sources(kind, new[] { 2d, 0, 5, 3 }, new[] { 1, 1, 2, 1 });
            Check(Allocate() == 3000, "mixed total"); double mean = (double)Get("ParticleMassMean")!;
            foreach (string kind in kinds) Check(Counts(kind).SequenceEqual(new[] { 0, 300, 0, 0, 450 }), "mixed active weights");
            Set("IWET", 2); Set("NTEILMAX", 999999); Check(Allocate() == 3000, "budget used previous allocated count");
            foreach (string kind in kinds) Check(Counts(kind).SequenceEqual(new[] { 0, 0, 0, 750, 0 }), "next hour mapping");
            Set("IWET", 3); Check(Allocate() == 0, "all-off new particles");
            Check((double)Get("ParticleMassMean")! == mean, "carryover reference changed on all-off hour");
            foreach (string kind in kinds) Check(Counts(kind).All(v => v == 0) && (int)Get(kind + "_PartSum")! == 0, "stale counts");
            Set("IWET", 4); Check(Allocate() == 3000, "reactivation");
            foreach (string kind in kinds) Check(Counts(kind).SequenceEqual(new[] { 0, 150, 0, 375, 225 }), "positive factors changed original relative sampling weights");
        });
        Test("all_zero_base_rates_and_no_sources", () =>
        {
            Reset(); foreach (string kind in kinds) Sources(kind, new[] { 0d }, new[] { 1 });
            Check(Allocate() == 0, "zero base rates"); Check(((Array)Get("ParticleMass")!).Length == 1, "zero-sized release storage");
            Reset(); Check(Allocate() == 0, "empty source set");
        });
        Test("minimum_counts_ignore_inactive_sources", () =>
        {
            foreach (string kind in kinds)
            {
                Reset(); Set("TPS", .01f); Set("NTEILMAX", 0);
                Sources(kind, Enumerable.Repeat(1d, 2001).ToArray(), Enumerable.Repeat(2, 2000).Append(1).ToArray());
                int expected = kind == "AS" ? 6 : kind == "LS" ? 16 : 20;
                Check(Allocate() == expected, kind + " minimum must use active count"); Check(Counts(kind).Take(2001).All(n => n == 0), "inactive minimum");
            }
        });
        Test("steady_and_missing_timeseries_keep_positive_sources", () =>
        {
            Reset(); Sources("PS", new[] { 1d, 1 }, new[] { 1, 2 }); Set("ISTATIONAER", 1);
            Check(Allocate() == 3000 && Counts("PS")[1] == 1500 && Counts("PS")[2] == 1500, "steady modulation");
            Set("ISTATIONAER", 0); Set("EmissionTimeseriesExist", false);
            Check(Allocate() == 3000 && Counts("PS")[2] == 1500, "missing temporal file");
            Set("EmissionTimeseriesExist", true); Set("IWET", 5);
            Check(Allocate() == 3000 && Counts("PS")[2] == 1500, "beyond temporal records fallback");
        });
        Test("deposition_only_actual_rate_and_reference", () =>
        {
            Reset(); Sources("PS", new[] { 2d, 6, .4, .4 }, new[] { 1, 1, 2, 2 });
            Element("PS_Mode", 1, 1); Element("PS_Mode", 2, 1); Element("PS_Mode", 3, 2); Element("PS_Mode", 4, 2);
            Element("PS_V_Dep", 1, .01f); Element("PS_V_Dep", 2, .03f); Element("PS_ER_Dep", 3, 8f);
            Set("IWET", 2); Allocate(); Check(Counts("PS")[3] == 3000 && Counts("PS")[4] == 0, "deposition-only eligibility");
            var dep = (Array)Get("TransientDepo")!; object first = dep.GetValue(0)!;
            Check((int)first.GetType().GetProperty("DepositionMode")!.GetValue(first)! == 1, "inactive-first group lost deposition settings");
            double velocity = (double)first.GetType().GetProperty("Vdep")!.GetValue(first)!;
            Check(velocity > .02 && velocity < .03, "reference deposition weighting");
            Set("IWET", 3); Allocate(); Check(ReferenceEquals(dep, Get("TransientDepo")), "all-off replaced carryover deposition");
        });
        Test("reordered_sparse_source_group_ids", () =>
        {
            Reset(new[] { 99, 5 }); Sources("AS", new[] { 1d, 1 }, new[] { 5, 99 });
            Check(Allocate() == 3000 && Counts("AS")[1] == 0 && Counts("AS")[2] == 3000, "external group used as temporal column");
            Set("IWET", 2); Check(Allocate() == 3000 && Counts("AS")[1] == 3000 && Counts("AS")[2] == 0, "sparse reactivation");
        });
        Test("partial_temporal_columns_and_last_record", () =>
        {
            Reset(); Sources("AS", new[] { 1d, 1 }, new[] { 1, 2 });
            Set("EmFacTimeSeries", new float[,] { { 0 }, { .5f }, { 0 } });
            Check(Allocate() == 3000 && Counts("AS")[1] == 0 && Counts("AS")[2] == 3000, "missing selected column default");
            Set("IWET", 2); Check(Allocate() == 3000 && Counts("AS")[1] == 1500, "last temporal record skipped");
        });
        Test("array_storage_reused_at_same_count", () =>
        {
            Reset(); Sources("AS", new[] { 1d, 1 }, new[] { 1, 2 }); Allocate(); object previous = Get("ParticleMass")!;
            Set("IWET", 2); Allocate(); Check(ReferenceEquals(previous, Get("ParticleMass")), "needless same-size allocation");
        });
        log.Flush(); Console.SetOut(output);
        int failed = results.Count(r => JsonSerializer.Serialize(r).Contains("\"status\":\"fail\""));
        File.WriteAllText("results.json", JsonSerializer.Serialize(new { dll, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))), assertions, tests = results, failed }, new JsonSerializerOptions { WriteIndented = true }));
        output.WriteLine($"tests {results.Count} assertions {assertions} failed {failed}"); return failed == 0 ? 0 : 1;
    }
}
