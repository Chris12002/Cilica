using System;
namespace Cilica.Fixtures.Execution
{
public interface ILocalOutput { int Read(int key, out int value); }
public sealed class FirstLocalOutput : ILocalOutput
{
    public int Read(int key, out int value) { value = unchecked(key + 7); return unchecked(value * 3); }
}
public sealed class SecondLocalOutput : ILocalOutput
{
    public int Read(int key, out int value) { value = unchecked(key - 11); return unchecked(value * 5); }
}
public static class LocalOutputProbe
{
    public static int Write(int key, ref int value) { value = unchecked(value + key); return unchecked(value * 7); }
    public static long Wide(int key, ref long value) { value = unchecked(value + ((long)key << 32)); return value; }
    public static int ArrayFault(int[] values, int index, out int value) { value = 41; return values[index]; }
    public static int Pair(ref int a, ref int b) { a = unchecked(a + 1); b = unchecked(b + a); return b; }
    public static int Run(int a, int b)
    {
        int value = b, result = 0;
        try
        {
            result = Write(a, ref value);
            ILocalOutput target = (a & 3) == 0 ? null : (a & 1) == 0 ? (ILocalOutput)new FirstLocalOutput() : new SecondLocalOutput();
            result = unchecked(result + target.Read(b, out value));
        }
        catch (NullReferenceException) { result = unchecked(result + 19); }
        long wide = ((long)b << 32) | (uint)a;
        result = unchecked(result + (int)(Wide(a, ref wide) >> 32));
        int[] array = { a, b };
        try { result = unchecked(result + ArrayFault((a & 1) == 0 ? null : array, b, out value)); }
        catch (NullReferenceException) { result = unchecked(result + 23); }
        catch (IndexOutOfRangeException) { result = unchecked(result + 29); }
        result = unchecked(result + value + (int)wide);
        return unchecked(result + Pair(ref value, ref value));
    }
}
}
