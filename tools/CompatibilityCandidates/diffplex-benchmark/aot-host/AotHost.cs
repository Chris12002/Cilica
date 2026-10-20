using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Cilica.CandidateBenchmark;

// NativeAOT (PublishAot) and default-JIT reference host. Benchmark calls and timing match DotNetHost.cs; JSON
// lines are written directly so no reflection serializer enters the program.
class Program
{
    static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    static void Main(string[] args)
    {
        string mode = args[0]; int work = int.Parse(args[1], CultureInfo.InvariantCulture);
        if (mode == "verify")
        {
            int length = BenchProbe.Run(work, -1); var values = new int[length];
            for (int i = 0; i < length; i++) values[i] = BenchProbe.Run(work, -i - 2);
            var text = new StringBuilder("{\"values\":[");
            for (int i = 0; i < length; i++) text.Append(i == 0 ? "" : ",").Append(values[i].ToString(CultureInfo.InvariantCulture));
            Console.WriteLine(text.Append("],\"checksum\":").Append(BenchProbe.Run(work, 1).ToString(CultureInfo.InvariantCulture)).Append('}').ToString());
            return;
        }
        int iterations = int.Parse(args[2], CultureInfo.InvariantCulture);
        // Optional warm-up milliseconds; 300 matches DotNetHost. Tiered JIT needs longer to settle.
        int warmMs = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 300;
        long warmStart = Stopwatch.GetTimestamp(); int warmIterations = 0;
        do { _ = BenchProbe.Run(work, 8); warmIterations += 8; }
        while (Stopwatch.GetElapsedTime(warmStart).TotalMilliseconds < warmMs);
        Console.WriteLine("{\"warmIterations\":" + warmIterations.ToString(CultureInfo.InvariantCulture)
            + ",\"warm_ms\":" + N(Stopwatch.GetElapsedTime(warmStart).TotalMilliseconds) + "}");
        for (int sample = 0; sample < 7; sample++)
        {
            long allocated = GC.GetTotalAllocatedBytes(true); int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            long start = Stopwatch.GetTimestamp(); int checksum = BenchProbe.Run(work, iterations);
            double ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds;
            Console.WriteLine("{\"sample\":" + sample.ToString(CultureInfo.InvariantCulture) + ",\"iterations\":" + iterations.ToString(CultureInfo.InvariantCulture)
                + ",\"ns\":" + N(ns) + ",\"checksum\":" + checksum.ToString(CultureInfo.InvariantCulture)
                + ",\"allocated_bytes\":" + (GC.GetTotalAllocatedBytes(true) - allocated).ToString(CultureInfo.InvariantCulture)
                + ",\"gen0\":" + (GC.CollectionCount(0) - g0).ToString(CultureInfo.InvariantCulture)
                + ",\"gen1\":" + (GC.CollectionCount(1) - g1).ToString(CultureInfo.InvariantCulture)
                + ",\"gen2\":" + (GC.CollectionCount(2) - g2).ToString(CultureInfo.InvariantCulture) + "}");
        }
    }
}
