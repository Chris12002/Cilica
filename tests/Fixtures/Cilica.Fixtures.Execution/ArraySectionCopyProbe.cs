#nullable disable
using System;
namespace Cilica.Fixtures.Execution
{
    public class CopyRangeBase { public int Value; public CopyRangeBase(int value){Value=value;} }
    public sealed class CopyRangeLeft : CopyRangeBase { public CopyRangeLeft(int value):base(value){} }
    public sealed class CopyRangeRight : CopyRangeBase { public CopyRangeRight(int value):base(value){} }
    public struct CopyRangeCell { public int Number; public long Wide; public CopyRangeBase Reference; }
    public sealed class CopyRangeOwner
    {
        public int[] Source={11,13,17}, Destination={19,23,29};public int Trace;
    }
    public sealed class CopyRangeBox<T>
    {
        public readonly T[] Values;
        public CopyRangeBox(T[] source){Values=new T[source.Length];Array.Copy(source,Values,source.Length);}
    }
    public static class ArraySectionCopyProbe
    {
        private static int Hash(string value){if(value==null)return 7;int h=17;for(int i=0;i<value.Length;++i)h=unchecked(h*31+value[i]);return h;}
        private static int Fold(int[] values,int h){if(values==null)return h+7;for(int i=0;i<values.Length;++i)h=unchecked(h*31+values[i]);return h;}
        private static int Copy<T>(T[] source,int si,T[] destination,int di,int length,bool whole)
        {
            try{if(whole)Array.Copy(source,destination,length);else Array.Copy(source,si,destination,di,length);return 19;}
            catch(ArgumentNullException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName)+1);}
            catch(ArgumentOutOfRangeException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName)+2);}
            catch(ArgumentException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName)+3);}
            catch(ArrayTypeMismatchException e){return unchecked(Hash(e.Message)*31+4);}
            catch(InvalidCastException e){return unchecked(Hash(e.Message)*31+5);}
        }
        public static int Whole(int a,int b){int[] s=b<0?null:new[]{11,13,17};int[] d=b==0?null:new[]{19,23,29,31};return Fold(s,Fold(d,Copy(s,0,d,0,a,true)));}
        public static int Sections(int a,int b){var s=new[]{11,13,17};var d=new[]{19,23,29,31};return Fold(s,Fold(d,Copy(s,a,d,b,1,false)));}
        public static int Bounds(int a,int b){var s=new[]{11,13,17};var d=new[]{19,23,29,31};return Fold(d,Copy(s,a,d,b,3,false));}
        public static int Overlap(int a,int b){var v=new[]{11,13,17,19,23};return Fold(v,Copy(v,a&3,v,b&3,1+(int)((uint)a>>3)%3,false));}
        public static int LengthOrder(int a,int b)
        {
            int[] s=b<0?null:new int[0];int[] d=b==0?null:new int[0];
            int h=Copy(s,-1,d,-2,a,false);try{Array.Copy(null,null,a);}catch(ArgumentNullException e){h=unchecked(h+Hash(e.Message)+Hash(e.ParamName));}return h;
        }
        public static int Widths(int a,int b)
        {
            uint[] u={0xffffffffu,0x80000000u,unchecked((uint)b)},ud={7,11,13};long[] l={long.MinValue,long.MaxValue,b},ld={17,19,23};
            byte[] x={255,17,99},xd={1,2,3};char[] c={'x','\uffff','\ud800'},cd={'a','b','c'};bool[] t={true,true,true},td={false,false,false};
            int h=Copy(u,0,ud,0,a,true)+Copy(l,0,ld,0,a,true)+Copy(x,0,xd,0,a,true)+Copy(c,0,cd,0,a,true)+Copy(t,0,td,0,a,true);
            for(int i=0;i<3;++i)h=unchecked(h*31+(int)ud[i]+(int)(ld[i]>>32)+xd[i]+cd[i]+(td[i]?1:0));return h;
        }
        public static int ReferenceFaults(int a,int b)
        {
            object[] s=new object[]{"first",b<0?(object)new CopyRangeLeft(7):"second",null};string[] exact={"old0","old1","old2"};object[] d=exact;
            int h=Copy(s,0,d,0,a,true);for(int i=0;i<3;++i)h=unchecked(h*31+Hash(exact[i]));return h;
        }
        public static int ReferenceShape(int a,int b)
        {
            CopyRangeBase[] s=new CopyRangeLeft[]{new CopyRangeLeft(11),new CopyRangeLeft(13)};
            CopyRangeBase[] d=b<0?(CopyRangeBase[])new CopyRangeRight[2]:new CopyRangeBase[2];
            int h=Copy(s,0,d,0,a,true);for(int i=0;i<2;++i)h=unchecked(h*31+(d[i]==null?7:d[i].Value));return h;
        }
        public static int Values(int a,int b)
        {
            var s=CreateTrackedValues();var d=CreateTrackedValues();d[1].Number=b;
            int h=Copy(s,0,d,0,a,true);s[0].Number=101;
            for(int i=0;i<3;++i)h=unchecked(h*31+d[i].Number+(int)(d[i].Wide>>32)+(d[i].Reference==null?7:d[i].Reference.Value));return h;
        }
        private static int[] Source(CopyRangeOwner o){o.Trace=1;return o.Source;}
        private static int SourceIndex(CopyRangeOwner o,int a){o.Trace=o.Trace*10+2;o.Source=new[]{101,103,107};return a;}
        private static int[] Destination(CopyRangeOwner o){o.Trace=o.Trace*10+3;return o.Destination;}
        private static int DestinationIndex(CopyRangeOwner o,int b){o.Trace=o.Trace*10+4;o.Destination=new[]{109,113,127};return b;}
        private static int Length(CopyRangeOwner o,int b){o.Trace=o.Trace*10+5;if(b<0)throw new InvalidOperationException("length");return 1;}
        public static int Order(int a,int b)
        {
            var o=new CopyRangeOwner();var oldSource=o.Source;var oldDestination=o.Destination;int h=0;
            try{Array.Copy(Source(o),SourceIndex(o,a),Destination(o),DestinationIndex(o,b),Length(o,b));}
            catch(ArgumentException e){h=unchecked(Hash(e.Message)*31+Hash(e.ParamName));}catch(InvalidOperationException e){h=Hash(e.Message);}
            return Fold(oldSource,Fold(oldDestination,Fold(o.Source,Fold(o.Destination,unchecked(h+o.Trace)))));
        }
        public static int Fields(int a,int b)
        {
            var source=new[]{a,b};var box=new CopyRangeBox<int>(source);source[0]=17;
            return unchecked(box.Values[0]*31+box.Values[1]);
        }
        public static int GeneralAlias(int a,int b){return Alias(new[]{a,b},new int[2]);}
        private static int Alias(Array s,Array d){Array.Copy(s,d,1);return 0;}
        public static int Joined(int a,int b){var s=new[]{a,b};var d=new int[2];Array.Copy(s,a<0?0:1,d,0,1);return d[0];}
        public static int Widening(int a,int b){var s=new int[]{a,b};var d=new long[2];Array.Copy(s,d,2);return 0;}
        public static int Boxing(int a,int b){var s=new int[]{a,b};var d=new object[2];Array.Copy(s,d,2);return 0;}
        public static CopyRangeBase[] CreateTracked(){return new[]{new CopyRangeBase(1),new CopyRangeBase(2),new CopyRangeBase(3)};}
        public static void CopyTracked(CopyRangeBase[] s,CopyRangeBase[] d){Array.Copy(s,0,d,1,1);}
        public static void ClearTracked(CopyRangeBase[] values){Array.Clear(values,0,values.Length);}
        public static CopyRangeCell[] CreateTrackedValues()
        {
            var values=new CopyRangeCell[3];for(int i=0;i<3;++i)values[i]=new CopyRangeCell{Reference=new CopyRangeBase(i),Number=i+17,Wide=long.MinValue+i};return values;
        }
        public static void CopyTrackedValues(CopyRangeCell[] s,CopyRangeCell[] d){Array.Copy(s,0,d,1,1);}
        public static void ClearTrackedValues(CopyRangeCell[] values){Array.Clear(values,0,values.Length);}
        public static int Lifetime(int a,int b){var s=CreateTracked();var d=CreateTracked();CopyTracked(s,d);ClearTracked(s);ClearTracked(d);return d.Length;}
        public static int LifetimeValues(int a,int b){var s=CreateTrackedValues();var d=CreateTrackedValues();CopyTrackedValues(s,d);ClearTrackedValues(s);ClearTrackedValues(d);return d.Length;}
    }
}
