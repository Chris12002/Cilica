using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ArrayEnumerationProbe
    {
        public static int Integers(int a, int b)
        {
            IEnumerable<int> values = new[] { a, b }; int result = 0;
            foreach (int value in values) result = unchecked(result + value);
            return result;
        }
        public static int States(int a, int b)
        {
            var values = new[] { a, b }; var e = ((IEnumerable<int>)values).GetEnumerator(); int result = 0;
            try { return e.Current; } catch (InvalidOperationException ex) { result += ex.Message == "Enumeration has not started. Call MoveNext." ? 1 : -100; }
            e.MoveNext(); values[0] = b; result = unchecked(result + e.Current);
            e.Dispose(); if (!e.MoveNext()) return -101;
            result = unchecked(result + e.Current); if (e.MoveNext() || e.MoveNext()) return -102;
            try { return e.Current; } catch (InvalidOperationException ex) { result += ex.Message == "Enumeration already finished." ? 3 : -100; }
            ((IEnumerator)e).Reset(); if (!e.MoveNext()) return -103;
            return unchecked(result + e.Current);
        }
        public static int Empty(int a, int b)
        {
            var first = ((IEnumerable<int>)new int[0]).GetEnumerator(); ((IEnumerator)first).Reset();
            var second = ((IEnumerable<int>)new int[0]).GetEnumerator();
            var different = ((IEnumerable<string>)new string[0]).GetEnumerator();
            if (first != second || (object)first == different) return -100;
            int result = 0;
            try { return first.Current; } catch (InvalidOperationException ex) { result += ex.Message == "Enumeration has not started. Call MoveNext." ? 1 : -100; }
            if (second.MoveNext()) return -101;
            var third = ((IEnumerable<int>)new int[0]).GetEnumerator();
            try { return third.Current; } catch (InvalidOperationException ex) { result += ex.Message == "Enumeration already finished." ? 3 : -100; }
            first.Dispose(); ((IEnumerator)second).Reset();
            try { return third.Current; } catch (InvalidOperationException ex) { result += ex.Message == "Enumeration has not started. Call MoveNext." ? 7 : -100; }
            return result;
        }
        public static int Reference(int a, int b)
        {
            var values = new[] { new ValueVectorBox(a), null }; var e = ((IEnumerable<ValueVectorBox>)values).GetEnumerator();
            e.MoveNext(); var old = e.Current; values[0] = new ValueVectorBox(b);
            int result = unchecked(old.Number + e.Current.Number); e.MoveNext();
            return e.Current == null ? result : -100;
        }
        public static int Value(int a, int b)
        {
            ValueVectorCell cell = default; cell.Number = a; cell.Reference = new ValueVectorBox(b);
            var values = new[] { cell }; var e = ((IEnumerable<ValueVectorCell>)values).GetEnumerator();
            e.MoveNext(); var copy = e.Current; copy.Number = b;
            cell.Number = b; cell.Reference = new ValueVectorBox(a); values[0] = cell;
            return unchecked(copy.Number + copy.Reference.Number + e.Current.Number + e.Current.Reference.Number);
        }
        public static int Covariance(int a, int b)
        {
            var values = new[] { "a", "bc" }; var wide = ((IEnumerable<object>)values).GetEnumerator();
            if (!wide.MoveNext()) return -100;
            values[0] = "def";
            var emptyWide = ((IEnumerable<object>)new string[0]).GetEnumerator();
            var emptyObject = ((IEnumerable<object>)new object[0]).GetEnumerator();
            return emptyWide == emptyObject ? ((string)wide.Current).Length : -102;
        }
        public static int VariantClosure(int a, int b)
        {
            var values = new[] { "a" };
            var wide = ((IEnumerable<object>)values).GetEnumerator();
            var narrow = ((IEnumerable<string>)values).GetEnumerator();
            wide.MoveNext(); narrow.MoveNext(); return wide.Current == (object)narrow.Current ? a : b;
        }
        public static int FreshAndNull(int a, int b)
        {
            var values = new[] { a }; var first = ((IEnumerable<int>)values).GetEnumerator(); var second = ((IEnumerable<int>)values).GetEnumerator();
            if (first == second) return -100; first.MoveNext(); int result = first.Current;
            try { result = second.Current; } catch (InvalidOperationException) { result = unchecked(result + b); }
            IEnumerable<int> missing = null;
            try { missing.GetEnumerator(); return -101; } catch (NullReferenceException) { return result; }
        }
        public static int Cache(int a, int b)
        {
            var first = ((IEnumerable<int>)new int[0]).GetEnumerator();
            var second = ((IEnumerable<int>)new int[0]).GetEnumerator();
            var different = ((IEnumerable<string>)new string[0]).GetEnumerator();
            return first == second && (object)first != different ? 11 : -100;
        }
        public static IEnumerator<ValueVectorBox> CreateTracked() { return ((IEnumerable<ValueVectorBox>)new[] { new ValueVectorBox(17) }).GetEnumerator(); }
        public static void EndTracked(IEnumerator<ValueVectorBox> e) { e.MoveNext(); e.MoveNext(); e.Dispose(); }
        public static int Tracked(int a, int b) { var e = CreateTracked(); EndTracked(e); return a; }
        public static int UnsupportedAlias(int a, int b) { return ((IEnumerable<uint>)(object)new[] { a }).GetEnumerator().MoveNext() ? b : a; }
        public static int LegacyCurrent(int a, int b) { var e = ((IEnumerable<int>)new[] { a }).GetEnumerator(); e.MoveNext(); return (int)((IEnumerator)e).Current; }
    }
}
