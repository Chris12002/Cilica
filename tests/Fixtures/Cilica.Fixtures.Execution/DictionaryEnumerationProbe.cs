using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class DictionaryEnumerationProbe
    {
        public static int Integers(int input, int mode)
        {
            var d = new Dictionary<int, int>();
            for (int i = 0; i < 24; ++i) d.TryAdd(i, unchecked(input + i));
            d.Remove(3); d.Remove(7); d.TryAdd(40, mode); d.TryAdd(41, input);
            int result = 0;
            foreach (var pair in d) result = unchecked(result * 31 + pair.Key + pair.Value);
            return result;
        }
        public static int RemoveWhileIterating(int input, int mode)
        {
            var d = new Dictionary<int, int>(); for (int i = 0; i < 8; ++i) d.TryAdd(i, unchecked(input + i));
            int result = 0;
            foreach (var pair in d)
            {
                if ((pair.Key & 1) == 0) d.Remove(pair.Key + 1);
                d.Remove(pair.Key); result = unchecked(result * 31 + pair.Value);
            }
            return result + d.Count;
        }
        public static int ClearWhileIterating(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); d.TryAdd(2, mode);
            var e = d.GetEnumerator(); e.MoveNext(); var before = e.Current; d.Clear();
            if (e.Current.Value != before.Value) return -100;
            if (e.MoveNext()) return -101;
            return e.Current.Key + e.Current.Value + before.Value;
        }
        public static int OverwriteAndDuplicates(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); d.TryAdd(2, mode);
            var e = d.GetEnumerator(); e.MoveNext(); d[1] = mode; d[2] = input;
            if (d.TryAdd(1, 17)) return -100;
            int result = e.Current.Value; e.MoveNext(); return unchecked(result + e.Current.Value);
        }
        public static int AddedKeyFault(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); d.TryAdd(2, mode);
            var e = d.GetEnumerator(); e.MoveNext(); d.Remove(2); d.TryAdd(3, mode);
            try { e.MoveNext(); return -100; }
            catch (InvalidOperationException error) { return unchecked(e.Current.Value + (error.Message == "Collection was modified; enumeration operation may not execute." ? 11 : -100)); }
        }
        public static int CopiesAndEnd(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); d.TryAdd(2, mode);
            var e = d.GetEnumerator(); if (e.Current.Key != 0 || e.Current.Value != 0) return -100;
            e.MoveNext(); var copy = e; e.MoveNext();
            int result = unchecked(e.Current.Value + copy.Current.Value); copy.Dispose(); copy.MoveNext();
            result = unchecked(result + copy.Current.Value); e.MoveNext();
            if (e.MoveNext() || e.Current.Key != 0 || e.Current.Value != 0) return -101;
            return result;
        }
        public static int EmptyAndDefault(int input, int mode)
        {
            var empty = new Dictionary<int, int>().GetEnumerator(); if (empty.MoveNext()) return -100;
            Dictionary<int, int>.Enumerator e = default; e.Dispose(); int result = e.Current.Key + e.Current.Value;
            try { e.MoveNext(); return -101; } catch (NullReferenceException) { return result + 7; }
        }
        public static int ValuesAndAllocation(int input, int mode)
        {
            var d = new Dictionary<int, ValueVectorCell>(); ValueVectorCell value = default;
            value.Number = input; value.Reference = new ValueVectorBox(mode); d.TryAdd(1, value); d.TryAdd(2, value);
            var e = d.GetEnumerator(); d = null; int result = 0;
            while (e.MoveNext())
            {
                var pair = e.Current; var allocated = new ValueVectorBox(input); var copied = pair.Value; copied.Number = mode;
                result = unchecked(result + pair.Value.Number + pair.Value.Reference.Number + allocated.Number);
            }
            return result + (e.Current.Value.Reference == null ? 1 : -100);
        }
        public static int References(int input, int mode)
        {
            var d = new Dictionary<string, ValueVectorBox>(); d.TryAdd("one", new ValueVectorBox(input)); d.TryAdd("two", new ValueVectorBox(mode));
            var e = d.GetEnumerator(); e.MoveNext(); var saved = e.Current; d.Remove(saved.Key);
            int result = saved.Value.Number; if (e.Current.Value != saved.Value) return -100;
            while (e.MoveNext()) result = unchecked(result + e.Current.Value.Number);
            return result + (e.Current.Key == null && e.Current.Value == null ? 1 : -100);
        }
        public static int CurrentLifetime(int input, int mode)
        {
            var d = new Dictionary<int, ValueVectorBox>(); d.TryAdd(1, new ValueVectorBox(input));
            var e = d.GetEnumerator(); e.MoveNext(); d.Clear(); d = null;
            var allocation = new ValueVectorBox(mode);
            int result = unchecked(e.Current.Value.Number + allocation.Number);
            if (e.MoveNext() || e.Current.Value != null) return -100;
            return result;
        }
        public static int FinallyAndEndedAdd(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); int result = 0;
            try { foreach (var pair in d) { result = pair.Value; throw new InvalidOperationException(); } }
            catch (InvalidOperationException) { result = unchecked(result + 3); }
            var e = d.GetEnumerator(); while (e.MoveNext()) { }
            d.TryAdd(2, mode);
            try { e.MoveNext(); return -100; } catch (InvalidOperationException) { return result + e.Current.Value; }
        }
    }
}
