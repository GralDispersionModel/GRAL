using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

class Harness
{
    static Type program = null!;
    static Assembly assembly = null!;
    static object? Get(string n) => program.GetField(n)!.GetValue(null);
    static void Set(string n, object v) => program.GetField(n)!.SetValue(null, v);
    static void Element(string n, int i, object v)
    {
        var a = (Array)Get(n)!; a.SetValue(Convert.ChangeType(v, a.GetType().GetElementType()!), i);
    }
    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        assembly = Assembly.LoadFrom(Path.GetFullPath(args[0])); program = assembly.GetType("GRAL_2001.Program")!;
        int assertions = 0;
        var lookup = assembly.GetType("GRAL_2001.ParticleSourceLookup");
        if (lookup != null)
        {
            var build = lookup.GetMethod("BuildEnds", BindingFlags.Static | BindingFlags.NonPublic)!;
            var find = (Func<int[], int, int>)lookup.GetMethod("FindSource", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate(typeof(Func<int[], int, int>));
            var rng = new Random(1537);
            for (int trial = 0; trial < 250; trial++)
            {
                int count = rng.Next(0, 200); var counts = new int[count + 1];
                for (int s = 1; s <= count; s++) counts[s] = rng.Next(0, 8);
                var ends = (int[])build.Invoke(null, new object[] { counts, count })!;
                for (int p = -1; p <= counts.Sum() + 1; p++)
                {
                    int expected = 0, sum = 0;
                    if (p > 0) for (int s = 1; s <= count; s++) { sum += counts[s]; if (p <= sum) { expected = s; break; } }
                    if (find(ends, p) != expected) throw new Exception("source mapping");
                    assertions++;
                }
            }
            var large = (int[])build.Invoke(null, new object[] { new[] { 0, 0, int.MaxValue - 1, 0, 1 }, 4 })!;
            if (find(large, int.MaxValue) != 4 || find(large, int.MaxValue - 1) != 2) throw new Exception("large ID boundary");
            assertions += 2;
        }
        var results = new List<object>();
        foreach (var size in new[] { 1, 100, 10000 })
        {
            int total = 0;
            foreach (string kind in new[] { "PS", "TS", "LS", "AS" })
            {
                Set(kind + "_Count", size);
                foreach (var f in program.GetFields().Where(f => f.Name.StartsWith(kind + "_") && f.FieldType.IsArray && f.FieldType.GetArrayRank() == 1 && !f.FieldType.GetElementType()!.IsArray))
                    f.SetValue(null, Array.CreateInstance(f.FieldType.GetElementType()!, size + 1));
                int sum = 0;
                for (int i = 1; i <= size; i++)
                {
                    int n = i % 3 == 0 ? 0 : 2; sum += n; Element(kind + "_PartNumb", i, n);
                    Element(kind + "_SG", i, 1); Element(kind + "_ER", i, 1);
                    if (kind == "AS") foreach (var field in new[] { "X", "Y", "Z", "dX", "dY", "dZ" }) Element(kind + "_" + field, i, field == "Z" ? 3 : 8);
                    else if (kind == "PS") { Element("PS_X", i, 20); Element("PS_Y", i, 20); Element("PS_Z", i, 3); Element("PS_D", i, 1); }
                    else { foreach (var field in new[] { "X1", "Y1", "Z1", "X2", "Y2", "Z2" }) Element(kind + "_" + field, i, field == "Z1" ? 1 : field == "Z2" ? 3 : field == "X2" ? 30 : 20); }
                }
                Set(kind + "_PartSum", sum); total += sum;
            }
            Set("NTEILMAX", total); Set("GridVolume", 400f); Set("TAUS", 30f); Set("UseFixedRndSeedVal", true); Set("ISTATIONAER", 1); Set("Topo", 0); Set("BuildingsExist", false);
            Set("XsiMaxGral", 200f); Set("EtaMaxGral", 200f); ((ParallelOptions)Get("pOptions")!).MaxDegreeOfParallelism = 1;
            foreach (string n in new[] { "ParticleSource", "ParticleSG", "Xcoord", "YCoord", "ZCoord", "ParticleMass", "SourceType", "ParticleVsed", "ParticleVdep", "ParticleMode" })
            {
                var f = program.GetField(n)!; f.SetValue(null, Array.CreateInstance(f.FieldType.GetElementType()!, total + 1));
            }
            var calculate = (Action)assembly.GetType("GRAL_2001.StartCoordinates")!.GetMethod("Calculate")!.CreateDelegate(typeof(Action));
            calculate(); var ms = new List<double>(); long bytes = 0;
            for (int repeat = 0; repeat < 5; repeat++)
            {
                long before = GC.GetTotalAllocatedBytes(true); var sw = Stopwatch.StartNew(); calculate(); sw.Stop();
                bytes += GC.GetTotalAllocatedBytes(true) - before; ms.Add(sw.Elapsed.TotalMilliseconds);
            }
            var hashes = new Dictionary<string,string>();
            foreach (string n in new[] { "ParticleSource", "ParticleSG", "Xcoord", "YCoord", "ZCoord", "ParticleMass", "SourceType", "ParticleVsed", "ParticleVdep", "ParticleMode" })
            {
                var a=(Array)Get(n)!; byte[] b=new byte[Buffer.ByteLength(a)]; Buffer.BlockCopy(a,0,b,0,b.Length); hashes[n]=Convert.ToHexString(SHA256.HashData(b));
            }
            results.Add(new { sources_per_type = size, particles = total, median_ms = ms.Order().ElementAt(2), samples_ms = ms, allocated_bytes = bytes/5, hashes });
        }
        File.WriteAllText(args[1], JsonSerializer.Serialize(new { assertions, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS GRAL lookup " + assertions);
    }
}
