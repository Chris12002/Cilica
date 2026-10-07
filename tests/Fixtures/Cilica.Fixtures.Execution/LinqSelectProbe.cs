using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public struct SelectCell { public long Wide; public ValueVectorBox Reference; public int Number; }
    public sealed class SelectorState
    {
        public int Calls, Trace, Add, ThrowAt;
        public int[] Values;
        public SelectorState(int add, int throwAt) { Add = add; ThrowAt = throwAt; }
        public int Apply(int value)
        {
            Calls++; Trace = unchecked(Trace * 7 + value);
            if (ThrowAt != int.MinValue && value == ThrowAt) throw new InvalidOperationException("selector");
            if (Values != null) Values[1] = unchecked(Values[1] + 1);
            return unchecked(value + Add);
        }
        public int Second(int value) { Trace = unchecked(Trace * 11 + value); return unchecked(value * 3); }
        public SelectCell Cell(int value) { return new SelectCell { Wide = (long)value * 4294967297L, Reference = new ValueVectorBox(value), Number = Apply(value) }; }
        public int Read(SelectCell value) { return unchecked(value.Number + value.Reference.Number + (int)(value.Wide >> 32)); }
        public int? Nullable(int value) { return value == Add ? (int?)null : value; }
        public ValueVectorBox Box(int value) { Calls++; return new ValueVectorBox(value); }
        public int Reference(object value) { Calls++; return value == null ? 7 : ((string)value).Length; }
    }
    public sealed class SelectorList : PredicateSequence, IList<int>
    {
        public int Reads;
        public SelectorList() : base(0) { }
        public int Count { get { Reads = unchecked(Reads * 7 + 1); return 3; } }
        public int this[int index] { get { Reads = unchecked(Reads * 7 + 2 + index); return index + 10; } set { throw new NotSupportedException(); } }
        public bool IsReadOnly => true;
        public void Add(int item) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int item) { throw new NotSupportedException(); }
        public void CopyTo(int[] array, int index) { throw new NotSupportedException(); }
        public bool Remove(int item) { throw new NotSupportedException(); }
        public int IndexOf(int item) { throw new NotSupportedException(); }
        public void Insert(int index, int item) { throw new NotSupportedException(); }
        public void RemoveAt(int index) { throw new NotSupportedException(); }
    }
    public static class LinqSelectProbe
    {
        private static int[] Three() { var a = new int[3]; a[0] = 1; a[1] = 2; a[2] = 3; return a; }
        public static int Deferred(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var source = Three(); var query = source.Select(state.Apply);
            if (state.Calls != 0) return -100;
            var first = query.GetEnumerator(); var second = query.GetEnumerator();
            if ((object)first != query || first == second || state.Calls != 0) return -101;
            source[0] = b; int result = first.Current;
            if (first.MoveNext()) result = unchecked(result + first.Current);
            if (second.MoveNext()) result = unchecked(result + second.Current);
            first.Dispose(); second.Dispose(); if (first.Current != 0 || first.MoveNext()) return -102;
            foreach (int value in query) result = unchecked(result + value);
            return unchecked(result + state.Calls * 37);
        }
        public static int EmptyAndNull(int a, int b)
        {
            var state = new SelectorState(a, 0); var query = new int[0].Select(state.Cell);
            if ((object)query != Array.Empty<SelectCell>()) return -100;
            IEnumerable<int> source = a < 0 ? null : new int[0]; int result = 0;
            try { source.Select((Func<int, int>)null); } catch (ArgumentNullException e) { result = e.ParamName == "source" ? 3 : e.ParamName == "selector" ? 7 : -101; }
            return result + state.Calls + query.Count() + (query.Any() ? 100 : 0);
        }
        public static int Cardinality(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var query = Three().Select(state.Apply);
            var e = query.GetEnumerator(); e.MoveNext(); int before = e.Current;
            if (!query.Any() || state.Calls != 1) return -100;
            int result = query.Count(); var values = query.ToArray();
            if (e.Current != before) return -101;
            if (e.MoveNext()) result = unchecked(result + e.Current); e.Dispose();
            return unchecked(result + values[0] + values[2] + state.Calls * 37);
        }
        public static int Chained(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var first = Three().Select(state.Apply); var query = first.Select(state.Second);
            if (!query.Any() || state.Calls != 0) return -100;
            var values = query.ToArray(); int count = query.Count();
            var e = first.GetEnumerator(); if ((object)e != first) return -101;
            e.MoveNext(); int before = e.Current;
            foreach (int value in query) count = unchecked(count + value);
            if (e.Current != before) return -102; e.Dispose();
            return unchecked(count + values[1] + state.Trace + state.Calls * 37);
        }
        public static int Filtered(int a, int b)
        {
            var predicate = new PredicateState(a, int.MinValue); var state = new SelectorState(b, int.MinValue);
            var where = Three().Where(predicate.Match); var query = where.Select(state.Apply).Select(state.Second);
            int result = (query.Any() ? 13 : 0) + query.Count(); var values = query.ToArray();
            foreach (int value in values) result = unchecked(result + value);
            var e = where.GetEnumerator(); if ((object)e != where) return -100;
            e.MoveNext(); int before = e.Current; query.Count(); if (e.Current != before) return -101; e.Dispose();
            return unchecked(result + state.Trace + state.Calls * 37 + predicate.Calls * 53);
        }
        public static int Fallback(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var source = new PredicateSequence(0); var query = source.Select(state.Apply);
            if (source.Trace != 0) return -100; var e = query.GetEnumerator(); if (source.Trace != 0) return -101; e.Dispose();
            int result = query.Any() ? 7 : 0; result += query.Count(); var values = query.ToArray();
            foreach (int value in query) { result = unchecked(result + value); if (b < 0) break; }
            return unchecked(result + values[2] + state.Calls * 37 + source.Trace * 53);
        }
        public static int Faults(int a, int b)
        {
            var state = new SelectorState(a, 2); var source = new PredicateSequence(b & 3); var query = source.Select(state.Apply); int result = 0;
            try { if (a < 0) result = query.Count(); else if (a == 0) result = query.ToArray().Length; else foreach (int value in query) result = unchecked(result + value); }
            catch (InvalidOperationException e) { result = e.Message == "selector" ? 17 : e.Message == "current" ? 19 : e.Message == "dispose" ? 23 : -100; }
            return unchecked(result + state.Calls * 37 + source.Trace * 53);
        }
        public static int Resume(int a, int b)
        {
            var state = new SelectorState(a, 2); var e = Three().Select(state.Apply).GetEnumerator();
            e.MoveNext(); int before = e.Current;
            try { e.MoveNext(); return -100; } catch (InvalidOperationException) { if (e.Current != before) return -101; }
            if (!e.MoveNext()) return -102; int result = e.Current;
            if (e.MoveNext() || e.Current != 0) return -103; e.Dispose(); return unchecked(result + state.Calls * 37);
        }
        public static int Values(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var query = new[] { a == int.MinValue ? 5 : a, b == int.MinValue ? 7 : b }.Select(state.Cell);
            var values = query.ToArray(); var numbers = query.Select(state.Read).ToArray();
            return unchecked(values[0].Reference.Number + values[1].Number + (int)(values[0].Wide >> 32) + numbers[1]);
        }
        public static int Nullable(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var values = new[] { a, b }.Select(state.Nullable).ToArray();
            return values[0].GetValueOrDefault(13) + values[1].GetValueOrDefault(17);
        }
        public static int Covariant(int a, int b)
        {
            var state = new SelectorState(a, 0); IEnumerable<object> source = new string[] { "a", null, "abc" };
            var values = source.Select(state.Reference).ToArray(); return values[0] + values[1] + values[2] + state.Calls * 37;
        }
        public static int Mutations(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); state.Values = Three(); var query = state.Values.Select(state.Apply);
            var values = query.ToArray(); return unchecked(values[1] + query.Count() + state.Values[1] + state.Calls * 37);
        }
        public static int ListTerminals(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var source = new SelectorList(); var query = source.Select(state.Apply).Select(state.Second);
            int result = query.Any() ? 11 : 0; if (state.Calls != 0 || source.Trace != 0) return -100;
            result += query.Count(); var values = query.ToArray(); if (source.Trace != 0) return -101;
            return unchecked(result + values[2] + state.Trace + source.Reads * 37);
        }
        public static int ListIteration(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var source = new SelectorList(); var query = source.Select(state.Apply).Select(state.Second); int result = 0;
            foreach (int value in query) result = unchecked(result + value);
            if (source.Reads != 0) return -100;
            return unchecked(result + state.Trace + source.Trace * 37);
        }
        public static int ReferenceCurrent(int a, int b)
        {
            var state = new SelectorState(a, 0); var source = Three(); var e = source.Select(state.Box).GetEnumerator();
            if (e.Current != null) return -100;
            e.MoveNext(); var before = e.Current; source[0] = b;
            var pressure = new ValueVectorBox(a); if (e.Current != before) return -101;
            e.Dispose(); if (e.Current != null || e.MoveNext()) return -102;
            return unchecked(before.Number + pressure.Number + state.Calls * 37);
        }
        public static int GenericResume(int a, int b)
        {
            var source = new PredicateSequence(0); var state = new SelectorState(a, 2); var e = source.Select(state.Apply).GetEnumerator();
            int result = 0; try { ((IEnumerator)e).Reset(); return -100; } catch (NotSupportedException) { result = 7; }
            e.MoveNext(); int before = e.Current;
            try { e.MoveNext(); return -101; } catch (InvalidOperationException) { if (e.Current != before) return -102; }
            if (!e.MoveNext()) return -103; result = unchecked(result + e.Current); e.Dispose();
            return unchecked(result + source.Trace * 37 + state.Calls * 53);
        }
        public static int WhereAfterSelect(int a, int b)
        {
            var source = Three(); var state = new SelectorState(a, int.MinValue); var predicate = new PredicateState(b, int.MinValue);
            var first = source.Select(state.Apply); var query = first.Where(predicate.Match).Select(state.Second);
            int result = query.Count(); var values = query.ToArray(); foreach (int value in values) result = unchecked(result + value);
            var e = first.GetEnumerator(); bool reused = (object)e == first; e.Dispose();
            return unchecked(result + state.Trace + predicate.Calls * 37 + (reused ? 53 : 71));
        }
        public static int UnsupportedIndexed(int a, int b) { return Three().Select((value, index) => value + index).Count(); }
        public static int ListSelection(int a, int b) { return new List<int>().Select(new SelectorState(a, 0).Apply).Count(); }
        public static int UncountedCollection(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var source = new PredicateCollection(); var query = source.Select(state.Apply);
            int result = query.Any() ? 11 : 0; result += query.Count(); var values = query.ToArray();
            return unchecked(result + values[2] + state.Calls * 37 + source.Trace * 53);
        }
    }
}
