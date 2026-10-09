using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class WideRootResult
    {
        public int Sum;
        public WideRootResult(ValueVectorBox p0, ValueVectorBox p1, ValueVectorBox p2, ValueVectorBox p3, ValueVectorBox p4, ValueVectorBox p5, ValueVectorBox p6, ValueVectorBox p7, ValueVectorBox p8, ValueVectorBox p9, ValueVectorBox p10, ValueVectorBox p11, ValueVectorBox p12, ValueVectorBox p13, ValueVectorBox p14, ValueVectorBox p15, ValueVectorBox p16, ValueVectorBox p17, ValueVectorBox p18, ValueVectorBox p19, ValueVectorBox p20, ValueVectorBox p21, ValueVectorBox p22, ValueVectorBox p23, ValueVectorBox p24, ValueVectorBox p25, ValueVectorBox p26, ValueVectorBox p27)
        {
            Sum = unchecked(p0.Number + p1.Number + p2.Number + p3.Number + p4.Number + p5.Number + p6.Number + p7.Number + p8.Number + p9.Number + p10.Number + p11.Number + p12.Number + p13.Number + p14.Number + p15.Number + p16.Number + p17.Number + p18.Number + p19.Number + p20.Number + p21.Number + p22.Number + p23.Number + p24.Number + p25.Number + p26.Number + p27.Number);
        }
    }
    public static class WideRootProbe
    {
        private static WideRootResult Build(int a, int b, ValueVectorBox first, ValueVectorBox second) =>
            new WideRootResult(
                first ?? new ValueVectorBox(unchecked(a + b + 0)),
                second ?? new ValueVectorBox(unchecked(a + b + 1)),
                first ?? new ValueVectorBox(unchecked(a + b + 2)),
                second ?? new ValueVectorBox(unchecked(a + b + 3)),
                first ?? new ValueVectorBox(unchecked(a + b + 4)),
                second ?? new ValueVectorBox(unchecked(a + b + 5)),
                first ?? new ValueVectorBox(unchecked(a + b + 6)),
                second ?? new ValueVectorBox(unchecked(a + b + 7)),
                first ?? new ValueVectorBox(unchecked(a + b + 8)),
                second ?? new ValueVectorBox(unchecked(a + b + 9)),
                first ?? new ValueVectorBox(unchecked(a + b + 10)),
                second ?? new ValueVectorBox(unchecked(a + b + 11)),
                first ?? new ValueVectorBox(unchecked(a + b + 12)),
                second ?? new ValueVectorBox(unchecked(a + b + 13)),
                first ?? new ValueVectorBox(unchecked(a + b + 14)),
                second ?? new ValueVectorBox(unchecked(a + b + 15)),
                first ?? new ValueVectorBox(unchecked(a + b + 16)),
                second ?? new ValueVectorBox(unchecked(a + b + 17)),
                first ?? new ValueVectorBox(unchecked(a + b + 18)),
                second ?? new ValueVectorBox(unchecked(a + b + 19)),
                first ?? new ValueVectorBox(unchecked(a + b + 20)),
                second ?? new ValueVectorBox(unchecked(a + b + 21)),
                first ?? new ValueVectorBox(unchecked(a + b + 22)),
                second ?? new ValueVectorBox(unchecked(a + b + 23)),
                first ?? new ValueVectorBox(unchecked(a + b + 24)),
                second ?? new ValueVectorBox(unchecked(a + b + 25)),
                first ?? new ValueVectorBox(unchecked(a + b + 26)),
                second ?? new ValueVectorBox(unchecked(a + b + 27)));
        public static int Run(int a, int b)
        {
            var first = a > b ? new ValueVectorBox(a) : null;
            var second = a < b ? new ValueVectorBox(b) : null;
            return Build(a, b, first, second).Sum;
        }
    }
}
