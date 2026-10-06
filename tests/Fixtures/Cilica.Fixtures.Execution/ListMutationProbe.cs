using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
public static class ListMutationProbe
{
    public static int Insert(int input, int mode)
    {
        var list = new List<int>(2); list.Add(input); list.Add(mode);
        list.Insert(0, 17); list.Insert(2, input); list.Insert(list.Count, mode);
        list.Insert(3, 19); int sum = list.Capacity;
        for (int i = 0; i < list.Count; ++i) sum = unchecked(sum * 31 + list[i]);
        return sum;
    }
    public static int Remove(int input, int mode)
    {
        var list = new List<int>(8); for (int i = 0; i < 7; ++i) list.Add(unchecked(input + i * mode));
        list.RemoveAt(0); list.RemoveAt(2); list.RemoveAt(list.Count - 1);
        var values = list.ToArray(); int sum = list.Count + list.Capacity;
        for (int i = 0; i < values.Length; ++i) sum = unchecked(sum * 31 + values[i]);
        return sum;
    }
    public static int Clear(int input, int mode)
    {
        var list = new List<int>(8); list.Add(input); list.Add(mode); var view = list.AsReadOnly();
        list.Clear(); int result = list.Capacity + view.Count; list.Clear();
        list.Insert(0, mode); return unchecked(result + view[0] + list.Count);
    }
    public static int References(int input, int mode)
    {
        var list = new List<ListLibraryBox>(1); var box = new ListLibraryBox { Value = input };
        list.Insert(0, box); list.Insert(0, null); list.Insert(1, box);
        list.RemoveAt(0); var snapshot = list.ToArray(); list.Clear();
        box.Value = mode; var pressure = new ListLibraryBox { Value = input };
        return unchecked(snapshot[0].Value + snapshot[1].Value + pressure.Value + (snapshot[0] == box && snapshot[1] == box ? 23 : 29));
    }
    public static int Values(int input, int mode)
    {
        var list = new List<ValueVectorCell>(1);
        ValueVectorCell first = default; first.Number = input; first.Wide = ((long)input << 32) + mode;
        first.Reference = new ValueVectorBox(mode); list.Insert(0, first);
        first.Number = mode; list.Insert(0, first); list.Insert(1, default); list.RemoveAt(1);
        var snapshot = list.ToArray(); list.Clear(); first.Reference.Number = input;
        snapshot[0].Number = 11;
        return unchecked(snapshot[0].Number + snapshot[1].Number + snapshot[1].Reference.Number + (int)(snapshot[1].Wide >> 32));
    }
    public static int EmptySnapshots(int input, int mode)
    {
        var first = new List<int>(); var second = new List<int>(4);
        var a = first.ToArray(); var b = second.ToArray();
        first.Insert(0, input); first.RemoveAt(0);
        int result = (a == b && a == first.ToArray() ? 1 : 2) + (a == Array.Empty<int>() ? 4 : 8);
        var objects = new List<object>(); var strings = new List<string>();
        object[] objectEmpty = objects.ToArray(); object[] stringEmpty = strings.ToArray();
        result += objectEmpty == stringEmpty ? 16 : 32;
        result += objectEmpty == Array.Empty<object>() ? 64 : 128;
        result += stringEmpty == Array.Empty<string>() ? 256 : 512;
        return unchecked(result + a.Length + input + mode);
    }
    public static int Snapshots(int input, int mode)
    {
        var list = new List<int>(); list.Add(input); list.Add(mode);
        var first = list.ToArray(); var second = list.ToArray();
        first[0] = 7; list[1] = 11; list.Insert(0, 13);
        return unchecked(first[0] + first[1] + second[0] + second[1] + list[0] + (first == second ? 17 : 19));
    }
    public static int Interfaces(int input, int mode)
    {
        var list = new List<int>(4); IList<int> indexed = list; ICollection<int> collection = list;
        indexed.Insert(0, input); indexed.Insert(1, mode); indexed.RemoveAt(0);
        var view = list.AsReadOnly(); int result = view[0];
        ((IList)list).RemoveAt(0); ((IList)list).Clear();
        collection.Add(input); collection.Clear(); indexed.Insert(0, mode);
        return unchecked(result + view[0] + view.Count + list.Capacity);
    }
    public static int Faults(int input, int mode)
    {
        var list = new List<int>(4); list.Add(input); int result = 0;
        try { list.Insert(mode, 19); } catch (ArgumentOutOfRangeException e) { result += e.ParamName.Length + e.Message.Length; }
        try { list.RemoveAt(input); } catch (ArgumentOutOfRangeException e) { result += e.ParamName.Length + e.Message.Length; }
        var values = list.ToArray();
        for (int i = 0; i < values.Length; ++i) result = unchecked(result * 31 + values[i]);
        return unchecked(result + list.Count + list.Capacity);
    }
    public static int NullReceiver(int input, int mode)
    {
        List<int> list = null;
        try { list.Insert(input, input / mode); return 0; }
        catch (DivideByZeroException) { return 23; }
        catch (OverflowException) { return 29; }
        catch (NullReferenceException) { return 31; }
    }
    public static int ReadOnly(int input, int mode)
    {
        var list = new List<int>(); list.Add(input); var view = list.AsReadOnly();
        int result = 0;
        try { ((IList<int>)view).Insert(mode, input); } catch (NotSupportedException e) { result += e.Message.Length; }
        try { ((IList<int>)view).RemoveAt(mode); } catch (NotSupportedException e) { result += e.Message.Length; }
        try { ((ICollection<int>)view).Clear(); } catch (NotSupportedException e) { result += e.Message.Length; }
        list.RemoveAt(0); list.Insert(0, mode); return unchecked(result + view[0]);
    }
    public static int Stress(int input, int mode)
    {
        var list = new List<ListLibraryBox>(); int sum = 0;
        for (int turn = 0; turn < 5; ++turn)
        {
            Fill(list, input, mode);
            for (int i = 0; i < 10; ++i) list.RemoveAt((int)((uint)mode % (uint)list.Count));
            sum = unchecked(sum + list[0].Value + list.Count + list.Capacity);
            list.Clear();
        }
        return sum;
    }
    public static void Fill(List<ListLibraryBox> list, int input, int mode)
    {
        for (int i = 0; i < 18; ++i) list.Insert(i / 2, new ListLibraryBox { Value = unchecked(input + mode * i) });
    }
    public static List<ListLibraryBox> CreateTracked()
    {
        var list = new List<ListLibraryBox>(3);
        list.Add(new ListLibraryBox { Value = 1 }); list.Add(new ListLibraryBox { Value = 2 });
        return list;
    }
    public static void RemoveTracked(List<ListLibraryBox> list) => list.RemoveAt(1);
    public static void ClearTracked(List<ListLibraryBox> list) => list.Clear();
    public static List<ValueVectorCell> CreateTrackedValues()
    {
        var list = new List<ValueVectorCell>(3);
        ValueVectorCell cell = default; cell.Reference = new ValueVectorBox(1); list.Add(cell);
        cell.Reference = new ValueVectorBox(2); list.Add(cell);
        return list;
    }
    public static void RemoveTrackedValues(List<ValueVectorCell> list) => list.RemoveAt(1);
    public static void ClearTrackedValues(List<ValueVectorCell> list) => list.Clear();
    public static int TrackedValues(int input, int mode)
    {
        var list = CreateTrackedValues(); RemoveTrackedValues(list); ClearTrackedValues(list); return list.Count;
    }
    public static int Tracked(int input, int mode)
    {
        var list = CreateTracked(); RemoveTracked(list); ClearTracked(list); return list.Count;
    }
}
}
