using System;
using System.Diagnostics;
namespace Cilica.SectionTiming
{
    // Diagnostic, single-threaded counters. No allocation inside a timed scope.
    public static class SectionClock
    {
        public static bool Enabled;
        public static readonly long[] Ticks = new long[4];
        public static readonly long[] Calls = new long[4];
        public static int Current = -1;
        private static long last;
        public static void Reset()
        {
            if (Current != -1) throw new InvalidOperationException("Unbalanced section");
            Array.Clear(Ticks, 0, 4); Array.Clear(Calls, 0, 4);
        }
        public struct Scope : IDisposable
        {
            private readonly bool active;
            private readonly int previous;
            public Scope(int section)
            {
                active = Enabled; previous = Current;
                if (!active) return;
                long now = Stopwatch.GetTimestamp();
                if (previous >= 0) Ticks[previous] += now - last;
                Current = section; last = now; ++Calls[section];
            }
            public void Dispose()
            {
                if (!active) return;
                long now = Stopwatch.GetTimestamp();
                Ticks[Current] += now - last;
                Current = previous; last = now;
            }
        }
    }
    [Serializable] public sealed class Sample
    {
        public int work, iterations, checksum;
        public bool timers;
        public long elapsedTicks, frequency;
        public long[] sectionTicks, calls;
        public int gen0, gen1, gen2;
    }
    [Serializable] public sealed class Result
    {
        public string error;
        public int[] values;
        public Sample[] samples;
    }
    public static class Runner
    {
        public static Result Run(string mode, int work, int iterations, bool timers)
        {
            if (work < 0 || work > 1 || iterations < 1 || iterations > 32768)
                throw new ArgumentOutOfRangeException();
            SectionClock.Enabled = timers;
            int expected = work == 0 ? 6146 : 3122;
            if (mode == "verify")
            {
                int count = CandidateBenchmark.BenchProbe.Run(work, -1);
                var values = new int[count];
                for (int i = 0; i < count; ++i)
                    values[i] = CandidateBenchmark.BenchProbe.Run(work, -i - 2);
                if (SectionClock.Current != -1) throw new Exception("Unbalanced verify");
                return new Result { values = values };
            }
            long warm = Stopwatch.GetTimestamp();
            do { CandidateBenchmark.BenchProbe.Run(work, 8); }
            while ((Stopwatch.GetTimestamp() - warm) * 1000.0 / Stopwatch.Frequency < 300);
            var samples = new Sample[7];
            for (int i = 0; i < samples.Length; ++i)
            {
                SectionClock.Reset();
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                long start = Stopwatch.GetTimestamp();
                int checksum = CandidateBenchmark.BenchProbe.Run(work, iterations);
                long elapsed = Stopwatch.GetTimestamp() - start;
                if (checksum != unchecked(expected * iterations) || SectionClock.Current != -1)
                    throw new Exception("Checksum or unbalanced section");
                long[] required = { iterations, 2L * iterations, 2L * iterations, iterations };
                for (int j = 0; j < 4; ++j)
                    if (SectionClock.Calls[j] != (timers ? required[j] : 0))
                        throw new Exception("Section coverage " + j);
                samples[i] = new Sample { work = work, iterations = iterations, checksum = checksum,
                    timers = timers, elapsedTicks = elapsed, frequency = Stopwatch.Frequency,
                    sectionTicks = (long[])SectionClock.Ticks.Clone(), calls = (long[])SectionClock.Calls.Clone(),
                    gen0 = GC.CollectionCount(0) - g0, gen1 = GC.CollectionCount(1) - g1, gen2 = GC.CollectionCount(2) - g2 };
            }
            return new Result { samples = samples };
        }
    }
}
