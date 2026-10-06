using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class LinqWhereProbe
    {
        private static int[] Three() { var a = new int[3]; a[0] = 1; a[1] = 2; a[2] = 3; return a; }
        public static int Deferred(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); var source = Three(); var query = source.Where(state.Match);
            if (state.Calls != 0) return -100; var e = query.GetEnumerator();
            if (state.Calls != 0 || (object)e != query) return -101;
            source[0] = b; int result = 0; while (e.MoveNext()) result = unchecked(result + e.Current);
            e.Dispose(); return unchecked(result + state.Calls * 37);
        }
        public static int Clones(int a, int b)
        {
            var state = new PredicateState(0, int.MinValue); var query = new[] { a, b }.Where(state.Match);
            var first = query.GetEnumerator(); var second = query.GetEnumerator();
            if ((object)query != first || first == second) return -100;
            int result = first.Current + second.Current;
            if (first.MoveNext()) result = unchecked(result + first.Current);
            if (second.MoveNext()) result = unchecked(result + second.Current);
            first.Dispose(); result = unchecked(result + first.Current); second.Dispose();
            var third = query.GetEnumerator(); if (third == first || third == second) return -101;
            while (third.MoveNext()) result = unchecked(result + third.Current);
            return unchecked(result + state.Calls * 37);
        }
        public static int Cardinality(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); var query = Three().Where(state.Match);
            var e = query.GetEnumerator(); bool moved = e.MoveNext(); int before = e.Current;
            int result = query.Count() + (query.Any() ? 17 : 0);
            if (e.Current != before) return -100;
            if (e.MoveNext()) result = unchecked(result + e.Current);
            e.Dispose(); return unchecked(result + state.Calls * 37 + (moved ? 1 : 0));
        }
        public static int Combined(int a, int b)
        {
            var first = new PredicateState(a, int.MinValue); var second = new PredicateState(b, int.MinValue);
            var query = Three().Where(first.Match).Where(second.Match);
            int result = query.Count() + (query.Any() ? 11 : 0);
            foreach (int value in query) result = unchecked(result + value);
            return unchecked(result + first.Calls * 37 + second.Calls * 53);
        }
        public static int Empty(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); var original = new int[0]; var query = original.Where(state.Match);
            if ((object)query != Array.Empty<int>() || (object)query == original) return -100;
            return query.Count() + (query.Any() ? -100 : 7) + state.Calls;
        }
        public static int Fallback(int a, int b)
        {
            var source = new PredicateSequence(0); var state = new PredicateState(a, int.MinValue);
            var query = source.Where(state.Match); if (source.Trace != 0 || state.Calls != 0) return -100;
            int result = query.Count() + (query.Any() ? 11 : 0);
            foreach (int value in query) { result = unchecked(result + value); if (b < 0) break; }
            return unchecked(result + source.Trace * 37 + state.Calls * 53);
        }
        public static int Faults(int a, int b)
        {
            var source = new PredicateSequence(b & 3); var state = new PredicateState(a, 2); int result = 0;
            var query = source.Where(state.Match);
            try { foreach (int value in query) result = unchecked(result + value); }
            catch (InvalidOperationException e) { result = e.Message == "predicate" ? 17 : e.Message == "current" ? 19 : e.Message == "dispose" ? 23 : -100; }
            return unchecked(result + source.Trace * 37 + state.Calls * 53);
        }
        public static int ReferenceCurrent(int a, int b)
        {
            var state = new PredicateState(int.MinValue, 0); var values = new[] { new ValueVectorBox(a), new ValueVectorBox(b) };
            var e = values.Where(state.Reference).GetEnumerator();
            if (e.Current != null) return -100; e.MoveNext(); var saved = e.Current;
            values[0] = new ValueVectorBox(b); if (e.Current != saved) return -101;
            e.Dispose(); if (e.Current != null || e.MoveNext()) return -102;
            return unchecked(saved.Number + state.Calls);
        }
        public static int FaultResume(int a, int b)
        {
            var state = new PredicateState(0, 2); var e = Three().Where(state.Match).GetEnumerator();
            if (!e.MoveNext() || e.Current != 1) return -100;
            try { e.MoveNext(); return -101; } catch (InvalidOperationException ex) { if (ex.Message != "predicate" || e.Current != 1) return -102; }
            if (!e.MoveNext() || e.Current != 3 || e.MoveNext() || e.Current != 0) return -103;
            return state.Calls;
        }
        public static IEnumerator<ValueVectorBox> MakeTracked()
        {
            var state = new PredicateState(int.MinValue, 0); var values = new[] { new ValueVectorBox(17) };
            var e = values.Where(state.Reference).GetEnumerator(); e.MoveNext(); values[0] = new ValueVectorBox(23); return e;
        }
        public static void StopTracked(IEnumerator<ValueVectorBox> e) { e.Dispose(); }
        public static int Tracked(int a, int b) { var e = MakeTracked(); StopTracked(e); return a; }
        public static int ResetAndNull(int a, int b)
        {
            var state = new PredicateState(a, 0); IEnumerable<int> source = a < 0 ? null : Three(); Func<int, bool> missing = null; int result = 0;
            try { source.Where(missing); } catch (ArgumentNullException e) { result = e.ParamName == "source" ? 3 : e.ParamName == "predicate" ? 7 : -100; }
            var e2 = Three().Where(state.Match).GetEnumerator();
            try { ((IEnumerator)e2).Reset(); return -101; } catch (NotSupportedException e) { result += e.Message.Length; }
            return result;
        }
    }
}
