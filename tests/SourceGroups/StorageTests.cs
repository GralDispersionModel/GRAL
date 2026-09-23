using System.Diagnostics;
using System.Text.Json;
using GRAL_2001;

internal static class StorageTests
{
    public static void Verify(Action<bool, string> check)
    {
        foreach (int count in new[] { 1, 99, 100, 101, 300, 1295, 1296 })
        {
            var actual = new SourceGroupBuffer<float>(count);
            var expected = new float[count];
            var random = new Random(95);
            for (int n = 0; n < 20000; n++)
            {
                int id = random.Next(count);
                float change = n % 7 == 0 ? -expected[id] : (float)(random.NextDouble() * 3);
                actual[id] += change; expected[id] += change;
                check(actual[id] == expected[id], "accumulation " + count);
            }
            for (int id = 0; id < count; id++) check(actual[id] == expected[id], "dense transition");
            actual.Clear();
            for (int id = 0; id < count; id++) check(actual[id] == 0, "cleared cell");
            actual[count - 1] = 4;
            check(actual[count - 1] == 4 && (count == 1 || actual[0] == 0), "reuse after clear");
            bool rejected = false;
            try { actual[count] = 1; } catch (IndexOutOfRangeException) { rejected = true; }
            check(rejected, "upper boundary");
        }
        // Exercise the direct first block before random writes cause dense promotion.
        foreach (int first in new[] { 0, 31, 32, 1024, 1294 })
        {
            var cell = new SourceGroupBuffer<double>(1295);
            object gate = cell.SyncRoot;
            cell[first] = 2.5;
            for (int i = 0; i < cell.Length; i++) check(cell[i] == (i == first ? 2.5 : 0), "single block read");
            int other = first < 1000 ? 1294 : 0;
            cell[other] = 0; // Writing zero outside the first block must keep its values.
            check(cell[first] == 2.5 && cell[other] == 0, "zero outside first block");
            cell[other] = -3.25;
            check(cell[first] == 2.5 && cell[other] == -3.25, "first block directory promotion");
            for (int i = 0; i < cell.Length; i++) cell[i] += i;
            for (int i = 0; i < cell.Length; i++) check(cell[i] == i + (i == first ? 2.5 : i == other ? -3.25 : 0), "first block dense promotion");
            cell.Clear(); cell[other] = 9;
            check(ReferenceEquals(gate, cell.SyncRoot) && cell[first] == 0 && cell[other] == 9, "stable lock after clear");
        }
        var concurrent = new SourceGroupBuffer<double>(1295);
        Parallel.For(0, 16, worker =>
        {
            for (int n = 0; n < 1024; n++)
                lock (concurrent.SyncRoot) { concurrent[1294] += 1; concurrent[worker * 32] += .5; }
        });
        check(concurrent[1294] == 16384, "parallel accumulation");
        for (int w = 0; w < 16; w++) check(concurrent[w * 32] == 512, "parallel group separation");
    }

    // Isolated-process comparison of storage only, not a simulation performance claim.
    public static int Benchmark(string[] args)
    {
        bool blocked = args[1] == "blocks";
        int groups = int.Parse(args[2]), occupied = int.Parse(args[3]), cells = int.Parse(args[4]);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetTotalMemory(true);
        var timer = Stopwatch.StartNew();
        object keep;
        double sum = 0;
        if (blocked)
        {
            var data = new SourceGroupBuffer<float>[cells];
            for (int i = 0; i < cells; i++) data[i] = new SourceGroupBuffer<float>(groups);
            for (int i = 0; i < cells; i++)
                for (int g = 0; g < occupied; g++) data[i][g] += 1.25f;
            for (int i = 0; i < cells; i++)
                for (int g = 0; g < groups; g++) sum += data[i][g];
            keep = data;
        }
        else
        {
            var data = new float[cells][];
            for (int i = 0; i < cells; i++) data[i] = new float[groups];
            for (int i = 0; i < cells; i++)
                for (int g = 0; g < occupied; g++) data[i][g] += 1.25f;
            for (int i = 0; i < cells; i++)
                for (int g = 0; g < groups; g++) sum += data[i][g];
            keep = data;
        }
        timer.Stop();
        long bytes = GC.GetTotalMemory(true) - before;
        Console.WriteLine(JsonSerializer.Serialize(new { mode = args[1], groups, occupied, cells,
            managed_bytes = bytes, elapsed_ms = timer.Elapsed.TotalMilliseconds,
            peak_working_set = Process.GetCurrentProcess().PeakWorkingSet64, sum }));
        GC.KeepAlive(keep);
        return sum == cells * occupied * 1.25 ? 0 : 1;
    }
}
