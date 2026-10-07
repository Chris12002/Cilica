using System;
namespace Cilica.Fixtures.Execution
{
    public static class ArraySearchProbe
    {
        private static int Text(string s) { int h=17;for(int i=0;i<s.Length;i++)h=unchecked(h*31+s[i]);return h; }
        public static int Integers(int a,int b) => Array.IndexOf(new[]{a,b,a},b)*31+Array.IndexOf(new int[0],a);
        public static int Wide(int a,int b) { long x=((long)a<<32)|(uint)b;return Array.IndexOf(new[]{x,~x,x},((b&1)==0?x:~x)); }
        public static int UnsupportedCharacters(int a,int b) => Array.IndexOf(new[]{(char)a,(char)b,(char)a},(char)b);
        public static int UnsupportedBytes(int a,int b) => Array.IndexOf(new[]{(byte)a,(byte)b,(byte)a},(byte)b);
        public static int Enums(int a,int b) => Array.IndexOf(new[]{(ArrayEnum)a,(ArrayEnum)b,(ArrayEnum)a},(ArrayEnum)b);
        public static int Strings(int a,int b)
        {
            var value=new string(new[]{(char)a,(char)b});var equal=new string(new[]{(char)a,(char)b});
            var values=new[]{null,"A","a","a\0z","é","e\u0301","\ud800",value};
            return unchecked(Array.IndexOf(values,equal)*31+Array.IndexOf(values,null)*37+Array.IndexOf(values,"a")+Array.IndexOf(new string[0],value));
        }
        public static int Faults(int a,int b)
        {
            int result=0;try{result=Array.IndexOf((int[])null,a/b);}catch(DivideByZeroException){result=3;}catch(OverflowException){result=5;}
            catch(ArgumentNullException e){result=Text(e.Message)^Text(e.ParamName);}
            return result;
        }
        public static int Values(int a,int b)
        {
            var cell=new SearchCell();cell.Number=a;cell.Payload=new ValueVectorBox(b);
            var values=new[]{cell,cell};cell.Number=b;int result=Array.IndexOf(values,cell);
            return unchecked(result+values[0].Number*31+values[1].Number+values[0].Payload.Number);
        }
        public static int Allocating(int a,int b)
        {
            var cell=new AllocatingSearchCell();cell.Number=a;var values=new[]{cell};cell.Number=b;int result=0;
            try{result=Array.IndexOf(values,cell);}catch(InvalidOperationException e){result=Text(e.Message);}
            return unchecked(result+values[0].Payload.Number);
        }
        public static int Throwing(int a,int b)
        {
            var trace=new KeyTrace();var values=new[]{new CollisionKey(a,trace),new CollisionKey(b,trace)};
            trace.EqualityFailure=1;int result=0;
            try{result=Array.IndexOf(values,new CollisionKey(b,trace));}catch(InvalidOperationException e){result=Text(e.Message);}
            trace.EqualityFailure=0;result=unchecked(result+Array.IndexOf(values,new CollisionKey(b,trace)));
            return unchecked(result+values[0].Number+values[1].Number+trace.Equalities*31);
        }
        public static int UnsupportedNullable(int a,int b)=>Array.IndexOf(new int?[]{a,null},(int?)b);
        public static int UnsupportedReference(int a,int b)=>Array.IndexOf(new[]{new ValueVectorBox(a)},new ValueVectorBox(b));
        public static int UnsupportedStart(int a,int b)=>Array.IndexOf(new[]{a,b},b,1);
        public static int UnsupportedRange(int a,int b)=>Array.IndexOf(new[]{a,b},b,0,1);
    }
}
