using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class OrderingProbe
    {
        public sealed class Marker { public int Value; public Marker(int value) { Value = value; } }
        public sealed class Trace
        {
            public int Value; public int Calls; public int Fail;
            public Exception Fault;
            public Trace() { Fault = new FormatException("compare"); }
            public void Add(int value) { var marker = new Marker(value); Value = unchecked(Value * 31 + marker.Value); }
            public int Key(int value) { Add(value + 101); if (++Calls == Fail) throw Fault; return value % 5; }
            public int Next(int value) { Add(value + 201); return value % 3; }
            public int Project(int value) { Add(value + 301); return value + 1; }
        }
        public sealed class ObservedComparer : IComparer<int>
        {
            public Trace Trace; public int Mode;
            public ObservedComparer(Trace trace, int mode) { Trace = trace; Mode = mode; }
            public int Compare(int a, int b)
            {
                Trace.Add(unchecked(a * 17 + b));
                int call = ++Trace.Calls;
                if (call == Trace.Fail) throw Trace.Fault;
                if (Mode == 1) return int.MinValue;
                if (Mode == 2) throw new IndexOutOfRangeException("index");
                if (a == b) return 0; return a < b ? int.MinValue : int.MaxValue;
            }
        }
        private static int[] Values(int length, int seed)
        {
            int[] result = new int[length];
            for (int i = 0; i < length; i++) result[i] = unchecked((i * 37 + seed) % 23);
            return result;
        }
        private static int Hash(int[] items) { int sum = 19; foreach (int item in items) sum = unchecked(sum * 31 + item); return sum; }
        private static int Fold(IEnumerable<int> items) { int sum = 19; foreach (int item in items) sum = unchecked(sum * 31 + item); return sum; }
        public static int Stable(int a, int b)
        {
            int[] values = Values((a & 31) + 1, b);
            var query = a < 0 ? values.OrderByDescending(x => x % 5) : values.OrderBy(x => x % 5);
            return Hash(query.ToArray());
        }
        public static int Multiple(int a, int b)
        {
            int[] values = Values((a & 31) + 1, b);
            return Hash(values.OrderBy(x => x % 5).ThenByDescending(x => x % 3).ThenBy(x => x).ToArray());
        }
        public static int Repeated(int a, int b)
        { int[] values = Values((a & 15) + 1, b); return Hash(values.OrderByDescending(x => x % 5).OrderBy(x => x % 3).ToArray()); }
        public static int TraceSort(int a, int b)
        {
            var trace = new Trace(); int[] values = Values(a & 63, b);
            var query = a < 0 ? values.OrderByDescending(trace.Key, new ObservedComparer(trace, 0)) : values.OrderBy(trace.Key, new ObservedComparer(trace, 0));
            int hash = Hash(query.ToArray()); return unchecked(hash + trace.Value);
        }
        public static int TraceChain(int a, int b)
        {
            var trace = new Trace(); int[] values = Values(a & 31, b);
            int hash = Hash(values.OrderBy(trace.Key, new ObservedComparer(trace, 0)).ThenByDescending(trace.Next, new ObservedComparer(trace, 0)).ToArray());
            return unchecked(hash + trace.Value);
        }
        public static int Faults(int a, int b)
        {
            var trace = new Trace(); trace.Fail = (b & 15) + 1; int[] values = Values((a & 31) + 2, b);
            try { return unchecked(Hash(values.OrderBy(x => x, new ObservedComparer(trace, a < 0 ? 2 : 0)).ToArray()) + trace.Value); }
            catch (InvalidOperationException outer) { return unchecked(trace.Value + outer.Message.Length + (outer.InnerException == trace.Fault ? 307 : 0)); }
            catch (ArgumentException outer) { return unchecked(trace.Value + outer.Message.Length + (outer.InnerException == null ? 401 : 0)); }
        }
        public static int SelectorFault(int a, int b)
        {
            var trace = new Trace(); trace.Fail = (b & 7) + 1;
            try { return unchecked(Hash(Values(a & 15, b).OrderBy(trace.Key).ToArray()) + trace.Value); }
            catch (Exception e) { return unchecked(trace.Value + (e == trace.Fault ? 607 : 701)); }
        }
        public static int Cursor(int a, int b)
        {
            var trace = new Trace(); var query = Values(a & 7, b).OrderBy(trace.Key);
            var first = query.GetEnumerator(); var second = query.GetEnumerator();
            int sum = first == query ? 1 : 0; if (second == first) sum += 2;
            sum += first.Current;
            if (first.MoveNext()) { sum += first.Current; if (a < 0) first.Dispose(); }
            sum = unchecked(sum + Fold(query));
            while (second.MoveNext()) sum = unchecked(sum * 7 + second.Current);
            second.Dispose(); first.Dispose(); sum += first.Current;
            return unchecked(sum + trace.Value);
        }
        public sealed class Source : IEnumerable<int>, IEnumerator<int>
        {
            private readonly Trace trace; private readonly int length; private int index;
            public Source(Trace trace, int length) { this.trace = trace; this.length = length; }
            private void Hit(int code) { trace.Add(code); if (trace.Fail == code) throw trace.Fault; }
            public IEnumerator<int> GetEnumerator() { Hit(11); index = 0; return this; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public bool MoveNext() { Hit(13); return ++index <= length; }
            public int Current { get { Hit(17); return (length - index) % 3; } }
            object IEnumerator.Current => Current;
            public void Dispose() { Hit(19); }
            public void Reset() { throw new NotSupportedException(); }
        }
        public static int Terminals(int a, int b)
        {
            var trace = new Trace(); var source = new Source(trace, a & 7);
            var query = b < 0 ? source.OrderByDescending(trace.Key, new ObservedComparer(trace, 0)) : source.OrderBy(trace.Key, new ObservedComparer(trace, 0));
            int result = (b & 3) == 0 ? query.Count() : (b & 3) == 1 ? (query.Any() ? 1 : 0)
                : (b & 3) == 2 ? (query.Select(trace.Project).Any() ? 1 : 0) : Hash(query.Select(trace.Project).ToArray());
            return unchecked(result + trace.Value);
        }
        public static int ChainAny(int a, int b)
        {
            var trace = new Trace(); var query = new Source(trace, a & 7).OrderBy(trace.Key, new ObservedComparer(trace, 0)).ThenByDescending(trace.Next, new ObservedComparer(trace, 0));
            bool result = b < 0 ? query.Any() : query.Select(trace.Project).Any(); return unchecked((result ? 1 : 0) + trace.Value);
        }
        public struct Item { public int Number; public string Text; public Item(int n, string text) { Number = n; Text = text; } }
        public static int Inline(int a, int b)
        {
            Item[] values = new Item[3]; values[0] = new Item(a, "first"); values[1] = new Item(b, "second"); values[2] = new Item(a, "third");
            var result = values.OrderBy(x => x.Number).ThenBy(x => x.Text.Length).ToArray();
            return unchecked(result[0].Number * 17 + result[0].Text.Length * 31 + result[1].Text.Length * 7 + result[2].Text.Length);
        }
        public sealed class CustomOrder : IOrderedEnumerable<int>
        {
            public Trace Trace;
            public CustomOrder(Trace trace) { Trace = trace; }
            public IOrderedEnumerable<int> CreateOrderedEnumerable<TKey>(Func<int, TKey> selector, IComparer<TKey> comparer, bool descending)
            { Trace.Add((selector == null ? 3 : 5) + (comparer == null ? 7 : 11) + (descending ? 13 : 17)); return this; }
            public IEnumerator<int> GetEnumerator() => new Source(Trace, 0);
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public static int CustomThen(int a, int b)
        {
            var trace = new Trace(); var source = new CustomOrder(trace);
            Func<int, int> selector = a < 0 ? null! : (Func<int, int>)(x => x);
            var result = b < 0 ? source.ThenByDescending(selector) : source.ThenBy(selector, new ObservedComparer(trace, 0));
            return trace.Value + (result == source ? 19 : 23);
        }
        public static int Validation(int a, int b)
        {
            IEnumerable<int> source = a < 0 ? null! : Values(0, 0); Func<int, int> selector = b < 0 ? null! : (Func<int, int>)(x => x);
            try { var query = source.OrderBy(selector); return query == null ? 1 : 2; }
            catch (ArgumentNullException e) { return e.ParamName.Length; }
        }
        public static int Inconsistent(int a, int b)
        {
            var trace = new Trace();
            try { return unchecked(Hash(Values((a & 63) + 17, b).OrderBy(x => x, new ObservedComparer(trace, 1)).ToArray()) + trace.Value); }
            catch (ArgumentException e) { return unchecked(e.Message.Length + trace.Value); }
        }
        public static int SourceFault(int a, int b)
        {
            var trace = new Trace(); int mode = b & 3; trace.Fail = mode == 0 ? 11 : mode == 1 ? 13 : mode == 2 ? 17 : 19;
            try { return unchecked(Hash(new Source(trace, a & 7).OrderBy(x => x).ToArray()) + trace.Value); }
            catch (Exception e) { return unchecked(trace.Value + (e == trace.Fault ? 809 : 907)); }
        }
        public static int FaultCodeUnit(int a, int b)
        {
            var trace = new Trace(); trace.Fail = a < 0 ? 0 : 1;
            try { return Values(3, 0).OrderBy(x => x, new ObservedComparer(trace, a < 0 ? 2 : 0)).ToArray().Length; }
            catch (Exception e) { string text = e.Message; return b >= 0 && b < text.Length ? text[b] : text.Length; }
        }
        public sealed class KeySelector { public int Key(ValueVectorBox item) => item.Number; }
        public sealed class PlainComparer : IComparer<int> { public int Compare(int a, int b) => a.CompareTo(b); }
        public static IEnumerator<ValueVectorBox> MakeTracked()
        {
            var selector = new KeySelector(); var values = new[] { new ValueVectorBox(17), new ValueVectorBox(23) };
            var e = values.OrderBy(selector.Key, new PlainComparer()).GetEnumerator(); e.MoveNext(); values[0] = new ValueVectorBox(31); return e;
        }
        public static void StopTracked(IEnumerator<ValueVectorBox> e) { e.Dispose(); }
        public static int Tracked(int a, int b) { var e = MakeTracked(); StopTracked(e); return a; }
        public static int Adversarial(int a, int b)
        {
            int length = a < 0 ? 1024 : 512; int[] values = new int[length];
            for (int i = 0; i < length; i++) values[i] = i < length / 2 ? i : length - i;
            var trace = new Trace(); int result = Hash(values.OrderBy(x => x, new ObservedComparer(trace, 0)).ToArray());
            return unchecked(result + trace.Value);
        }
        public static int UnsupportedWide(int a, int b) => Values(3, a).OrderBy(x => (long)x).Count();
        public static int UnsupportedUnsigned(int a, int b) => Values(3, a).OrderBy(x => (uint)x).Count();
        public static int UnsupportedString(int a, int b) => new[] { "a", "b" }.OrderBy(x => x, StringComparer.Ordinal).Count();
    }
}
