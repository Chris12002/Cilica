#nullable disable
using System;
namespace Cilica.Fixtures.Execution
{
    public enum ClearCode { First=1, Last=-1 }
    public sealed class ClearNode { public int Value; public ClearNode(int value){Value=value;} }
    public struct ClearCell { public int Number;public long Wide;public ClearNode Reference; }
    public sealed class ClearOwner
    {
        public int[] Values;public int Trace;
        public ClearOwner(int a){Values=new[]{a,7,9};}
        public int ClearAll(){Array.Clear(Values,0,Values.Length);return Values[0]+Values[1]+Values[2];}
    }
    public static class ArraySectionClearProbe
    {
        private static int Hash(string value){if(value==null)return 13;int h=17;for(int i=0;i<value.Length;i++)h=unchecked(h*31+value[i]);return h;}
        private static int Clear<T>(T[] array,int start,int count)
        {
            try{Array.Clear(array,start,count);return 19;}
            catch(ArgumentNullException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName));}
            catch(IndexOutOfRangeException e){return Hash(e.Message);}
        }
        private static int Fold(int[] values,int h){for(int i=0;i<values.Length;i++)h=unchecked(h*31+values[i]);return h;}
        public static int Ints(int a,int b){var v=new[]{11,13,17};return Fold(v,Clear(v,a,b));}
        public static int Widths(int a,int b)
        {
            uint[] u={0xffffffffu,0x80000000u,7};long[] l={long.MinValue,long.MaxValue,-1};
            ClearCode[] e={ClearCode.Last,ClearCode.First,ClearCode.Last};
            int h=Clear(u,a,b)+Clear(l,a,b)+Clear(e,a,b);
            for(int i=0;i<3;i++)h=unchecked(h*31+(int)u[i]+(int)(l[i]>>32)+(int)e[i]);return h;
        }
        public static int NarrowByte(int a,int b){return Clear(new byte[]{255,17,99},a,b);}
        public static int NarrowChar(int a,int b){return Clear(new char[]{'x','y','z'},a,b);}
        public static int NarrowBoolean(int a,int b){return Clear(new bool[]{true,true,true},a,b);}
        public static int References(int a,int b)
        {
            string[] exact={"left","middle","right"};object[] view=exact;int h=Clear(view,a,b);
            for(int i=0;i<3;i++)h=unchecked(h*31+(exact[i]==null?11:exact[i].Length));return h;
        }
        public static int Values(int a,int b)
        {
            var v=new ClearCell[3];for(int i=0;i<3;i++)v[i]=new ClearCell{Number=i+11,Wide=long.MinValue+i,Reference=new ClearNode(i+17)};
            int h=Clear(v,a,b);for(int i=0;i<3;i++)h=unchecked(h*31+v[i].Number+(int)(v[i].Wide>>32)+(v[i].Reference==null?7:v[i].Reference.Value));return h;
        }
        public static int Null(int a,int b)
        {
            int h=Clear<int>(null,a,b);try{Array.Clear(null,a,b);}catch(ArgumentNullException e){h=unchecked(h+Hash(e.Message)+Hash(e.ParamName));}return h;
        }
        private static int[] Get(ClearOwner owner){owner.Trace=1;return owner.Values;}
        private static int Start(ClearOwner owner,int a){owner.Trace=owner.Trace*10+2;owner.Values=new[]{101,103,107};return a;}
        private static int Count(ClearOwner owner,int b){owner.Trace=owner.Trace*10+3;if(b<0)throw new InvalidOperationException("count");return b;}
        public static int Order(int a,int b)
        {
            var owner=new ClearOwner(11);var original=owner.Values;int h=0;
            try{Array.Clear(Get(owner),Start(owner,a),Count(owner,b));}
            catch(IndexOutOfRangeException e){h=Hash(e.Message);}
            catch(InvalidOperationException e){h=Hash(e.Message);}
            return Fold(original,Fold(owner.Values,unchecked(h+owner.Trace)));
        }
        public static int Field(int a,int b){var owner=new ClearOwner(a);return owner.ClearAll();}
        public static int Empty(int a,int b){return Clear(new int[0],a,b);}
        public static int GeneralAlias(int a,int b){Array array=new[]{a,b};return General(array,a,b);}
        private static int General(Array array,int a,int b){Array.Clear(array,a,b);return a;}
        public static int JoinedArguments(int a,int b){var values=new[]{a,b};Array.Clear(values,a<0?0:1,1);return values[0];}
        public static ClearNode[] CreateTracked(){return new[]{new ClearNode(1),new ClearNode(2),new ClearNode(3)};}
        public static void ClearTracked(ClearNode[] values){Array.Clear(values,1,1);}
        public static void ClearAllTracked(ClearNode[] values){Array.Clear(values,0,values.Length);}
        public static ClearCell[] CreateTrackedValues()
        {
            var values=new ClearCell[3];for(int i=0;i<3;i++)values[i]=new ClearCell{Reference=new ClearNode(i),Number=17};return values;
        }
        public static void ClearTrackedValues(ClearCell[] values){Array.Clear(values,1,1);}
        public static void ClearAllTrackedValues(ClearCell[] values){Array.Clear(values,0,values.Length);}
        public static int Lifetime(int a,int b){var x=CreateTracked();ClearTracked(x);ClearAllTracked(x);return x.Length;}
        public static int LifetimeValues(int a,int b){var x=CreateTrackedValues();ClearTrackedValues(x);ClearAllTrackedValues(x);return x.Length;}
    }
}
