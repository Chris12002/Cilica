using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class ObjectArrayEnumerationProbe
    {
        public static int Integers(int a, int b)
        {
            var values = new[] { a, b }; var e = ((IEnumerable)values).GetEnumerator(); e.MoveNext(); object first = e.Current, second = e.Current;
            if (first == second) return -100; values[0] = b; int result = unchecked((int)first + (int)e.Current);
            e.MoveNext(); result = unchecked(result + (int)e.Current); if (e.MoveNext()) return -101;
            return result;
        }
        public static int States(int a, int b)
        {
            var e = ((IEnumerable)new[] { a }).GetEnumerator(); int result = 0;
            try { object ignored = e.Current; return -100; } catch (InvalidOperationException ex) { result = ex.Message == "Enumeration has not started. Call MoveNext." ? 7 : -101; }
            e.MoveNext(); result = unchecked(result + (int)e.Current); e.MoveNext();
            try { object ignored = e.Current; return -102; } catch (InvalidOperationException ex) { result += ex.Message == "Enumeration already finished." ? 11 : -103; }
            if (e.MoveNext()) return -104; e.Reset(); e.MoveNext();
            return unchecked(result + (int)e.Current + (e is IDisposable ? -100 : 13));
        }
        public static int Empty(int a, int b)
        {
            IEnumerable source = new int[0]; var e = source.GetEnumerator(); var other = source.GetEnumerator();
            if (e == other || e.MoveNext() || other.MoveNext()) return -100;
            e.Reset(); int result = 0;
            try { object ignored = e.Current; } catch (InvalidOperationException ex) { result = ex.Message == "Enumeration has not started. Call MoveNext." ? 7 : -101; }
            return result;
        }
        public static int References(int a, int b)
        {
            var first = new ValueVectorBox(a); var values = new[] { first, null }; var e = ((IEnumerable)values).GetEnumerator(); e.MoveNext(); var saved = (ValueVectorBox)e.Current;
            values[0] = new ValueVectorBox(b); if (saved != first || e.Current != values[0]) return -100;
            e.MoveNext(); if (e.Current != null) return -101; return saved.Number;
        }
        public static int Values(int a, int b)
        {
            ValueVectorCell value = default; value.Number = a; value.Wide = ((long)a << 32) + b; value.Reference = new ValueVectorBox(b);
            var values = new[] { value }; var e = ((IEnumerable)values).GetEnumerator(); e.MoveNext(); object first = e.Current, second = e.Current;
            if (first == second) return -100;
            var copy = (ValueVectorCell)first; values[0].Number = b; var later = (ValueVectorCell)e.Current;
            return unchecked(copy.Number + copy.Reference.Number + (int)(copy.Wide >> 32) + later.Number);
        }
        public static int Nullable(int a, int b)
        {
            var values = new int?[2]; values[0] = a; var e = ((IEnumerable)values).GetEnumerator(); e.MoveNext(); int result = (int)e.Current;
            e.MoveNext(); if (e.Current != null) return -100; return result;
        }
        public static int Covariance(int a, int b)
        {
            object[] values = new string[] { "a", null }; var e = ((IEnumerable)values).GetEnumerator(); e.MoveNext(); string first = (string)e.Current;
            values[0] = "abc"; int result = first.Length + ((string)e.Current).Length; e.MoveNext(); return e.Current == null ? result : -100;
        }
        public static int Legacy(int a, int b) { return ArrayEnumerationProbe.LegacyCurrent(a,b); }
        public static int SelectCurrent(int a, int b)
        {
            var state = new SelectorState(a, 0); var e = new[] { a, b }.Select(state.Nullable).GetEnumerator(); IEnumerator raw = e;
            if (raw.Current != null) return -100; e.MoveNext(); if (raw.Current != null) return -101;
            e.MoveNext(); int result = raw.Current == null ? 7 : (int)raw.Current;
            e.Dispose(); return raw.Current == null ? result : -102;
        }
        public static int DistinctCurrent(int a, int b)
        {
            var trace = new KeyTrace(); var e = new[] { new CollisionKey(a, trace) }.Distinct().GetEnumerator(); IEnumerator raw = e;
            var initial = (CollisionKey)raw.Current; if (initial.Number != 0 || initial.Trace != null) return -100;
            e.MoveNext(); object first = raw.Current, second = raw.Current; if (first == second) return -101;
            var value = (CollisionKey)first; e.Dispose(); var cleared = (CollisionKey)raw.Current;
            if (cleared.Trace != null || cleared.Number != 0) return -102;
            return unchecked(value.Number + trace.Hashes * 37);
        }
        public static int OfTypeCurrent(int a, int b)
        {
            var source = new object[] { a, null }; var e = source.OfType<int>().GetEnumerator(); IEnumerator raw = e;
            if ((int)raw.Current != 0) return -100; e.MoveNext(); object first = raw.Current, second = raw.Current;
            if (first == second) return -101; int result = (int)first; e.Dispose(); return unchecked(result + (int)raw.Current);
        }
        public static int QueryCurrent(int a, int b)
        {
            var state = new PredicateState(int.MinValue, int.MinValue); var e = new[] { a == int.MinValue ? 5 : a, b }.Where(state.Match).GetEnumerator(); IEnumerator raw = e;
            int result = (int)raw.Current; if (e.MoveNext()) result = unchecked(result + (int)raw.Current);
            e.Dispose(); return unchecked(result + (int)raw.Current);
        }
    }
}
