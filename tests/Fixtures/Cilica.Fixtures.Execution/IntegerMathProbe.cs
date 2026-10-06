using System;
namespace Cilica.Fixtures.Execution
{
    public static class IntegerMathProbe
    {
        public static int Abs32(int input, int mode)
        {
            try { return Math.Abs(input); }
            catch (OverflowException e) { return e.Message == "Negating the minimum value of a twos complement number is invalid." ? -91 : -100; }
        }
        public static int Abs64(int input, int mode)
        {
            long value = ((long)input << 32) | (uint)mode;
            try { long result = Math.Abs(value); return (mode & 1) == 0 ? unchecked((int)result) : (int)(result >> 32); }
            catch (OverflowException e) { return e.Message == "Negating the minimum value of a twos complement number is invalid." ? -92 : -100; }
        }
        public static int Signed32(int input, int mode) => unchecked(Math.Min(input, mode) * 31 + Math.Max(input, mode));
        public static int Unsigned32(int input, int mode) => unchecked((int)(Math.Min((uint)input, (uint)mode) * 31 + Math.Max((uint)input, (uint)mode)));
        public static int Signed64(int input, int mode)
        {
            long left = ((long)input << 32) | (uint)mode, right = ((long)mode << 32) | (uint)input;
            long min = Math.Min(left, right), max = Math.Max(left, right);
            return unchecked((int)min * 31 + (int)(min >> 32) * 17 + (int)max * 13 + (int)(max >> 32));
        }
        public static int Unsigned64(int input, int mode)
        {
            ulong left = ((ulong)(uint)input << 32) | (uint)mode, right = ((ulong)(uint)mode << 32) | (uint)input;
            ulong min = Math.Min(left, right), max = Math.Max(left, right);
            return unchecked((int)min * 31 + (int)(min >> 32) * 17 + (int)max * 13 + (int)(max >> 32));
        }
        public static int Compare32(int input, int mode) => input.CompareTo(mode);
        public static int CompareUnsigned32(int input, int mode) => ((uint)input).CompareTo((uint)mode);
        public static int Compare64(int input, int mode)
        {
            long left = ((long)input << 32) | (uint)mode, right = ((long)mode << 32) | (uint)input;
            return left.CompareTo(right);
        }
        public static int CompareUnsigned64(int input, int mode)
        {
            ulong left = ((ulong)(uint)input << 32) | (uint)mode, right = ((ulong)(uint)mode << 32) | (uint)input;
            return left.CompareTo(right);
        }
        public static int Boundaries(int input, int mode)
        {
            int result = 0;
            try { result += Math.Abs(int.MinValue); } catch (OverflowException e) { result += e.Message.Length; }
            try { result += (int)Math.Abs(long.MinValue); } catch (OverflowException e) { result += e.Message.Length; }
            if (Math.Min(long.MinValue, long.MaxValue) != long.MinValue || Math.Max(long.MinValue, long.MaxValue) != long.MaxValue) return -100;
            if (Math.Min(0UL, ulong.MaxValue) != 0 || Math.Max(0UL, ulong.MaxValue) != ulong.MaxValue) return -101;
            if (Math.Abs(-9223372036854775807L) != long.MaxValue || Math.Abs(-2147483647) != int.MaxValue) return -102;
            return result;
        }
    }
}
