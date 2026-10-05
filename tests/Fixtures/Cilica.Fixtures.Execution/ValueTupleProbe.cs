using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
public struct MutatingTupleKey : IEquatable<MutatingTupleKey>
{
    public int Value;
    public bool Equals(MutatingTupleKey other) { Value++; return Value == other.Value + 1; }
    public override bool Equals(object other) { return other is MutatingTupleKey key && Equals(key); }
    public override int GetHashCode() { Value++; return Value; }
}
public static class ValueTupleProbe
{
    public static int Values(int input, int mode)
    {
        string text = mode < 0 ? null : "a\0\ud800";
        var first = new ValueTuple<int, string>(input, text);
        var second = first; second.Item1 = mode;
        string pressure = string.Concat("root", "pair");
        return (first.Equals(second) ? 1 : 0) + (first.Equals(first) ? 2 : 0)
            + (first.Equals(default(ValueTuple<int, string>)) ? 4 : 0)
            + (first.Item2 == null ? 16 : first.Item2.Length) + pressure.Length;
    }
    public static int Dictionary(int input, int mode)
    {
        var values = new Dictionary<ValueTuple<int, string>, int>();
        string first = string.Concat("scope", "\0\ud800");
        string equal = string.Concat("scope", "\0\ud800");
        values.TryAdd(new ValueTuple<int, string>(input, first), 17);
        values.TryAdd(new ValueTuple<int, string>(input, null), 23);
        for (int i = 0; i < 21; i++) values[new ValueTuple<int, string>(i, "other")] = i + 5;
        bool added = values.TryAdd(new ValueTuple<int, string>(input, equal), 99);
        int read; bool found = values.TryGetValue(new ValueTuple<int, string>(input, equal), out read);
        int nullValue; values.TryGetValue(new ValueTuple<int, string>(input, null), out nullValue);
        return values.Count + read + nullValue
            + (added ? 100 : 0) + (found ? 200 : 0);
    }
    public static int CopyAndHash(int input, int mode)
    {
        var key = new MutatingTupleKey(); key.Value = input;
        string text = mode < 0 ? null : "value";
        var pair = new ValueTuple<MutatingTupleKey, string>(key, text);
        var copy = pair;
        bool same = pair.Equals(copy);
        int first = pair.GetHashCode(), second = copy.GetHashCode();
        return pair.Item1.Value + copy.Item1.Value + (same ? 1 : 0) + (first == second ? 2 : 0);
    }
    public static int Strings(int input, int mode)
    {
        var values = new Dictionary<string, int>();
        values.TryAdd("", 2); values.TryAdd("a", 3); values.TryAdd("ab", 5); values.TryAdd("abc", 7);
        values.TryAdd("abcd", 11); values.TryAdd("abcde", 13); values.TryAdd("\0\ud800\udfffz\0", 17);
        string equal = string.Concat("ab", "cde");
        int read; values.TryGetValue(equal, out read);
        try { values.TryAdd(null, 1); } catch (ArgumentNullException) { read += 19; }
        int special; values.TryGetValue("\0\ud800\udfffz\0", out special);
        return read + special + values.Count;
    }
    public static int UnsupportedObjectEquals(int input, int mode) { return new ValueTuple<int,int>(input,mode).Equals((object)null) ? 1 : 0; }
    public static int UnsupportedText(int input, int mode) { return new ValueTuple<int,int>(input,mode).ToString().Length; }
    public static int UnsupportedCompare(int input, int mode) { return new ValueTuple<int,int>(input,mode).CompareTo(new ValueTuple<int,int>(mode,input)); }
}
}
