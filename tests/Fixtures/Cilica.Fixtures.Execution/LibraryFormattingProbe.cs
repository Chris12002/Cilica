using Cilica.Library;

namespace Cilica.Fixtures.Execution
{
    public static class LibraryFormattingProbe
    {
        public static ulong UnsignedCase(int index)
        {
            if (index == 0) return 0;
            if (index == 1) return 1;
            if (index == 2) return 9;
            if (index == 3) return 10;
            if (index == 4) return 99;
            if (index == 5) return 100;
            if (index == 6) return 101;
            if (index == 7) return 999;
            if (index == 8) return 1000;
            if (index == 9) return 1000000000;
            if (index == 10) return uint.MaxValue;
            if (index == 11) return 4294967296;
            if (index == 12) return long.MaxValue;
            if (index == 13) return 9223372036854775808;
            if (index == 14) return 10000000000000000000;
            if (index == 15) return 18446744073709551614;
            return ulong.MaxValue;
        }
        public static long SignedCase(int index)
        {
            if (index == 0) return long.MinValue;
            if (index == 1) return long.MinValue + 1;
            if (index == 2) return -1000000000000000000;
            if (index == 3) return int.MinValue;
            if (index == 4) return -1001;
            if (index == 5) return -100;
            if (index == 6) return -99;
            if (index == 7) return -10;
            if (index == 8) return -1;
            if (index == 9) return 0;
            if (index == 10) return 1;
            if (index == 11) return 10;
            if (index == 12) return 99;
            if (index == 13) return 100;
            if (index == 14) return int.MaxValue;
            if (index == 15) return long.MaxValue - 1;
            return long.MaxValue;
        }
        private static int Observe(string text, int position)
        {
            if (position < 0) return text.Length;
            if (position >= text.Length) return -1;
            return text[position];
        }
        public static int Signed64(int index, int position) => Observe(IntegerFormatting.Int64Invariant(SignedCase(index)), position);
        public static int Unsigned64(int index, int position) => Observe(IntegerFormatting.UInt64Invariant(UnsignedCase(index)), position);
        public static int Signed32(int index, int position) => Observe(IntegerFormatting.Int32Invariant(unchecked((int)SignedCase(index))), position);
        public static int Unsigned32(int index, int position) => Observe(IntegerFormatting.UInt32Invariant(unchecked((uint)UnsignedCase(index))), position);
    }
}
