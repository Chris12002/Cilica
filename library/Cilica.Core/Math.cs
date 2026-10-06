// Cilica-owned bounded integer math. No platform-specific arithmetic or intrinsic.
namespace System
{
    public static class Math
    {
        public static int Abs(int value)
        {
            if (value == -2147483648) throw new OverflowException("Negating the minimum value of a twos complement number is invalid.");
            return value < 0 ? -value : value;
        }
        public static long Abs(long value)
        {
            if (value == -9223372036854775808L) throw new OverflowException("Negating the minimum value of a twos complement number is invalid.");
            return value < 0 ? -value : value;
        }
        public static int Min(int left, int right) => left < right ? left : right;
        public static int Max(int left, int right) => left > right ? left : right;
        public static uint Min(uint left, uint right) => left < right ? left : right;
        public static uint Max(uint left, uint right) => left > right ? left : right;
        public static long Min(long left, long right) => left < right ? left : right;
        public static long Max(long left, long right) => left > right ? left : right;
        public static ulong Min(ulong left, ulong right) => left < right ? left : right;
        public static ulong Max(ulong left, ulong right) => left > right ? left : right;
    }
}
