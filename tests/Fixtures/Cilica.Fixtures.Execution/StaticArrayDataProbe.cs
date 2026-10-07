using System;
using System.Runtime.CompilerServices;
namespace Cilica.Fixtures.Execution
{
public static class StaticArrayDataProbe
{
    public static int Integers(int a, int b)
    {
        var values = new[] { int.MinValue, -1, 0x12345678, int.MaxValue, -71235, 87, 94, 104 };
        values[b & 7] = a;
        return values[a & 7] ^ values[(a + 1) & 7];
    }
    public static int Wide(int a, int b)
    {
        var values = new[] { long.MinValue, long.MaxValue, -1L, 0x12345678abcdef01L };
        long value = values[a & 3];
        values[b & 3] = ((long)a << 32) | (uint)b;
        return (int)(value >> 32) ^ (int)value ^ (int)(values[b & 3] >> 32) ^ (int)values[b & 3];
    }
    public static int Bytes(int a, int b)
    {
        var values = new byte[] { 0, 1, 127, 128, 254, 255, 43, 87 };
        values[b & 7] = (byte)a;
        return values[a & 7] * 256 + values[(a + 1) & 7];
    }
    public static int Chars(int a, int b)
    {
        var values = new char[] { '\0', '\u1234', '\ud800', '\udfff', '\uffff', 'A', 'Z', '\u8000' };
        values[b & 7] = (char)a;
        return values[a & 7] ^ values[(a + 1) & 7];
    }
    public static int Fresh(int a, int b)
    {
        var left = new[] { 11, 22, 33, 44 };
        var right = new[] { 11, 22, 33, 44 };
        left[a & 3] = b;
        return left[a & 3] ^ right[a & 3];
    }
    public static int CaughtBounds(int a, int b)
    {
        try { var values = new[] { 17, 29, 31, 47 }; return values[a] + b; }
        catch (IndexOutOfRangeException) { return -83; }
    }
    public static int Multiple(int a, int b)
    {
        var first = a < 0 ? new[] { 1, 3, 5, 7 } : new[] { 2, 4, 6, 8 };
        var second = new[] { 13L, 29L, long.MinValue, long.MaxValue };
        return first[a & 3] ^ (int)(second[b & 3] >> 32);
    }
    public static int Static(int a, int b) => Constants.Values[a & 3] + b;
    private static class Constants { internal static readonly int[] Values = new[] { -1, 3, 711, int.MinValue }; }
    public static int UnsupportedHandle(int a, int b)
    {
        RuntimeHelpers.InitializeArray(new int[4], default(RuntimeFieldHandle)); return a + b;
    }
}
}
