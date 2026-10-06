using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class YieldState
    {
        public int Trace;
        public int Mode;
        public YieldState(int mode) { Mode = mode; }
        public IEnumerable<int> Values(int value)
        {
            Trace = unchecked(Trace * 7 + 1);
            try
            {
                yield return value;
                Trace = unchecked(Trace * 7 + 2);
                if (Mode == 1) throw new InvalidOperationException("body");
                yield return unchecked(value + 1);
            }
            finally
            {
                Trace = unchecked(Trace * 7 + 3);
                if (Mode == 2) throw new InvalidOperationException("finally");
            }
            Trace = unchecked(Trace * 7 + 4);
        }
    }
    public static class YieldIteratorProbe
    {
        private static IEnumerable<int> Pair(int a, int b) { yield return a; yield return b; }
        private static IEnumerable<ValueVectorBox> ReferenceValues(int a, int b) { yield return new ValueVectorBox(a); yield return null; yield return new ValueVectorBox(b); }
        private static IEnumerable<ValueVectorCell> ValueValues(int a, int b)
        {
            ValueVectorCell cell = default; cell.Number = a; cell.Reference = new ValueVectorBox(b); yield return cell;
            cell.Number = b; cell.Reference = new ValueVectorBox(a); yield return cell;
        }
        public static int ThreadIdentity(int a, int b)
        {
            int first = Environment.CurrentManagedThreadId; var value = new ValueVectorBox(a);
            return first > 0 && first == Environment.CurrentManagedThreadId ? value.Number : b;
        }
        public static int Integers(int a, int b) { int result = 0; foreach (var value in Pair(a, b)) result = unchecked(result + value); return result; }
        public static int Reuse(int a, int b)
        {
            var values = Pair(a, b); var first = values.GetEnumerator(); var second = values.GetEnumerator();
            if ((object)values != first || first == second) return -100;
            if (!first.MoveNext() || !second.MoveNext()) return -101;
            int result = unchecked(first.Current + second.Current);
            if (!first.MoveNext()) return -102;
            result = unchecked(result + first.Current + second.Current);
            first.Dispose(); second.Dispose(); var third = values.GetEnumerator();
            if (third == first || third == second || !third.MoveNext()) return -103;
            return unchecked(result + third.Current);
        }
        public static int CurrentAndReset(int a, int b)
        {
            var values = Pair(a, b); var e = values.GetEnumerator(); int result = e.Current;
            e.MoveNext(); result = unchecked(result + e.Current); e.MoveNext();
            if (e.MoveNext() || e.MoveNext()) return -100;
            result = unchecked(result + e.Current); e.Dispose(); result = unchecked(result + e.Current);
            try { ((IEnumerator)e).Reset(); return -101; } catch (NotSupportedException ex) { return unchecked(result + ex.Message.Length); }
        }
        public static int Disposal(int a, int b)
        {
            var state = new YieldState(0); var values = state.Values(a);
            if (state.Trace != 0) return -100;
            var e = values.GetEnumerator(); if (state.Trace != 0 || !e.MoveNext()) return -101;
            int result = e.Current; e.Dispose(); e.Dispose();
            return unchecked(result + state.Trace * 37);
        }
        public static int Finally(int a, int b)
        {
            var state = new YieldState(b & 3); int result = 0;
            try { foreach (int value in state.Values(a)) { result = unchecked(result + value); if (a < 0) break; } }
            catch (InvalidOperationException ex) { result = unchecked(result + (ex.Message == "body" ? 17 : ex.Message == "finally" ? 23 : -100)); }
            return unchecked(result + state.Trace * 37);
        }
        public static int References(int a, int b)
        {
            var e = ReferenceValues(a, b).GetEnumerator(); e.MoveNext(); var old = e.Current;
            e.MoveNext(); if (e.Current != null) return -100;
            e.MoveNext(); var current = e.Current; if (e.MoveNext()) return -101; e.Dispose();
            return unchecked(old.Number + current.Number + e.Current.Number);
        }
        public static int Values(int a, int b)
        {
            var e = ValueValues(a, b).GetEnumerator(); e.MoveNext(); var copy = e.Current;
            e.MoveNext(); var current = e.Current; e.Dispose();
            return unchecked(copy.Number + copy.Reference.Number + current.Number + current.Reference.Number);
        }
        public static IEnumerable<ValueVectorBox> MakeTracked() { return ReferenceValues(17, 23); }
        public static IEnumerator<ValueVectorBox> BindTracked(IEnumerable<ValueVectorBox> values) { return values.GetEnumerator(); }
        public static void MoveTracked(IEnumerator<ValueVectorBox> values) { values.MoveNext(); }
        public static void StopTracked(IEnumerator<ValueVectorBox> values) { values.Dispose(); }
        public static int Tracked(int a, int b) { var e = BindTracked(MakeTracked()); MoveTracked(e); StopTracked(e); return a; }
        public static int UnsupportedEnvironment(int a, int b) { return Environment.ProcessorCount; }
        public static int UnsupportedThread(int a, int b) { return System.Threading.Thread.CurrentThread.ManagedThreadId; }
    }
}
