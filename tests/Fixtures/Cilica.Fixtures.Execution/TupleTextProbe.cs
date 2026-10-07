using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class TupleTextHolder { public (TupleTextValue,TupleTextValue) Pair; }
    public sealed class TupleTextTrace { public int Value; public TupleTextHolder Owner; }
    public struct TupleTextValue
    {
        public int Value, Mode, Tag; public TupleTextTrace Trace;
        public override string ToString()
        {
            Value = unchecked(Value + 1); Trace.Value = unchecked(Trace.Value * 5 + Tag);
            var pressure = new int[3]; pressure[0] = Mode;
            if (pressure[0] == 4 && Trace.Owner != null) Trace.Owner.Pair.Item2.Value = unchecked(Trace.Owner.Pair.Item2.Value + 31);
            if (pressure[0] == 1) throw new InvalidOperationException("tuple text");
            return pressure[0] == 2 ? null : "item\0\ud800";
        }
    }
    public static class TupleTextProbe
    {
        private static int Observe(string text) { int h = 17; for (int i = 0; i < text.Length; ++i) h = unchecked(h * 31 + text[i]); return h; }
        public static int Signed(int a, int b) => Observe((a,b).ToString());
        public static int Unsigned(int a, int b) => Observe(((uint)a, ((ulong)(uint)a << 32) | (uint)b).ToString());
        public static int WideStrings(int a, int b) => Observe((((long)a << 32) | (uint)b, b < 0 ? null : "A\0\ud800").ToString());
        public static int Dictionary(int a, int b)
        {
            string text = b < 0 ? null : "node"; var key = (((long)a << 32) | (uint)b, text); var d = new Dictionary<(long,string),int>(); d.Add(key,b); int result = d[key];
            try { d.Add(key,a); } catch (ArgumentException error) { result = unchecked(result + Observe(error.Message)); }
            try { return unchecked(result + d[(~key.Item1,key.Item2)]); } catch (KeyNotFoundException error) { return unchecked(result + Observe(error.Message) + d.Count); }
        }
        public static int MutableDirect(int a, int b)
        {
            var pair = (new FormattingValue { Text = "left", Calls = a & 1 }, new FormattingValue { Text = null, Calls = b & 1 });
            int result = Observe(pair.ToString()); result = unchecked(result + Observe(pair.ToString()));
            return unchecked(result + pair.Item1.Calls * 7 + pair.Item2.Calls * 11);
        }
        public static int MutableBoxed(int a, int b)
        {
            var pair = (new FormattingValue { Text = "left", Calls = a & 1 }, new FormattingValue { Text = "right", Calls = b & 1 });
            int result = Observe(string.Format("{0}|{0}", pair));
            return unchecked(result + pair.Item1.Calls * 7 + pair.Item2.Calls * 11);
        }
        public static int Nested(int a, int b)
        {
            var pair = ((a, new FormattingValue { Text = "nested" }), ((long)b, "tail"));
            int result = Observe(pair.ToString()); return unchecked(result + pair.Item1.Item2.Calls * 7);
        }
        public static int Custom(int a, int b) => Observe((new CustomFormattingValue(), a).ToString());
        public static int Order(int a, int b)
        {
            var trace = new TupleTextTrace();
            var pair = (new TupleTextValue { Value = a, Mode = b & 3, Tag = 1, Trace = trace }, new TupleTextValue { Value = b, Mode = (b >> 2) & 3, Tag = 2, Trace = trace });
            int result; try { result = Observe(pair.ToString()); } catch (InvalidOperationException error) { result = Observe(error.Message); }
            return unchecked(result + trace.Value * 7 + pair.Item1.Value * 11 + pair.Item2.Value * 13);
        }
        public static int HeapMutation(int a, int b)
        {
            var trace = new TupleTextTrace(); var owner = new TupleTextHolder(); trace.Owner = owner;
            owner.Pair = (new TupleTextValue { Value = a, Mode = 4, Tag = 1, Trace = trace }, new TupleTextValue { Value = b, Mode = b & 3, Tag = 2, Trace = trace });
            int result; try { result = Observe(owner.Pair.ToString()); } catch (InvalidOperationException error) { result = Observe(error.Message); }
            return unchecked(result + trace.Value * 7 + owner.Pair.Item1.Value * 11 + owner.Pair.Item2.Value * 13);
        }
        public static int UnsupportedNullable(int a, int b) => ((int?)a,b).ToString().Length;
        public static int UnsupportedEnum(int a, int b) => ((TextEnum)(a & 7),b).ToString().Length;
        public static int UnsupportedReference(int a, int b) => (new PlainReferenceKey(),b).ToString().Length;
        public static int UnsupportedCharacter(int a, int b) => ((char)a,b).ToString().Length;
        public static int UnsupportedOne(int a, int b) => new ValueTuple<int>(a).ToString().Length;
        public static int UnsupportedFive(int a, int b) => (a,b,a,b,a).ToString().Length;
    }
}
