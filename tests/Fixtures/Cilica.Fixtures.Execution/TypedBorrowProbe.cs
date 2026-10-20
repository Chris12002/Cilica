#nullable enable
using System;
namespace Cilica.Fixtures.Execution
{
public sealed class TypedTextCell { public string? Text = ""; }
public static class TypedBorrowProbe
{
    public static int Mix(string text, int seed)
    {
        uint hash = (uint)seed;
        for (int i = 0; i < text.Length; ++i) hash = (hash << 5) ^ (hash >> 27) ^ text[i];
        return unchecked((int)hash);
    }
    public static int Snapshot(ref string? text, int seed)
    {
        if (text == null) return seed;
        return unchecked(Mix(text, seed) + text.Length + text[seed & 3]);
    }
    public static int Pair(ref int a, ref int b) => unchecked(a + b);
    public static long ReadWide(ref long a) => unchecked(a + (a >> 32));
    public static int Mutate(ref int a, ref int b) { a = unchecked(a + 1); return b; }
    public static void Change(ref string? text) { text = "\uffff\0\udfffZ"; }
    public static int Run(int a, int b)
    {
        string? text = (a & 1) == 0 ? "A\0\ud800Z" : null;
        int first = Snapshot(ref text, a);
        Change(ref text);
        int second = Snapshot(ref text, b);
        var cell = new TypedTextCell { Text = text! };
        string?[] array = { "ABCD", text };
        long wide = ((long)a << 32) | (uint)b;
        int alias = Pair(ref a, ref a) + Mutate(ref b, ref b);
        return unchecked(first + second + Snapshot(ref cell.Text, a) + Snapshot(ref array[a & 1], b) + alias + (int)ReadWide(ref wide));
    }
    public static int At(string text, int index) => text[index];
    public static int Fault(int a, int b)
    {
        try { return At((a & 1) == 0 ? null! : "A\0\ud800Z", b); }
        catch (NullReferenceException) { return 17; }
        catch (IndexOutOfRangeException) { return 31; }
    }
}
}
