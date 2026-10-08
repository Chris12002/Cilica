using System;
namespace Cilica.Fixtures.Execution
{
    public static class IntegerSignProbe
    {
        public static int Signed32(int a, int b) => Math.Sign(a) * 17 + Math.Sign(b);
        public static int Signed64(int a, int b)
        { long value = ((long)a << 32) | (uint)b; return Math.Sign(value) * 17 + Math.Sign(unchecked(-value)); }
        public static int Directions(int a, int b)
        {
            int x = Math.Sign(unchecked(a - b)), y = -Math.Sign(unchecked(b - a));
            return unchecked(x * 17 + y + Math.Sign((long)a - b) * 97);
        }
        public static int Boundaries(int a, int b)
        {
            int h = Math.Sign(int.MinValue) * 17 + Math.Sign(int.MaxValue);
            h = h * 17 + Math.Sign(long.MinValue); h = h * 17 + Math.Sign(long.MaxValue);
            h = h * 17 + Math.Sign(0); h = h * 17 + Math.Sign(0L);
            return unchecked(h + Math.Sign(a) + Math.Sign((long)b));
        }
    }
}
