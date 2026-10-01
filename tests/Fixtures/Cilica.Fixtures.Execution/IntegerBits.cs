namespace Cilica.Fixtures.Execution
{
    public static class IntegerBits
    {
        public static int BitwiseAnd(int a, int b, int unused) => a & b;
        public static int BitwiseOr(int a, int b, int unused) => a | b;
        public static int BitwiseXor(int a, int b, int unused) => a ^ b;
        public static int BitwiseNot(int a, int unused, int alsoUnused) => ~a;
        public static int ShiftLeft(int value, int count, int unused) => value << count;
        public static int ShiftRight(int value, int count, int unused) => value >> count;
        public static int ShiftRightUnsigned(int value, int count, int unused) => unchecked((int)((uint)value >> count));
        public static int MixBits(int a, int b, int c) => ((a & b) | (~a & c)) ^ (ShiftRightUnsigned(a, b, c) | ShiftLeft(c, b, a));

        public static int BitLoop(int count, int value, int mask)
        {
            int result = value;
            for (int index = 0; index < count; index++) result = (result ^ mask) << index;
            return result;
        }
    }
}
