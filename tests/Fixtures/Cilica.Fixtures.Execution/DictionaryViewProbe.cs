using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public struct DictionarySearchValue : IEquatable<DictionarySearchValue>
    {
        public int Number;
        public ValueVectorBox Observer;
        public bool Equals(DictionarySearchValue other)
        {
            int saved = Number; Number++;
            Observer.Number++;
            var allocation = new ValueVectorBox(saved);
            if (other.Number == 3) throw new InvalidOperationException("value equality");
            return allocation.Number == other.Number;
        }
    }
    public static class DictionaryViewProbe
    {
        public static int Identity(int input, int mode)
        {
            var d = new Dictionary<int, int>(); var keys = d.Keys; var values = d.Values;
            if (keys != d.Keys || values != d.Values || (object)keys == values) return -100;
            if ((object)keys != ((IDictionary<int, int>)d).Keys || (object)values != ((IDictionary<int, int>)d).Values) return -101;
            if ((object)keys != ((IReadOnlyDictionary<int, int>)d).Keys || (object)values != ((IReadOnlyDictionary<int, int>)d).Values) return -102;
            if ((object)keys != ((IDictionary)d).Keys || (object)values != ((IDictionary)d).Values) return -103;
            if (((ICollection)keys).SyncRoot != d || ((ICollection)values).SyncRoot != d) return -104;
            return keys.Count + values.Count + (((ICollection<int>)keys).IsReadOnly ? 11 : 0) + (((ICollection<int>)values).IsReadOnly ? 13 : 0);
        }
        public static int Live(int input, int mode)
        {
            var d = new Dictionary<int, int>(); var keys = d.Keys; var values = d.Values;
            d.TryAdd(input, mode); int result = keys.Count + values.Count;
            d.TryAdd(mode, input); result += keys.Count + values.Count;
            result += ((ICollection<int>)keys).Contains(input) ? 7 : -100;
            d.Remove(input); result += keys.Count + values.Count;
            result += ((ICollection<int>)keys).Contains(input) ? -100 : 11;
            d.Clear(); return result + keys.Count + values.Count;
        }
        public static int ManualAndNull(int input, int mode)
        {
            var d = new Dictionary<int, int>(); var keys = new Dictionary<int, int>.KeyCollection(d); var values = new Dictionary<int, int>.ValueCollection(d);
            if (keys == d.Keys || values == d.Values) return -100;
            int result = 0;
            try { new Dictionary<int, int>.KeyCollection(null); } catch (ArgumentNullException e) { result += e.ParamName == "dictionary" ? 11 : -100; }
            try { new Dictionary<int, int>.ValueCollection(null); } catch (ArgumentNullException e) { result += e.ParamName == "dictionary" ? 13 : -100; }
            d.TryAdd(input, mode); return result + keys.Count + values.Count;
        }
        public static int ReadOnlyFaults(int input, int mode)
        {
            var d = new Dictionary<string, string>(); d.TryAdd("a", "b");
            ICollection<string> keys = d.Keys, values = d.Values; int result = 0;
            try { keys.Add(null); } catch (NotSupportedException e) { result += e.Message == "Mutating a key collection derived from a dictionary is not allowed." ? 11 : -100; }
            try { keys.Remove(null); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { keys.Clear(); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { values.Add(null); } catch (NotSupportedException e) { result += e.Message == "Mutating a value collection derived from a dictionary is not allowed." ? 13 : -100; }
            try { values.Remove(null); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { values.Clear(); } catch (NotSupportedException e) { result += e.Message.Length; }
            return result + d.Count;
        }
        public static int StringKeys(int input, int mode)
        {
            var d = new Dictionary<string, int>(); var keys = d.Keys; int result = 0;
            try { ((ICollection<string>)keys).Contains(null); } catch (ArgumentNullException e) { result += e.ParamName == "key" ? 11 : -100; }
            d.TryAdd("first", input); d.TryAdd("second", mode);
            result += ((ICollection<string>)keys).Contains("first") ? 13 : -100;
            d.Remove("first"); result += ((ICollection<string>)keys).Contains("first") ? -100 : 17;
            ICollection<string> via = keys; return result + (via.Contains("second") ? 19 : -100);
        }
        public static int ValuesAndReferences(int input, int mode)
        {
            var d = new Dictionary<int, ValueVectorCell>(); ValueVectorCell cell = default;
            cell.Number = input; cell.Reference = new ValueVectorBox(mode); d.TryAdd(input, cell);
            var keys = d.Keys; var values = d.Values;
            d.Remove(input); d.TryAdd(mode, cell);
            return keys.Count + values.Count + (((ICollection<int>)keys).Contains(mode) ? 7 : -100);
        }
        public static Dictionary<int, ValueVectorBox>.KeyCollection CreateTracked()
        {
            var d = new Dictionary<int, ValueVectorBox>(); d.TryAdd(3, new ValueVectorBox(17)); return d.Keys;
        }
        public static int Tracked(int input, int mode) => CreateTracked().Count;
        public static int ValueSearch(int input, int mode)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, input); d.TryAdd(2, mode); d.TryAdd(3, 17);
            ICollection<int> values = d.Values; int result = d.ContainsValue(input) ? 3 : -100;
            d.Remove(1); result += values.Contains(input) ? 5 : 7;
            d.Remove(3); d.TryAdd(4, input); result += values.Contains(input) ? 11 : -100;
            d.Clear(); return result + (d.ContainsValue(input) ? -100 : 13);
        }
        public static int NullValues(int input, int mode)
        {
            var d = new Dictionary<int, string>(); d.TryAdd(1, null); d.TryAdd(2, "x");
            ICollection<string> values = d.Values; int result = values.Contains(null) ? 3 : -100;
            d.Remove(1); result += values.Contains(null) ? -100 : 5;
            result += values.Contains("x") ? 7 : -100;
            d.Clear(); return result + (values.Contains(null) ? -100 : 11);
        }
        public static int MutableValues(int input, int mode)
        {
            var observer = new ValueVectorBox(0); DictionarySearchValue first = default;
            first.Number = input; first.Observer = observer; DictionarySearchValue second = first; second.Number = mode;
            var d = new Dictionary<int, DictionarySearchValue>(); d.TryAdd(1, first); d.TryAdd(2, second);
            int result = 0;
            try { result = ((ICollection<DictionarySearchValue>)d.Values).Contains(second) ? 3 : -100; }
            catch (InvalidOperationException e) { result = e.Message == "value equality" ? 5 : -100; }
            d.TryGetValue(1, out var saved);
            return unchecked(result + saved.Number + observer.Number * 31);
        }
        public static int UnsupportedValuesContains(int input, int mode) => ((ICollection<ValueVectorBox>)new Dictionary<int, ValueVectorBox>().Values).Contains(null) ? 1 : 0;
    }
}
