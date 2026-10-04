namespace Cilica.Fixtures.Execution
{
    public static class IntegerWide
    {
        public static long Add(long a, long b) => unchecked(a + b);
        public static long Subtract(long a, long b) => unchecked(a - b);
        public static long Multiply(long a, long b) => unchecked(a * b);
        public static long And(long a, long b) => a & b;
        public static long Or(long a, long b) => a | b;
        public static long Xor(long a, long b) => a ^ b;
        public static long Left(long a, long b) => a << unchecked((int)b);
        public static long Right(long a, long b) => a >> unchecked((int)b);
        public static ulong UnsignedRight(ulong a, long b) => a >> unchecked((int)b);
        public static long Divide(long a, long b) => a / b;
        public static ulong UnsignedDivide(ulong a, ulong b) => a / b;
        public static long AddChecked(long a, long b) => checked(a + b);
        public static long SubtractChecked(long a, long b) => checked(a - b);
        public static long MultiplyChecked(long a, long b) => checked(a * b);
        public static ulong AddUnsignedChecked(ulong a, ulong b) => checked(a + b);
        public static ulong SubtractUnsignedChecked(ulong a, ulong b) => checked(a - b);
        public static ulong MultiplyUnsignedChecked(ulong a, ulong b) => checked(a * b);
        public static long Remainder(long a, long b) => a % b;
        public static ulong UnsignedRemainder(ulong a, ulong b) => a % b;
        public static long Widen(int a) => a;
        public static ulong WidenUnsigned(uint a) => a;
        public static int Narrow(long a) => unchecked((int)a);
        public static long Complement(long a) => ~a;
        public static long Negate(long a) => unchecked(-a);
        public static long Choose(long a, long b) => a < b ? a : b;
        public static int Compare(long a, long b) => a == b ? 0 : a < b ? -1 : 1;
        public static int CompareUnsigned(ulong a, ulong b) => a == b ? 0 : a < b ? -1 : 1;
        public static long Loop(long a, long b)
        {
            long result = a;
            int count = unchecked((int)b) & 7;
            for (int i = 0; i < count; ++i) result = Add(result, b);
            return result;
        }
        public static long Recursive(long a, long b)
        {
            return Recurse(a, unchecked((int)b) & 7);
        }
        private static long Recurse(long value, int count) => count == 0 ? value : Recurse(Add(value, 1L), count - 1);
        public static long FaultCall(long a, long b) => AddChecked(a, b);
        public static long Constant(long a) => a ^ long.MinValue ^ long.MaxValue ^ 0x100000001L;
    }
}
