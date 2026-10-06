using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public class EnumerationOuter<T>
    {
        public struct Inner<U>
        {
            public T First;
            public U Second;
            public U Pick() => Second;
        }
    }
    public static class ListEnumerationProbe
    {
        public static int Integers(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode); list.Add(17);
            int result = 0; foreach (int value in list) result = unchecked(result * 31 + value);
            return result;
        }
        public static int Empty(int input, int mode)
        {
            var list = new List<int>(); var e = list.GetEnumerator(); int result = e.Current;
            if (e.MoveNext() || e.MoveNext()) return -100;
            e.Dispose(); if (e.MoveNext()) return -101; return result + e.Current;
        }
        public static int Copy(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode);
            var first = list.GetEnumerator(); first.MoveNext(); var copy = first;
            first.MoveNext(); int result = unchecked(first.Current * 31 + copy.Current);
            first.Dispose(); copy.MoveNext(); return unchecked(result + copy.Current);
        }
        public static int Mutation(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode);
            var e = list.GetEnumerator(); e.MoveNext();
            if ((mode & 3) == 0) list.Add(input);
            else if ((mode & 3) == 1) list[0] = mode;
            else if ((mode & 3) == 2) list.RemoveAt(0);
            else list.Clear();
            int result = e.Current;
            try { e.MoveNext(); return -100; }
            catch (InvalidOperationException error) { return unchecked(result + (error.Message == "Collection was modified; enumeration operation may not execute." ? 13 : -100)); }
        }
        public static int NoMutation(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode);
            var e = list.GetEnumerator(); list.Capacity = 13; list.Remove(unchecked(input + 1) == mode ? unchecked(input + 2) : unchecked(input + 1));
            int sum = 0; while (e.MoveNext()) sum = unchecked(sum + e.Current); return sum;
        }
        public static int References(int input, int mode)
        {
            var list = new List<ValueVectorBox>(); list.Add(new ValueVectorBox(input)); list.Add(new ValueVectorBox(mode));
            var e = list.GetEnumerator(); if (e.Current != null) return -100;
            e.MoveNext(); var saved = e.Current; int result = saved.Number;
            list[0].Number = mode; if (e.Current != saved) return -101;
            result = unchecked(result + e.Current.Number); e.MoveNext(); result = unchecked(result + e.Current.Number);
            e.MoveNext(); if (e.Current != null) return -102; return result;
        }
        public static int Values(int input, int mode)
        {
            var list = new List<ValueVectorCell>(); ValueVectorCell item = default; item.Number = input; item.Reference = new ValueVectorBox(mode); list.Add(item);
            var e = list.GetEnumerator(); e.MoveNext(); var copy = e.Current; copy.Number = mode;
            int result = e.Current.Number; copy.Reference.Number = input;
            result = unchecked(result + e.Current.Reference.Number); e.MoveNext();
            return result + (e.Current.Reference == null ? 1 : -100);
        }
        public static int Default(int input, int mode)
        {
            List<int>.Enumerator e = default; e.Dispose(); int result = e.Current;
            try { e.MoveNext(); return -100; } catch (NullReferenceException) { return result + 7; }
        }
        public static int EndMutation(int input, int mode)
        {
            var list = new List<int>(); var e = list.GetEnumerator(); e.MoveNext(); list.Clear();
            try { e.MoveNext(); return -100; }
            catch (InvalidOperationException) { return e.Current + 11; }
        }
        public static int BreakAndFinally(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode); int result = 0;
            try { foreach (int item in list) { result = item; if (mode == 0) break; throw new InvalidOperationException(); } }
            catch (InvalidOperationException) { result = unchecked(result + 3); }
            return unchecked(result + list.Count);
        }
        public static int AllocatingValues(int input, int mode)
        {
            var list = new List<ValueVectorCell>(); ValueVectorCell item = default;
            item.Number = input; item.Reference = new ValueVectorBox(mode);
            for (int i = 0; i < 9; ++i) list.Add(item);
            var e = list.GetEnumerator(); list = null; int result = 0;
            while (e.MoveNext())
            {
                var value = e.Current;
                var allocated = new ValueVectorBox(unchecked(input + mode));
                result = unchecked(result + allocated.Number + value.Number + value.Reference.Number);
            }
            e.Dispose(); return result + (e.Current.Reference == null ? 1 : -100);
        }
        public static int NestedOwner(int input, int mode)
        {
            EnumerationOuter<ValueVectorBox>.Inner<int> first = default;
            first.First = new ValueVectorBox(input); first.Second = mode;
            EnumerationOuter<int>.Inner<ValueVectorBox> second = default;
            second.First = mode; second.Second = first.First;
            return unchecked(first.Pick() + second.Pick().Number + second.First);
        }
        public static int UnsupportedLegacyCurrent(int input, int mode)
        {
            var e = new List<int>().GetEnumerator(); return ((System.Collections.IEnumerator)e).Current == null ? 1 : 0;
        }
    }
}
