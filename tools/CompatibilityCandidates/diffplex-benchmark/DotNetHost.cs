using System;
using System.Diagnostics;
using System.Text.Json;
using Cilica.CandidateBenchmark;
class Program
{
    static void Main(string[] args)
    {
        string mode = args[0]; int work = int.Parse(args[1]);
        if (mode == "verify")
        {
            int length = BenchProbe.Run(work, -1); var values = new int[length];
            for (int i = 0; i < length; i++) values[i] = BenchProbe.Run(work, -i - 2);
            Console.WriteLine(JsonSerializer.Serialize(new { values, checksum = BenchProbe.Run(work, 1) })); return;
        }
        if (mode == "cold")
        {
            long start = Stopwatch.GetTimestamp(); int checksum = BenchProbe.Run(work, 1);
            double ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds;
            Console.WriteLine(JsonSerializer.Serialize(new { ns, checksum })); return;
        }
        int iterations = int.Parse(args[2]);
        // Tiering is disabled by the supervisor. Warm every reachable workload path
        // before sampling and require at least 300 ms of actual workload execution.
        long warmStart = Stopwatch.GetTimestamp(); int warmIterations = 0;
        do { _ = BenchProbe.Run(work, 8); warmIterations += 8; }
        while (Stopwatch.GetElapsedTime(warmStart).TotalMilliseconds < 300);
        Console.WriteLine(JsonSerializer.Serialize(new { warmIterations, warm_ms = Stopwatch.GetElapsedTime(warmStart).TotalMilliseconds }));
        for (int sample = 0; sample < 7; sample++)
        {
            long allocated = GC.GetTotalAllocatedBytes(true); int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            long start = Stopwatch.GetTimestamp(); int checksum = BenchProbe.Run(work, iterations);
            double ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds;
            Console.WriteLine(JsonSerializer.Serialize(new { sample, iterations, ns, checksum,
                allocated_bytes = GC.GetTotalAllocatedBytes(true) - allocated,
                gen0 = GC.CollectionCount(0)-g0, gen1 = GC.CollectionCount(1)-g1, gen2 = GC.CollectionCount(2)-g2 }));
        }
    }
}
