using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class DistinctCollection : PredicateSequence, ICollection<int>
    {
        public int Counts;
        public DistinctCollection(int mode) : base(mode) { }
        public int Count { get { Counts++; if (Mode == 3) throw new InvalidOperationException("count"); return 3; } }
        public bool IsReadOnly => true;
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int value) { throw new NotSupportedException(); }
        public bool Remove(int value) { throw new NotSupportedException(); }
        public void CopyTo(int[] array, int index) { throw new NotSupportedException(); }
    }
    public sealed class DistinctSelector
    {
        public KeyTrace Trace;
        public int Cutoff;
        public DistinctSelector(KeyTrace trace, int cutoff) { Trace = trace; Cutoff = cutoff; }
        public bool Match(CollisionKey value) => value.Number > Cutoff;
        public CollisionKey Project(CollisionKey value) => new CollisionKey(value.Number % 3, Trace);
    }
    public static class LinqDistinctProbe
    {
        private static int[] Values(int a, int b) { return new[] { a, b, a, b }; }
        public static int Integers(int a, int b)
        {
            var values = new int[40]; for (int i = 0; i < 40; i++) values[i] = unchecked(a + i % 19);
            var query = values.Distinct(); int result = query.Count();
            foreach (int value in query) result = unchecked(result * 7 + value);
            var array = query.ToArray(); for (int i = 0; i < array.Length; i++) result = unchecked(result * 11 + array[i]);
            return unchecked(result + array.Length + (query.Any() ? 31 : 0));
        }
        public static int Identity(int a, int b)
        {
            var query = Values(a, b).Distinct(); var e = query.GetEnumerator(); var other = query.GetEnumerator();
            if ((object)e != query || e == other) return -100; int result = e.Current;
            e.MoveNext(); other.MoveNext(); result = unchecked(result + e.Current + other.Current);
            int before = e.Current; result += query.Count(); query.ToArray(); query.Any();
            if (e.Current != before) return -101; e.Dispose(); other.Dispose(); if (e.Current != 0 || e.MoveNext()) return -102;
            var last = query.GetEnumerator(); if (last == e || last == other) return -103; last.Dispose(); return result;
        }
        public static int Unstarted(int a, int b)
        {
            var query = Values(a, b).Distinct(); int result = query.Count() + query.ToArray().Length + (query.Any() ? 7 : 0);
            var e = query.GetEnumerator(); if ((object)e != query) return -100;
            e.Dispose(); return result;
        }
        public static int EmptyAndNull(int a, int b)
        {
            var query = new int[0].Distinct(); if ((object)query != Array.Empty<int>()) return -100;
            int result = query.Count() + query.ToArray().Length + (query.Any() ? 100 : 0);
            try { ((IEnumerable<int>)null).Distinct(); } catch (ArgumentNullException e) { result += e.ParamName == "source" ? 7 : -101; }
            return result;
        }
        public static int Text(int a, int b)
        {
            string first = string.Concat("o", "ne"), equal = string.Concat("on", "e");
            var query = new[] { first, null, equal, null, "two" }.Distinct(); var result = query.ToArray();
            if (result.Length != 3 || (object)result[0] != first || result[1] != null || result[2] != "two") return -100;
            return query.Count() + result[0].Length;
        }
        public static int MutableValues(int a, int b)
        {
            var trace = new KeyTrace(); var values = new[] { new CollisionKey(a, trace), new CollisionKey(b, trace), new CollisionKey(a, trace) };
            var query = values.Distinct(); int result = 0;
            foreach (var value in query) result = unchecked(result * 7 + value.Number);
            var array = query.ToArray(); foreach (var value in array) result = unchecked(result * 11 + value.Number);
            result = unchecked(result + query.Count() + trace.Hashes * 1009 + trace.Equalities * 31);
            return unchecked(result + values[0].Number);
        }
        public static int AnySkipsHash(int a, int b)
        {
            var trace = new KeyTrace(); trace.HashFailure = 1;
            var query = new[] { new CollisionKey(a, trace) }.Distinct();
            if (!query.Any() || trace.Hashes != 0) return -100;
            int result = 0; try { query.Count(); } catch (InvalidOperationException e) { result += e.Message == "hash" ? 7 : -101; }
            try { query.ToArray(); } catch (InvalidOperationException e) { result += e.Message == "hash" ? 11 : -102; }
            return result + trace.Hashes;
        }
        public static int Fallback(int a, int b)
        {
            var source = new PredicateSequence(0); var query = source.Distinct(); if (source.Trace != 0) return -100;
            var e = query.GetEnumerator(); if (source.Trace != 0) return -101; e.Dispose();
            int result = query.Count() + query.ToArray().Length + (query.Any() ? 7 : 0);
            foreach (int value in query) { result += value; if (b < 0) break; }
            return unchecked(result + source.Trace * 37);
        }
        public static int Faults(int a, int b)
        {
            var source = new PredicateSequence(b & 3); var query = source.Distinct(); int result = 0;
            try { if (a < 0) result = query.Count(); else if (a == 0) result = query.ToArray().Length; else if (a == 1) result = query.Any() ? 5 : 7; else foreach (int value in query) result += value; }
            catch (InvalidOperationException e) { result = e.Message == "current" ? 19 : e.Message == "dispose" ? 23 : -100; }
            return unchecked(result + source.Trace * 37);
        }
        public static int CollectionTerminals(int a, int b)
        {
            var source = new DistinctCollection(b & 3); var query = source.Distinct(); int result = 0;
            try { result = a < 0 ? query.Count() : query.ToArray().Length; }
            catch (InvalidOperationException e) { result = e.Message == "count" ? 17 : e.Message == "current" ? 19 : e.Message == "dispose" ? 23 : -100; }
            return unchecked(result + source.Trace * 37 + source.Counts * 53);
        }
        public static int ListAny(int a, int b)
        {
            var source = new SelectorList(); var query = source.Distinct();
            if (!query.Any()) return -100;
            return unchecked(source.Reads + source.Trace * 37);
        }
        public static int ProjectedAny(int a, int b)
        {
            var source = new SelectorList(); var selector = new SelectorState(a, int.MinValue);
            var selected = source.Select(selector.Apply).Select(selector.Second); var query = selected.Distinct();
            if (!query.Any()) return -100;
            var e = selected.GetEnumerator(); if ((object)e != selected) return -101; e.Dispose();
            return unchecked(source.Reads + source.Trace * 37 + selector.Calls * 53 + selector.Trace);
        }
        public static int FilteredAny(int a, int b)
        {
            var predicate = new PredicateState(a, int.MinValue); var source = new PredicateSequence(0); var where = source.Where(predicate.Match);
            var query = where.Distinct().Distinct(); int result = query.Any() ? 7 : 11;
            var e = where.GetEnumerator(); if ((object)e != where) return -100; e.Dispose();
            return unchecked(result + source.Trace * 37 + predicate.Calls * 53);
        }
        public static int Projection(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var first = Values(a, b).Distinct(); var query = first.Select(state.Apply);
            var array = query.ToArray(); int result = array.Length; foreach (int value in array) result = unchecked(result + value);
            var e = first.GetEnumerator(); bool reused = (object)e == first; e.Dispose();
            return unchecked(result + state.Calls * 37 + (reused ? 53 : 71));
        }
        public static int Resume(int a, int b)
        {
            var trace = new KeyTrace(); var query = new[] { new CollisionKey(a, trace), new CollisionKey(b, trace) }.Distinct();
            var e = query.GetEnumerator(); trace.HashFailure = 1; int result = 0;
            try { e.MoveNext(); return -100; } catch (InvalidOperationException) { result = 7; }
            trace.HashFailure = 0; if (!e.MoveNext()) return -101; result = unchecked(result + e.Current.Number);
            trace.HashFailure = 1; try { e.MoveNext(); return -102; } catch (InvalidOperationException) { result += 11; }
            trace.HashFailure = 0; if (e.MoveNext()) return -103; e.Dispose();
            return unchecked(result + trace.Hashes * 37 + trace.Equalities * 53);
        }
        public static int ValuePipeline(int a, int b)
        {
            var trace = new KeyTrace(); var state = new DistinctSelector(trace, b);
            var values = new[] { new CollisionKey(a, trace), new CollisionKey(b, trace), new CollisionKey(unchecked(a + 3), trace) };
            var result = values.Where(state.Match).Select(state.Project).Distinct().ToArray(); int total = result.Length;
            foreach (var value in result) total = unchecked(total * 7 + value.Number);
            return unchecked(total + trace.Hashes * 37 + trace.Equalities * 53);
        }
        public static int EqualityFault(int a, int b)
        {
            var trace = new KeyTrace(); var values = new[] { new CollisionKey(a, trace), new CollisionKey(b, trace), new CollisionKey(a, trace) };
            var e = values.Distinct().GetEnumerator(); e.MoveNext(); int first = e.Current.Number; trace.EqualityFailure = 1; int result = 0;
            try { e.MoveNext(); return -100; } catch (InvalidOperationException ex) { if (ex.Message != "equality" || e.Current.Number != first) return -101; result = 7; }
            trace.EqualityFailure = 0; if (e.MoveNext()) result = unchecked(result + e.Current.Number);
            e.Dispose(); return unchecked(result + trace.Hashes * 37 + trace.Equalities * 53);
        }
        public static int ReferenceCurrent(int a, int b)
        {
            string value = string.Concat("o", "ne"); var values = new[] { value, null }; var e = values.Distinct().GetEnumerator();
            if (e.Current != null) return -100; e.MoveNext(); values[0] = string.Concat("t", "wo");
            if ((object)e.Current != value) return -101;
            int result = 0; try { ((IEnumerator)e).Reset(); return -102; } catch (NotSupportedException) { result = 7; }
            e.Dispose(); if (e.Current != null || e.MoveNext()) return -103; return result + value.Length;
        }
        public static int UnsupportedComparer(int a, int b) { return Values(a,b).Distinct((IEqualityComparer<int>)null).Count(); }
        public static int UnsupportedList(int a, int b) { return new List<int>().Distinct().Count(); }
    }
}
