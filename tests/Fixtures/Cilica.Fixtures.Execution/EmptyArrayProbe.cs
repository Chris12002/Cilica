using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
public static class EmptyArrayProbe
{
    public static int Identity(int input, int mode)
    {
        var first = Array.Empty<int>();
        var fresh = new int[0];
        var second = Empty<int>();
        return (first == second ? 1 : 0) + (first != fresh ? 2 : 0)
            + first.Length * input + ((IReadOnlyCollection<int>)second).Count * mode;
    }
    private static T[] Empty<T>() => Array.Empty<T>();
    public static int Covariance(int input, int mode)
    {
        object[] strings = Array.Empty<string>();
        var objects = Array.Empty<object>();
        var again = Empty<string>();
        var boxes = Array.Empty<EmptyArrayBox>();
        return (strings == again ? 1 : 0) + (strings != objects ? 2 : 0)
            + ((object)boxes != objects ? 4 : 0) + boxes.Length * input
            + ((IReadOnlyCollection<object>)strings).Count * mode;
    }
    public static int Faults(int input, int mode)
    {
        var first = Array.Empty<int>();
        try { first[input] = mode; return -1; }
        catch (IndexOutOfRangeException) { }
        try { return first[mode]; }
        catch (IndexOutOfRangeException) { }
        try { return ((IReadOnlyList<int>)first)[input]; }
        catch (ArgumentOutOfRangeException) { return first == Array.Empty<int>() ? 7 : -2; }
    }
    public static int UnsupportedUnsigned(int input, int mode) => Array.Empty<uint>().Length;
    public static int UnsupportedWide(int input, int mode) => Array.Empty<long>().Length;
    public static int UnsupportedValue(int input, int mode) => Array.Empty<EmptyArrayValue>().Length;
}
public sealed class EmptyArrayBox { public int Value; }
public struct EmptyArrayValue { public int Value; }
}
