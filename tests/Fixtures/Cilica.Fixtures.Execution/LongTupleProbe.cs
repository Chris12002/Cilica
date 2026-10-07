using System;
using System.Runtime.CompilerServices;
using LongTuple = System.ValueTuple<int, long, Cilica.Fixtures.Execution.TupleFiveCell, bool, int, string, int, System.ValueTuple<Cilica.Fixtures.Execution.TupleFiveCell>>;
namespace Cilica.Fixtures.Execution
{
    public sealed class LongTupleHolder { public LongTuple Value; }
    public struct PretendTuple : ITuple { public int Length => 1; public object this[int index] => index; }
    public static class LongTupleProbe
    {
        private static int Observe(string text) { int result=17; for(int i=0;i<text.Length;i++) result=unchecked(result*31+text[i]); return result; }
        private static LongTuple Make(int a, int b)
        {
            var cell = new TupleFiveCell(b); string text = a < 0 ? null : "tuple";
            return new LongTuple(a, ((long)a << 32) | (uint)b, cell, a < b, b, text, a, new ValueTuple<TupleFiveCell>(cell));
        }
        private static int Read(LongTuple t) => unchecked(t.Item1 + (int)(t.Item2 >> 32) * 3 + (int)t.Item2 * 5 + t.Item3.Value * 7
            + (t.Item4 ? 11 : 13) + t.Item5 * 17 + (t.Item6 == null ? 19 : 23) + t.Item7 * 29 + t.Rest.Item1.Value * 31);
        public static int Single(int a, int b) { var t = new ValueTuple<int>(a); var copy=t; copy.Item1=b; return unchecked(t.Item1*3+copy.Item1*5+(t.Equals(copy)?7:11)); }
        public static int Fields(int a, int b) => Read(Make(a,b));
        public static int Copies(int a, int b) { var t=Make(a,b); var copy=t; copy.Item1=b; copy.Rest.Item1.Value=a; return unchecked(Read(t)+Read(copy)); }
        public static int Heap(int a, int b) { var h=new LongTupleHolder(); var value=Make(a,b); h.Value=value; var copy=h.Value; h.Value.Item5=a; copy.Rest.Item1.Value=b; return unchecked(Read(copy)+Read(h.Value)); }
        public static int Arrays(int a, int b) { var items=new LongTuple[2]; items[0]=Make(a,b); items[1]=items[0]; items[1].Rest.Item1=new TupleFiveCell(a); return unchecked(Read(items[0])+Read(items[1])); }
        public static int Boxing(int a, int b) { var t=Make(a,b); object box=t; t.Item1=b; var copy=(LongTuple)box; return unchecked(Read(t)+Read(copy)); }
        public static int Nullable(int a, int b) { LongTuple? t = a < b ? (LongTuple?)null : Make(a,b); return t.HasValue ? Read(t.Value) : b; }
        private static int Argument(TupleFiveCell trace, int value) { trace.Value=unchecked(trace.Value*3+value); return value; }
        public static int InvalidRest(int a, int b)
        {
            var trace=new TupleFiveCell(0);
            var t=default(ValueTuple<int,int,int,int,int,int,int,int>); t.Item1=a; t.Rest=b;
            try { t=new ValueTuple<int,int,int,int,int,int,int,int>(Argument(trace,a),Argument(trace,b),3,4,5,6,7,8); return 0; }
            catch(ArgumentException e) { return unchecked(Observe(e.Message)+trace.Value+t.Item1*7+t.Rest*11); }
        }
        public static int PublicTupleInterfaceIsInsufficient(int a, int b)
        {
            var rest=new PretendTuple();
            try { var t=new ValueTuple<int,int,int,int,int,int,int,PretendTuple>(a,b,3,4,5,6,7,rest); return t.Item1; }
            catch(ArgumentException e) { return Observe(e.Message); }
        }
        public static int NestedDefaultRest(int a, int b)
        {
            var inner=default(ValueTuple<int,int,int,int,int,int,int,int>); inner.Item1=a; inner.Rest=b;
            var outer=new ValueTuple<int,int,int,int,int,int,int,ValueTuple<int,int,int,int,int,int,int,int>>(a,b,3,4,5,6,7,inner);
            return unchecked(outer.Rest.Item1*3+outer.Rest.Rest*5+outer.Item7);
        }
        public static int Equality(int a, int b)
        {
            var left=(a,a,a,a,a,a,a,a); var right=left; int stage=b&7;
            if(stage==0)right.Item1=b; if(stage==1)right.Item2=b; if(stage==2)right.Item3=b; if(stage==3)right.Item4=b;
            if(stage==4)right.Item5=b; if(stage==5)right.Item6=b; if(stage==6)right.Item7=b; if(stage==7)right.Rest.Item1=b;
            return left.Equals(right)?17:19;
        }
        public static int RestEffects(int a, int b)
        {
            var trace=new TupleFiveCell(0); var first=new TupleFiveComponent(a,1,trace); var last=new TupleFiveComponent(a,3,trace);
            var left=(first,a,a,a,a,a,a,last); var right=left;
            if(b<0)right.Rest.Item1.Tag=-3;
            if((b&3)==1)right.Item1.Value=unchecked(a+1); if((b&3)==2)right.Rest.Item1.Value=unchecked(a+1);
            int result;
            try { result=left.Equals(right)?17:19; } catch(InvalidOperationException e) { result=Observe(e.Message); }
            return unchecked(result+trace.Value*31+left.Item1.Value*7+left.Rest.Item1.Value*11);
        }
        public static int UnsupportedHeapReturn(int a,int b) { var h=new LongTupleHolder(); h.Value=Make(a,b); return Read(h.Value); }
        public static int UnsupportedHash(int a,int b) => (a,b,a,b,a,b,a,b).GetHashCode();
        public static int UnsupportedText(int a,int b) => (a,b,a,b,a,b,a,b).ToString().Length;
    }
}
