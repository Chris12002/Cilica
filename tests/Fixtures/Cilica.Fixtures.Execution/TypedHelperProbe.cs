using System;
namespace Cilica.Fixtures.Execution
{
public static class TypedHelperProbe
{
    public static int Rotate(int value, int shift) => unchecked((int)(((uint)value << shift) | ((uint)value >> (32 - shift))));
    public static long Wide(long value, int shift) => unchecked((value >> shift) ^ (value << shift));
    public static int Sum(int value, int count)
    {
        int sum = 0;
        for (int i = 0; i < (count & 15); ++i) sum = unchecked(sum + value + i);
        return sum;
    }
    public static int Divide(int left, int right) => left / right;
    public static int Checked(int left, int right) => checked(left * right + left);
    public static int Deep(int count, int value) => count == 0 ? Rotate(value, count) : unchecked(Rotate(value, count) + Deep(count - 1, value));
    public static int Run(int a, int b) => unchecked(Rotate(a, b) + (int)Wide(((long)a << 32) | (uint)b, b) + Sum(a, b) + Deep(a & 7, b));
    public static int Fault(int a, int b)
    {
        try { return Checked(Divide(a, b), a); }
        catch (DivideByZeroException) { return 71; }
        catch (OverflowException) { return 93; }
    }
}
}
