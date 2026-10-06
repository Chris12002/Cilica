using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public struct SearchCell : IEquatable<SearchCell>
    {
        public int Number;
        public ValueVectorBox Payload;
        public bool Equals(SearchCell other) { int previous = Number++; return previous == other.Number; }
    }
    public struct AllocatingSearchCell : IEquatable<AllocatingSearchCell>
    {
        public int Number;
        public ValueVectorBox Payload;
        public bool Equals(AllocatingSearchCell other)
        {
            Payload = new ValueVectorBox(other.Number);
            if (other.Number < 0) throw new InvalidOperationException("search");
            return Number == other.Number;
        }
    }
    public static class ListSearchProbe
    {
        public static int Integers(int input, int mode)
        {
            var list = new List<int>(12); if (list.Contains(0) || list.IndexOf(0) != -1 || list.Remove(0)) return -100;
            list.Add(input); list.Add(mode); list.Add(input);
            int result = list.IndexOf(mode);
            if (!list.Remove(input) || list.Count != 2) return -101;
            result = unchecked(result * 31 + list[0] + list.IndexOf(input));
            list.Remove(input); list.Remove(mode);
            if (list.Contains(0) || list.Count != 0) return -102;
            return result + list.Capacity;
        }
        public static int Strings(int input, int mode)
        {
            var list = new List<string>(); list.Add(null); list.Add("abc"); list.Add(string.Concat("a", "bc"));
            if (!list.Contains(null) || list.IndexOf("abc") != 1) return -100;
            list.Remove("abc"); list.Remove(null);
            return list.Count + list[0].Length + (list.Contains("missing") ? -100 : 1);
        }
        public static int Values(int input, int mode)
        {
            var list = new List<SearchCell>(); SearchCell cell = default; cell.Number = input; cell.Payload = new ValueVectorBox(mode);
            list.Add(cell); list.Add(cell); cell.Number = mode;
            int result = list.IndexOf(cell); cell = list[0];
            if (!list.Contains(cell)) return -100;
            result = unchecked(result + list[0].Number); cell = list[0];
            if (!list.Remove(cell)) return -101;
            return unchecked(result + list[0].Number + list[0].Payload.Number + list.Count);
        }
        public static int ThrowingEquality(int input, int mode)
        {
            var trace = new KeyTrace(); var list = new List<CollisionKey>();
            list.Add(new CollisionKey(input, trace)); list.Add(new CollisionKey(mode, trace)); trace.EqualityFailure = 1;
            int result = 0;
            try { list.Remove(new CollisionKey(input, trace)); } catch (InvalidOperationException) { result += list.Count; }
            trace.EqualityFailure = 0;
            if (!list.Remove(new CollisionKey(list[0].Number, trace))) return -100;
            return unchecked(result + list[0].Number + trace.Equalities * 31);
        }
        public static int Arrays(int input, int mode)
        {
            IList<int> array = new[] { input, mode, input }; int result = array.IndexOf(mode);
            if (!array.Contains(input)) return -100;
            try { array.Remove(mode); } catch (NotSupportedException e) { result += e.Message == "Collection was of a fixed size." ? 13 : -100; }
            return result + array.Count;
        }
        public static int ArrayValues(int input, int mode)
        {
            var array = new SearchCell[2]; SearchCell cell = default; cell.Number = input; cell.Payload = new ValueVectorBox(mode); array[1] = cell;
            IList<SearchCell> view = array;
            int result = view.IndexOf(cell); if (!view.Contains(cell)) return -100;
            try { view.Remove(cell); } catch (NotSupportedException e) { result += e.Message.Length; }
            return unchecked(result + array[1].Number + array[1].Payload.Number);
        }
        public static int EmptyRemove(int input, int mode)
        {
            ICollection<ValueVectorEmpty> array = new ValueVectorEmpty[0];
            try { array.Remove(default); return -100; } catch (NotSupportedException e) { return e.Message.Length; }
        }
        public static int ReadOnly(int input, int mode)
        {
            var list = new List<string>(); list.Add(null); list.Add("abc"); var view = list.AsReadOnly();
            int result = view.IndexOf("abc"); if (!view.Contains(null)) return -100;
            try { ((ICollection<string>)view).Remove("missing"); } catch (NotSupportedException e) { result += e.Message.Length; }
            list.Remove(null); return result + view.IndexOf("abc") + view.Count;
        }
        public static int Mixed(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode);
            IList<int> values = mode < 0 ? (IList<int>)new[] { input, mode } : list;
            int result = values.IndexOf(mode); if (!values.Contains(input)) return -100;
            try { values.Remove(input); } catch (NotSupportedException e) { result += e.Message.Length; }
            return result + values.Count;
        }
        public static int Nulls(int input, int mode)
        {
            IList<int> absent = null; int result = 0;
            try { absent.IndexOf(input / mode); }
            catch (DivideByZeroException) { result += 3; } catch (OverflowException) { result += 5; } catch (NullReferenceException) { result += 7; }
            try { absent.Contains(input); } catch (NullReferenceException) { result += 11; }
            try { absent.Remove(input); } catch (NullReferenceException) { result += 13; }
            return result;
        }
        public static int AllocatingEquality(int input, int mode)
        {
            var array = new AllocatingSearchCell[1]; AllocatingSearchCell value = default; value.Number = input; array[0] = value;
            IList<AllocatingSearchCell> view = array; value.Number = mode; int result = 0;
            try { result = view.IndexOf(value); } catch (InvalidOperationException) { result = 7; }
            if (array[0].Payload == null) return -100;
            return unchecked(result + array[0].Payload.Number);
        }
        public static int UnsupportedNullable(int input, int mode) => new List<int?>().Contains(input) ? 1 : 0;
        public static int UnsupportedReference(int input, int mode) => new List<ValueVectorBox>().Contains(new ValueVectorBox(input)) ? 1 : 0;
        public static List<SearchCell> CreateTracked()
        {
            var list = new List<SearchCell>(); SearchCell cell = default;
            cell.Number = 1; cell.Payload = new ValueVectorBox(7); list.Add(cell);
            cell.Number = 2; cell.Payload = new ValueVectorBox(9); list.Add(cell); return list;
        }
        public static void RemoveTracked(List<SearchCell> list) { SearchCell cell = default; cell.Number = 1; list.Remove(cell); }
        public static void ClearTracked(List<SearchCell> list) { list.Clear(); }
        public static int Tracked(int input, int mode) { var list = CreateTracked(); RemoveTracked(list); int count = list.Count; ClearTracked(list); return count; }
    }
}
