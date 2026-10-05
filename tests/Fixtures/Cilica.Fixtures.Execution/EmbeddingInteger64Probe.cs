namespace Cilica.Fixtures.Execution
{
    public sealed class EmbeddingWideBox { public long Value; }
    public static class EmbeddingInteger64Probe
    {
        public static long Signed(long value) => value;
        public static ulong Unsigned(ulong value) => value;
        public static long Add(long left, long right) => unchecked(left + right);
        public static long CheckedAdd(long left, long right) => checked(left + right);
        public static ulong CheckedUnsignedAdd(ulong left, ulong right) => checked(left + right);
        public static long Divide(long left, long right) => left / right;
        public static ulong UnsignedDivide(ulong left, ulong right) => left / right;
        public static long Shift(long value, int count) => value >> count;
        public static EmbeddingWideBox Make(long value) => new EmbeddingWideBox { Value = value };
        public static long Read(EmbeddingWideBox box) => box.Value;
        public static void Set(EmbeddingWideBox box, long value) => box.Value = value;
        public static long Constant() => long.MinValue;
        public static int Root(int mode, int input)
        {
            long a = Signed(input), b = unchecked((long)Unsigned((ulong)input));
            var box = Make(Constant()); Set(box, a);
            if (mode == 0) return unchecked((int)Add(Read(box), b));
            if (mode == 1) return unchecked((int)CheckedAdd(a, b));
            if (mode == 2) return unchecked((int)CheckedUnsignedAdd((ulong)a, (ulong)b));
            if (mode == 3) return unchecked((int)Divide(a, b));
            if (mode == 4) return unchecked((int)UnsignedDivide((ulong)a, (ulong)b));
            return unchecked((int)Shift(a, input));
        }
    }
}
