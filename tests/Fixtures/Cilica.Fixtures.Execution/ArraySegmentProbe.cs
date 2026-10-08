#nullable disable
using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class SegmentNode { public int Value; public SegmentNode(int value){Value=value;} }
    public struct SegmentCell { public SegmentNode Node; public int Number; public long Wide; }
    public sealed class SegmentOwner<T> { public ArraySegment<T> Segment; }
    public sealed class ConstructorChain<T>
    {
        public T[] Values;
        public ConstructorChain(int capacity):this(capacity,new T[0]){}
        public ConstructorChain(int capacity,T[] values){Values=new T[capacity];Array.Copy(values,Values,values.Length);}
    }
    public static class ArraySegmentProbe
    {
        private static int Hash(string s){if(s==null)return 7;int h=19;for(int i=0;i<s.Length;i++)h=unchecked(h*31+s[i]);return h;}
        private static int Fault(ArgumentException e,int kind)=>unchecked(Hash(e.Message)*31+Hash(e.ParamName)*17+kind);
        public static int Whole(int a,int b)
        {
            int[] values=a<0?null:new[]{a,b,17};
            try{var s=new ArraySegment<int>(values);return unchecked(s.Count*31+s.Offset+(s.Array==values?7:0)+s.Array[1]);}
            catch(ArgumentNullException e){return Fault(e,1);}
        }
        public static int Bounds(int a,int b)
        {
            try{var s=new ArraySegment<int>(new int[3],a,b);return unchecked(s.Count*31+s.Offset+7);}
            catch(ArgumentNullException e){return Fault(e,1);}catch(ArgumentOutOfRangeException e){return Fault(e,2);}catch(ArgumentException e){return Fault(e,3);}
        }
        public static int NullOrder(int a,int b)
        {
            try{var s=new ArraySegment<int>(null,a,b);return s.Count;}
            catch(ArgumentNullException e){return Fault(e,1);}catch(ArgumentException e){return Fault(e,3);}
        }
        public static int Defaults(int a,int b)
        {
            var s=default(ArraySegment<int>);var t=new ArraySegment<int>(new int[0]);
            return s.Count*17+s.Offset+(s.Array==null?31:0)+t.Count*7+t.Offset+(t.Array!=null?101:0);
        }
        private static ArraySegment<T> Copy<T>(ArraySegment<T> s)=>s;
        public static int Copies(int a,int b)
        {
            var values=new[]{a,b,17,19};var source=new ArraySegment<int>(values,1,2);var copy=Copy(source);
            var owner=new SegmentOwner<int>{Segment=copy};var segments=new ArraySegment<int>[2];segments[0]=owner.Segment;segments[1]=new ArraySegment<int>(values,values.Length,0);
            source=default;values[1]=unchecked(b+1);owner.Segment=default;
            return unchecked(segments[0].Array[segments[0].Offset]*31+segments[0].Count*7+segments[1].Offset+(segments[0].Array==segments[1].Array?103:0));
        }
        public static int References(int a,int b)
        {
            var values=new[]{new SegmentNode(a),new SegmentNode(b)};var s=new ArraySegment<SegmentNode>(values,1,1);var copy=Copy(s);
            values[1]=new SegmentNode(unchecked(b+17));s=default;
            return unchecked(copy.Array[copy.Offset].Value*31+copy.Count);
        }
        public static int Values(int a,int b)
        {
            var values=new SegmentCell[]{new SegmentCell{Node=new SegmentNode(a),Number=b,Wide=long.MinValue+b}};
            var s=new ArraySegment<SegmentCell>(values);var copy=Copy(s);values[0].Number=unchecked(b+17);s=default;
            return unchecked(copy.Array[0].Node.Value*31+copy.Array[0].Number+(int)(copy.Array[0].Wide>>32));
        }
        public static int Widths(int a,int b)
        {
            var u=new ArraySegment<uint>(new[]{unchecked((uint)a),(uint)0x80000000});var l=new ArraySegment<long>(new[]{long.MinValue+(long)b});
            var c=new ArraySegment<char>(new[]{(char)0xffff});var n=new ArraySegment<byte>(new[]{(byte)255});var f=new ArraySegment<bool>(new[]{a<0});
            return unchecked((int)u.Array[0]+(int)(l.Array[0]>>32)+c.Array[0]+n.Array[0]+(f.Array[0]?17:0)+u.Count+l.Count);
        }
        public static int InterfaceCounts(int a,int b)
        {
            var values=new[]{a,b,17};var original=new ArraySegment<int>(values,1,2);
            IList<int> list=original;System.Collections.Generic.IReadOnlyCollection<int> read=original;
            original=default;IList<int> empty=original;
            return list.Count*31+read.Count*7+empty.Count;
        }
        public static int IndexerGap(int a,int b){var s=new ArraySegment<int>(new[]{a,b});return s[0];}
        public static int EqualityGap(int a,int b){var s=new ArraySegment<int>(new[]{a,b});return s.Equals((object)s)?1:0;}
        public static int InterfaceGap(int a,int b){IList<int> s=new ArraySegment<int>(new[]{a,b});s.Add(a);return s.Count;}
        public static int ChainedConstructorGap(int a,int b){var x=new ConstructorChain<int>(2);return x.Values.Length;}
        public static SegmentOwner<SegmentNode> CreateTracked()
        {
            var values=new[]{new SegmentNode(1),new SegmentNode(2),new SegmentNode(3)};
            return new SegmentOwner<SegmentNode>{Segment=new ArraySegment<SegmentNode>(values,3,0)};
        }
        public static SegmentOwner<SegmentCell> CreateTrackedValues()
        {
            var values=new SegmentCell[3];for(int i=0;i<3;i++)values[i]=new SegmentCell{Node=new SegmentNode(i),Number=i,Wide=i};
            return new SegmentOwner<SegmentCell>{Segment=new ArraySegment<SegmentCell>(values,3,0)};
        }
        public static SegmentOwner<SegmentNode> CopyTracked(SegmentOwner<SegmentNode> source)=>new SegmentOwner<SegmentNode>{Segment=source.Segment};
        public static SegmentOwner<SegmentCell> CopyTrackedValues(SegmentOwner<SegmentCell> source)=>new SegmentOwner<SegmentCell>{Segment=source.Segment};
        public static void ClearTracked(SegmentOwner<SegmentNode> source){source.Segment=default;}
        public static void ClearTrackedValues(SegmentOwner<SegmentCell> source){source.Segment=default;}
        public static int Lifetime(int a,int b){var s=CreateTracked();var c=CopyTracked(s);ClearTracked(s);ClearTracked(c);return c.Segment.Count;}
        public static int LifetimeValues(int a,int b){var s=CreateTrackedValues();var c=CopyTrackedValues(s);ClearTrackedValues(s);ClearTrackedValues(c);return c.Segment.Count;}
    }
}
