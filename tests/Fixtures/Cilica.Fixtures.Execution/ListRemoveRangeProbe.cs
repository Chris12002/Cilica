using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class RemoveRangeBox { public int Value; public RemoveRangeBox(int value) { Value = value; } }
    public struct RemoveRangeValue { public int Number; public RemoveRangeBox Box; }
    public static class ListRemoveRangeProbe
    {
        private static int Hash(string text)
        { if (text == null) return -7; int h = 17; for (int i = 0; i < text.Length; ++i) h = unchecked(h * 31 + text[i]); return h; }
        private static int Remove<T>(List<T> list, int index, int count)
        {
            try { list.RemoveRange(index, count); return 1; }
            catch (ArgumentOutOfRangeException e) { return unchecked(Hash(e.Message) * 31 + Hash(e.ParamName)); }
            catch (ArgumentException e) { return unchecked(Hash(e.Message) * 17 + Hash(e.ParamName)); }
        }
        private static int Numbers(List<int> list, int index, int count)
        {
            var cursor = list.GetEnumerator(); int h = Remove(list, index, count);
            try { if (cursor.MoveNext()) h = unchecked(h * 31 + cursor.Current); }
            catch (InvalidOperationException e) { h = unchecked(h * 31 + Hash(e.Message)); }
            h = unchecked(h * 31 + list.Count + list.Capacity);
            for (int i = 0; i < list.Count; ++i) h = unchecked(h * 31 + list[i]);
            list.Add(13); return unchecked(h * 31 + list[list.Count - 1] + list.Count);
        }
        public static int Integers(int a, int b) => Numbers(new List<int>(8) { -3, -2, -1, 0, 1, 2 }, a, b);
        public static int Empty(int a, int b) => Numbers(new List<int>(3), a, b);
        public static int References(int a, int b)
        {
            var list = new List<RemoveRangeBox>(8);
            for (int i = 0; i < 6; ++i) list.Add(i == 2 ? null : new RemoveRangeBox(i));
            var first = list[0]; int h = Remove(list, a, b);
            for (int i = 0; i < list.Count; ++i) h = unchecked(h * 31 + (list[i] == null ? -1 : list[i].Value) + (list[i] == first ? 19 : 0));
            return unchecked(h * 31 + list.Count + list.Capacity);
        }
        public static int Values(int a, int b)
        {
            var list = new List<RemoveRangeValue>(8);
            for (int i = 0; i < 6; ++i) list.Add(new RemoveRangeValue { Number = i, Box = new RemoveRangeBox(10 + i) });
            int h = Remove(list, a, b);
            for (int i = 0; i < list.Count; ++i) h = unchecked(h * 31 + list[i].Number + list[i].Box.Value);
            return unchecked(h * 31 + list.Count + list.Capacity);
        }
        public static int Strings(int a, int b)
        {
            var list = new List<string>(8) { null, "", "a", "b\0", "\ud800", "last" };
            int h = Remove(list, a, b);
            for (int i = 0; i < list.Count; ++i) h = unchecked(h * 31 + Hash(list[i]));
            return unchecked(h * 31 + list.Count + list.Capacity);
        }
        public static int Null(int a, int b)
        { List<int> list = null; try { list.RemoveRange(a, b); return -1; } catch (NullReferenceException) { return 29; } }
        public static List<RemoveRangeBox> CreateTracked() => new List<RemoveRangeBox>(3) { new RemoveRangeBox(1), new RemoveRangeBox(2) };
        public static void RemoveTracked(List<RemoveRangeBox> list) => list.RemoveRange(1, 1);
        public static void ClearTracked(List<RemoveRangeBox> list) => list.RemoveRange(0, list.Count);
        public static List<RemoveRangeValue> CreateTrackedValues() => new List<RemoveRangeValue>(3) { new RemoveRangeValue { Number = 1, Box = new RemoveRangeBox(1) }, new RemoveRangeValue { Number = 2, Box = new RemoveRangeBox(2) } };
        public static void RemoveTrackedValues(List<RemoveRangeValue> list) => list.RemoveRange(1, 1);
        public static void ClearTrackedValues(List<RemoveRangeValue> list) => list.RemoveRange(0, list.Count);
        public static int Tracked(int a, int b) { var list = CreateTracked(); RemoveTracked(list); ClearTracked(list); return list.Count; }
        public static int TrackedValues(int a, int b) { var list = CreateTrackedValues(); RemoveTrackedValues(list); ClearTrackedValues(list); return list.Count; }
    }
}
