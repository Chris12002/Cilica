using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Cilica.Fixtures.Execution
{
public static class ListLibraryProbe
{
    public static int Growth(int input, int mode)
    {
        var list = new List<int>();
        int count = 35 + (input & 7), sum = 0;
        for (int i = 0; i < count; ++i) { list.Add(unchecked(input + i * mode)); sum = unchecked(sum * 17 + list.Capacity); }
        ICollection<int> collection = list;
        collection.Add(mode);
        list[0] = mode;
        IReadOnlyList<int> view = list;
        for (int i = 0; i < view.Count; ++i) sum = unchecked(sum * 31 + view[i]);
        return unchecked(sum + list.Count);
    }
    public static int References(int input, int mode)
    {
        var list = new List<ListLibraryBox>(1);
        var box = new ListLibraryBox(); box.Value = input;
        list.Add(box); list.Add(null);
        for (int i = 0; i < 18; ++i) { var next = new ListLibraryBox(); next.Value = unchecked(i * mode); list.Add(next); }
        var view = list.AsReadOnly();
        box.Value = mode;
        int sum = view[0].Value;
        for (int i = 2; i < view.Count; ++i) sum = unchecked(sum * 31 + view[i].Value);
        return unchecked(sum + (view[1] == null ? 3 : 0) + (view[0] == box ? 7 : 0));
    }
    public static int LiveView(int input, int mode)
    {
        var list = new List<int>(); list.Add(input);
        var view = list.AsReadOnly(); var other = list.AsReadOnly();
        list.Add(mode); list[0] = unchecked(input + mode);
        int bits = view != other ? 1 : 0;
        bits += ((ICollection<int>)view).IsReadOnly ? 2 : 0;
        bits += !((ICollection<int>)list).IsReadOnly ? 4 : 0;
        bits += ((IList)view).IsFixedSize && !((IList)list).IsFixedSize ? 8 : 0;
        bits += ((ICollection)view).SyncRoot == (object)list ? 16 : 0;
        try { ((IList<int>)view)[-1] = 8; } catch (NotSupportedException e) { bits += e.Message.Length; }
        try { ((ICollection<int>)view).Add(input); } catch (NotSupportedException) { bits += 64; }
        return unchecked(bits + view[0] * 19 + view[1] * 23 + view.Count);
    }
    public static int Capacity(int input, int mode)
    {
        var list = new List<int>(input & 7);
        list.Add(input); list.Add(mode); list.Capacity = 12;
        int sum = list.Capacity + list.Count;
        try { list.Capacity = -1; } catch (ArgumentOutOfRangeException e) { sum += e.Message.Length + e.ParamName.Length; }
        list.Capacity = list.Count;
        return unchecked(sum + list[0] * 31 + list[1] * 17 + list.Capacity);
    }
    public static int Faults(int input, int mode)
    {
        int sum = 0;
        try { _ = new List<int>(-1); } catch (ArgumentOutOfRangeException e) { sum += e.Message.Length + e.ParamName.Length; }
        try { _ = new ReadOnlyCollection<int>(null); } catch (ArgumentNullException e) { sum += e.Message.Length + e.ParamName.Length; }
        var list = new List<int>();
        try { _ = list[input]; } catch (ArgumentOutOfRangeException e) { sum += e.Message.Length + e.ParamName.Length; }
        try { list[mode] = input; } catch (ArgumentOutOfRangeException e) { sum += e.Message.Length + e.ParamName.Length; }
        var view = list.AsReadOnly();
        try { _ = view[mode]; } catch (ArgumentOutOfRangeException e) { sum += e.Message.Length + e.ParamName.Length; }
        List<int> missing = null;
        try { missing.Add(input); } catch (NullReferenceException) { sum += 17; }
        return sum;
    }
    public static int ArrayMutations(int input, int mode)
    {
        IList<int> integers = new int[2]; integers[0] = input; integers[1] = mode;
        int result = unchecked(integers[0] * 19 + integers[1]);
        try { integers.Add(input); } catch (NotSupportedException e) { result += e.Message.Length; }
        string[] text = new string[1]; IList<object> objects = text;
        objects[0] = "text";
        result += text[0].Length;
        try { objects[0] = new object(); } catch (ArrayTypeMismatchException) { result += 16; }
        try { objects[-1] = new object(); } catch (ArgumentOutOfRangeException e) { result += e.Message.Length; }
        try { objects.Add(new object()); } catch (NotSupportedException) { result += 32; }
        objects[0] = null;
        result += text[0] == null ? 64 : 0;
        IList<int> missing = null;
        try { missing[0] = input; } catch (NullReferenceException) { result += 128; }
        try { missing.Add(input); } catch (NullReferenceException) { result += 256; }
        return result;
    }
    public static int UnsupportedEnumeration(int input, int mode) => new List<int>().GetEnumerator().MoveNext() ? 1 : 0;
    public static int UnsupportedContains(int input, int mode) => new List<int>().Contains(input) ? 1 : 0;
}
public sealed class ListLibraryBox { public int Value; }
}
