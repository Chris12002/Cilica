using System;
namespace Cilica.Fixtures.Execution
{
    public static class ArrayLengthCompareProbe
    {
        private static int Observe(bool value, int other) => value ? other : ~other;
        private static int[] Make(int a, ref int trace)
        { trace = unchecked(trace * 10 + 1); return a < 0 ? null : new int[a & 3]; }
        public static int Nonempty(int a, int b) => Observe(new int[a & 3].Length > 0, b);
        public static int Empty(int a, int b) => Observe(new string[a & 3].Length == 0, b);
        public static int SelectedArray(int a, int b)
        {
            var left = new long[a & 3]; var right = new long[b & 3];
            return Observe((a < b ? left : right).Length > 0, a);
        }
        public static int FaultOrder(int a, int b)
        {
            int trace = 0;
            try { return Observe(Make(a, ref trace).Length > 0, trace + a / b); }
            catch (NullReferenceException) { return 100 + trace; }
            catch (DivideByZeroException) { return 200 + trace; }
            catch (OverflowException) { return 300 + trace; }
        }
    }
}
