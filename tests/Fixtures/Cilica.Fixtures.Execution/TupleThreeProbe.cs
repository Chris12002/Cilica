using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class TupleThreeHolder { public (int, long, TupleFiveCell) Value; }
    public static class TupleThreeProbe
    {
        public static int Fields(int a, int b)
        { var t = (a, b, unchecked(a + 7)); return unchecked(t.Item1 + t.Item2 * 2 + t.Item3 * 3); }
        public static int Copies(int a, int b)
        { var t = (a,b,a); var copy = t; copy.Item1 = b; copy.Item3 = unchecked(b + 1); return unchecked(t.Item1 + t.Item3 * 3 + copy.Item1 * 5 + copy.Item3 * 7); }
        private static (int, long, TupleFiveCell) Make(int a, int b) => (a, ((long)a << 32) | (uint)b, new TupleFiveCell(b));
        private static int Read((int, long, TupleFiveCell) t) => unchecked(t.Item1 + (int)(t.Item2 >> 32) * 3 + (int)t.Item2 * 5 + t.Item3.Value * 7);
        public static int Returns(int a, int b) { var t = Make(a,b); return Read(t); }
        public static int Heap(int a, int b)
        { var h = new TupleThreeHolder(); var t = Make(a,b); h.Value = t; var copy = h.Value; h.Value.Item1 = b; copy.Item3.Value = a; return unchecked(Read(copy) + Read(h.Value)); }
        public static int Arrays(int a, int b)
        { var items = new (int, long, TupleFiveCell)[2]; var t = Make(a,b); items[0] = t; items[1] = items[0]; items[1].Item1 = b; return unchecked(Read(items[0]) + Read(items[1])); }
        public static int Boxing(int a, int b)
        { var t = Make(a,b); object box = t; t.Item1 = b; var copy = ((int, long, TupleFiveCell))box; return unchecked(Read(t) + Read(copy)); }
        public static int Nullable(int a, int b)
        { (int,long,TupleFiveCell)? t = a < b ? ((int,long,TupleFiveCell)?)null : Make(a,b); return t.HasValue ? Read(t.Value) : b; }
        public static int Equality(int a, int b)
        { var left = (a, b, a); var right = (a, b, b); return left.Equals(right) ? 17 : 19; }
        public static int TextEquality(int a, int b)
        { string first = a < 0 ? null : a == 0 ? "" : "A\0\ud800"; string second = b < 0 ? null : b == 0 ? "" : string.Concat("A", "\0\ud800"); return (first,a,first).Equals((second,a,second)) ? 23 : 29; }
        public static int ComponentEffects(int a, int b)
        {
            var trace = new TupleFiveCell(0);
            var first = new TupleFiveComponent(a, 1, trace);
            var second = new TupleFiveComponent(a, 2, trace);
            var third = new TupleFiveComponent(a, 3, trace);
            var left = (first,second,third);
            int stage = (b & 3); int changed = unchecked(a + 1);
            int v1 = stage == 1 ? changed : a, v2 = stage == 2 ? changed : a;
            int v3 = stage == 3 ? changed : a;
            int tag = b < 0 ? -3 : 3;
            var right = (new TupleFiveComponent(v1, 1, trace), new TupleFiveComponent(v2, 2, trace), new TupleFiveComponent(v3, tag, trace));
            int result;
            try { result = left.Equals(right) ? 31 : 37; } catch (InvalidOperationException) { result = 41; }
            return unchecked(trace.Value * 43 + result + left.Item1.Value + left.Item2.Value + left.Item3.Value);
        }
        public static int UnsupportedConditionalConstruction(int a, int b)
        {
            var trace = new TupleFiveCell(0);
            var right = (new TupleFiveComponent(a < b ? a : b, 1, trace), a, b);
            return right.Item1.Value;
        }
        public static int UnsupportedHash(int a, int b) => (a,b,a).GetHashCode();
        public static int UnsupportedComparison(int a, int b) => (a,b,a).CompareTo((b,a,b));
        public static int UnsupportedText(int a, int b) => (a,b,a).ToString().Length;
        public static int UnsupportedObjectEquals(int a, int b) => (a,b,a).Equals((object)(b,a,b)) ? 1 : 0;
    }
}
