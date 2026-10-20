using System;
namespace Cilica.Fixtures.Execution
{
public sealed class ScalarLocalsBox
{
    public int Value;
    public ScalarLocalsBox(int value) { Value = value; }
}
public static class ScalarLocalsProbe
{
    // Many IL temporaries and loop-carried scalars, like the diff kernels.
    public static int Chain(int[] a, int[] b, int seed)
    {
        int x = 0, y = 0, matches = 0, edits = seed & 7;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y]) { ++matches; ++x; ++y; continue; }
            int left = a[x] - b[y], right = unchecked(left * 31 + x - y);
            if ((right & 1) == 0) ++x; else ++y;
            edits = unchecked(edits + (left ^ right) + (x << 2) - (y >> 1));
        }
        return unchecked(matches * 1000 + edits);
    }
    public static long Wide(int a, int b)
    {
        long acc = ((long)a << 32) | (uint)b, mix = 0;
        for (int i = 0; i < 9; ++i)
        {
            long shifted = acc >> (i + 1), rotated = unchecked((acc << 7) ^ shifted);
            mix = unchecked(mix * 1099511628211L + rotated);
            acc = unchecked(acc + mix - i);
        }
        return unchecked(acc ^ mix);
    }
    // Address-taken locals stay framed while neighbouring scalars become native.
    public static int Bump(ref int target, int amount) { target = unchecked(target + amount); return unchecked(target * 3); }
    public static void Split(int value, out int high, out long low) { high = value >> 16; low = (long)(value & 0xffff) << 20; }
    public static int Neighbours(int a, int b)
    {
        int before = unchecked(a * 7), framed = b, after = unchecked(a - b);
        int bumped = Bump(ref framed, before);
        Split(unchecked(after + framed), out int high, out long low);
        int unused = unchecked(before * after); _ = unused;
        return unchecked(bumped + framed + high + (int)(low >> 20) + before - after);
    }
    public static int Recurse(int depth, int value)
    {
        if (depth <= 0) return value;
        int local = unchecked(value * 5 + depth), next = Recurse(depth - 1, local ^ depth);
        return unchecked(next + local - depth);
    }
    // Scalars stay live across allocations and collection.
    public static int Allocating(int a, int b)
    {
        int total = 0, mark = unchecked(a ^ b);
        for (int i = 0; i < 6; ++i)
        {
            var box = new ScalarLocalsBox(unchecked(mark + i));
            int[] values = { box.Value, i, mark };
            total = unchecked(total + values[0] * values[1] - values[2]);
            mark = unchecked(mark * 17 + box.Value);
        }
        return unchecked(total + mark);
    }
    public static int Faulting(int[] values, int index, ref int seen)
    {
        int doubled = unchecked(index * 2), adjusted = unchecked(doubled + seen);
        seen = adjusted;
        return unchecked(values[index] + adjusted);
    }
    public static int Run(int a, int b)
    {
        int[] first = { a & 15, b & 15, 3, a & 7, 9, b & 3 }, second = { b & 15, a & 15, 3, 9, a & 3, 1 };
        int result = Chain(first, second, a);
        result = unchecked(result + (int)Wide(a, b) + (int)(Wide(b, a) >> 32));
        result = unchecked(result + Neighbours(a, b) + Recurse((a & 15) + 8, b) + Allocating(a, b));
        int seen = b;
        try { result = unchecked(result + Faulting((a & 1) == 0 ? null : first, (b & 7) + 1, ref seen)); }
        catch (NullReferenceException) { result = unchecked(result + 37); }
        catch (IndexOutOfRangeException) { result = unchecked(result + 41); }
        return unchecked(result + seen);
    }
}
}
