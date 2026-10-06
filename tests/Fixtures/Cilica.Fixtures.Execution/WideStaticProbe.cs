using System;
namespace Cilica.Fixtures.Execution
{
    public static class WideStaticSlot<T> { public static T Value; }
    public static class WideStaticInitialized { public static readonly long Value = long.MinValue + 0x12345678L; }
    public static class WideStaticProbe
    {
        private static long signed;
        private static ulong unsigned;
        private static bool started;
        public static int Signed(int a, int b)
        {
            signed = unchecked(((long)a << 32) | (uint)b);
            long copy = signed; signed = unchecked(signed + long.MaxValue);
            return unchecked((int)(copy >> 32) + (int)copy + (int)(signed >> 32) + (int)signed);
        }
        public static int Unsigned(int a, int b)
        {
            unsigned = unchecked(((ulong)(uint)a << 32) | (uint)b);
            ulong copy = unsigned; unsigned = unchecked(unsigned + ulong.MaxValue);
            return unchecked((int)(copy >> 32) + (int)copy + (int)(unsigned >> 32) + (int)unsigned);
        }
        public static int Closed(int a, int b)
        {
            WideStaticSlot<long>.Value = unchecked(((long)a << 32) | (uint)b);
            WideStaticSlot<ulong>.Value = unchecked(((ulong)(uint)b << 32) | (uint)a);
            return unchecked((int)(WideStaticSlot<long>.Value >> 32) + (int)WideStaticSlot<long>.Value
                + (int)(WideStaticSlot<ulong>.Value >> 32) + (int)WideStaticSlot<ulong>.Value);
        }
        public static int Faults(int a, int b)
        {
            signed = unchecked(((long)a << 32) | (uint)b);
            long copy = signed;
            try { signed = checked(signed + long.MaxValue); }
            catch (OverflowException) { return signed == copy ? 31 : -31; }
            return unchecked((int)(signed >> 32) + (int)signed);
        }
        public static int Initialized(int a, int b) => unchecked((int)(WideStaticInitialized.Value >> 32) + (int)WideStaticInitialized.Value + a + b);
        public static int Persistent(int a, int b)
        {
            if (!started) { signed = (long)a << 32; started = true; }
            signed = unchecked(signed + ((long)b << 32));
            return (int)(signed >> 32);
        }
    }
}
