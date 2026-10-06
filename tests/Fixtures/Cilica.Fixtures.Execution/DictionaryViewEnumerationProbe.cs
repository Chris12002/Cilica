using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class DictionaryEnumerationHolder
    {
        public Dictionary<int, ValueVectorBox> Dictionary;
        public Dictionary<int, ValueVectorBox>.KeyCollection.Enumerator Keys;
        public Dictionary<int, ValueVectorBox>.ValueCollection.Enumerator Values;
    }
    public static class DictionaryViewEnumerationProbe
    {
        public static int Order(int input, int mode)
        {
            var d = new Dictionary<int, int>(); for (int i = 0; i < 20; ++i) d.TryAdd(i, unchecked(input + i));
            d.Remove(3); d.Remove(7); d.TryAdd(40, mode); int result = 0;
            foreach (int key in d.Keys) result = unchecked(result * 31 + key);
            foreach (int value in d.Values) result = unchecked(result * 31 + value);
            return result;
        }
        public static int RemoveAndClear(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); d.TryAdd(2, mode); d.TryAdd(3, 17);
            var keys = d.Keys.GetEnumerator(); var values = d.Values.GetEnumerator(); keys.MoveNext(); values.MoveNext();
            d.Remove(2); int result = unchecked(keys.Current + values.Current);
            keys.MoveNext(); values.MoveNext(); result = unchecked(result + keys.Current + values.Current);
            d.Clear(); if (keys.MoveNext() || values.MoveNext()) return -100;
            return result + keys.Current + values.Current;
        }
        public static int OverwriteAndAdd(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); d.TryAdd(2, mode);
            var values = d.Values.GetEnumerator(); values.MoveNext(); int result = values.Current;
            d[2] = input; d.TryAdd(1, mode); values.MoveNext(); result = unchecked(result + values.Current);
            var keys = d.Keys.GetEnumerator(); d.Remove(1); d.TryAdd(3, mode);
            try { keys.MoveNext(); return -100; } catch (InvalidOperationException e) { result = unchecked(result + e.Message.Length); }
            try { values.MoveNext(); return -101; } catch (InvalidOperationException e) { return unchecked(result + e.Message.Length); }
        }
        public static int CopiesAndDefaults(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); d.TryAdd(2, mode);
            var keys = d.Keys.GetEnumerator(); var values = d.Values.GetEnumerator();
            int result = keys.Current + values.Current; keys.MoveNext(); values.MoveNext(); var keyCopy = keys; var valueCopy = values;
            keys.MoveNext(); values.MoveNext(); result = unchecked(result + keys.Current + values.Current + keyCopy.Current + valueCopy.Current);
            values.Dispose(); keys.Dispose(); keys.MoveNext(); values.MoveNext();
            if (keys.MoveNext() || values.MoveNext()) return -100;
            return result + keys.Current + values.Current;
        }
        public static int DefaultFaults(int input, int mode)
        {
            Dictionary<int, int>.KeyCollection.Enumerator keys = default;
            Dictionary<int, int>.ValueCollection.Enumerator values = default;
            keys.Dispose(); values.Dispose(); int result = keys.Current + values.Current;
            try { keys.MoveNext(); return -100; } catch (NullReferenceException) { result += 3; }
            try { values.MoveNext(); return -101; } catch (NullReferenceException) { result += 5; }
            return result;
        }
        public static int ReferencesAndValues(int input, int mode)
        {
            var d = new Dictionary<string, ValueVectorCell>(); ValueVectorCell value = default;
            value.Number = input; value.Reference = new ValueVectorBox(mode); d.TryAdd("a", value); d.TryAdd("b", value);
            var keys = d.Keys.GetEnumerator(); var values = d.Values.GetEnumerator(); keys.MoveNext(); values.MoveNext();
            var copy = values.Current; copy.Number = mode; d.Clear();
            var allocated = new ValueVectorBox(input);
            int result = unchecked(keys.Current.Length + values.Current.Number + values.Current.Reference.Number + allocated.Number);
            values.MoveNext(); keys.MoveNext();
            if (keys.Current != null || values.Current.Reference != null) return -100;
            return result;
        }
        public static DictionaryEnumerationHolder CreateKeys()
        {
            var holder = new DictionaryEnumerationHolder(); var d = new Dictionary<int, ValueVectorBox>(); d.TryAdd(1, new ValueVectorBox(17));
            holder.Dictionary = d; var keys = d.Keys.GetEnumerator(); keys.MoveNext(); holder.Keys = keys; return holder;
        }
        public static DictionaryEnumerationHolder CreateValues()
        {
            var holder = new DictionaryEnumerationHolder(); var d = new Dictionary<int, ValueVectorBox>(); d.TryAdd(1, new ValueVectorBox(17));
            holder.Dictionary = d; var values = d.Values.GetEnumerator(); values.MoveNext(); holder.Values = values; return holder;
        }
        public static void Clear(DictionaryEnumerationHolder holder) { holder.Dictionary.Clear(); }
        public static void EndKeys(DictionaryEnumerationHolder holder) { holder.Keys.MoveNext(); }
        public static void EndValues(DictionaryEnumerationHolder holder) { holder.Values.MoveNext(); }
        public static int TrackedKeys(int input, int mode) { var holder = CreateKeys(); Clear(holder); int result = holder.Keys.Current; EndKeys(holder); return result + holder.Keys.Current; }
        public static int TrackedValues(int input, int mode) { var holder = CreateValues(); Clear(holder); int result = holder.Values.Current.Number; EndValues(holder); return result + (holder.Values.Current == null ? 1 : -100); }
    }
}
