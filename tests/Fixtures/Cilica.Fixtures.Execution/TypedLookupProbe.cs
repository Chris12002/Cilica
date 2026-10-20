#nullable enable
using System;
namespace Cilica.Fixtures.Execution
{
public sealed class TypedLookupCell
{
    public int Key;
    public int Value;
    public TypedLookupCell? Next;
}
public interface ITypedLookup { int Read(TypedLookupCell cell, int key); }
public sealed class PureTypedLookup : ITypedLookup
{
    public int Read(TypedLookupCell cell, int key) => unchecked(cell.Value + key);
}
public sealed class EffectfulTypedLookup : ITypedLookup
{
    public int Read(TypedLookupCell cell, int key)
    {
        var other = new TypedLookupCell { Value = unchecked(cell.Value + key) };
        cell.Value = unchecked(other.Value + 1);
        return cell.Value;
    }
}
public static class TypedLookupProbe
{
    public static TypedLookupCell? Find(TypedLookupCell? cell, int key)
    {
        while (cell != null) { if (cell.Key == key) return cell; cell = cell.Next; }
        return null;
    }
    public static bool TryFind(TypedLookupCell? cell, int key, out int result)
    {
        var found = Find(cell, key);
        if (found == null) { result = 0; return false; }
        result = found.Value; return true;
    }
    public static int Apply(ITypedLookup lookup, TypedLookupCell cell, int key) => lookup.Read(cell, key);
    public static int WriteAlias(ref int left, ref int right) { left = unchecked(left + 7); return right; }
    public static int HeapAlias(TypedLookupCell cell, ref int value) { value = 37; return cell.Value; }
    public static int WriteThenFault(ref int value, int divisor) { value = unchecked(value + 1); return 120 / divisor; }
    public static void Require(TypedLookupCell? cell) { if (cell == null) throw new ArgumentNullException("cell"); }
    public static int ArrayRead(TypedLookupCell[] cells, int index) => cells[index].Value + cells.Length;
    public static int Run(int a, int b)
    {
        var tail = new TypedLookupCell { Key = 1, Value = b };
        var head = new TypedLookupCell { Key = 0, Value = a, Next = tail };
        int result;
        int score = TryFind(head, a & 3, out result) ? 19 : 31;
        score = unchecked(score + result);
        ITypedLookup lookup = (b & 1) == 0 ? (ITypedLookup)new PureTypedLookup() : new EffectfulTypedLookup();
        score = unchecked(score + Apply(lookup, head, b));
        var found = Find(head, 1);
        var allocated = new TypedLookupCell { Value = 3 };
        score = unchecked(score + found!.Value + allocated.Value);
        score = unchecked(score + WriteAlias(ref result, ref result) + result);
        score = unchecked(score + HeapAlias(head, ref head.Value));
        TryFind(head, 1, out head.Value); // Heap output deliberately uses the framed path.
        Require(head);
        return unchecked(score + head.Value + ArrayRead(new[] { head, tail }, a & 1));
    }
    public static int Fault(int a, int b)
    {
        int value = a; int score = 0;
        try { score = WriteThenFault(ref value, b & 1); }
        catch (DivideByZeroException) { score = 41; }
        try { Require(null); }
        catch (ArgumentNullException) { score += 13; }
        try { score += ArrayRead(new TypedLookupCell[1], b & 3); }
        catch (NullReferenceException) { score += 17; }
        catch (IndexOutOfRangeException) { score += 23; }
        return unchecked(score + value);
    }
}
}
