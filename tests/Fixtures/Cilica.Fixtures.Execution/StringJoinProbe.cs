using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class JoinTrace { public int Value; public void Add(int n) { Value=unchecked(Value*31+n); } }
    public sealed class JoinSource : IEnumerable<string>, IEnumerator<string>
    {
        private readonly JoinTrace trace;private readonly string[] values;private readonly int fault;private int index=-1;
        public JoinSource(JoinTrace trace,string[] values,int fault){this.trace=trace;this.values=values;this.fault=fault;}
        public IEnumerator<string> GetEnumerator(){trace.Add(1);if(fault==1)throw new InvalidOperationException("get");return this;}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public bool MoveNext(){trace.Add(2);index++;if(fault==2&&index==0||fault==3&&index==1||fault==4&&index==2)throw new InvalidOperationException("move");return index<values.Length;}
        public string Current {get{trace.Add(3);if(fault==5&&index==0||fault==6&&index==1)throw new InvalidOperationException("current");return values[index];}}
        object IEnumerator.Current=>Current;
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){trace.Add(4);if(fault==7)throw new InvalidOperationException("dispose");}
    }
    public sealed class JoinList : List<string>, IEnumerable<string>
    {
        IEnumerator<string> IEnumerable<string>.GetEnumerator(){throw new InvalidOperationException("must bypass");}
    }
    public static class StringJoinProbe
    {
        private static int Hash(string value){int h=17;for(int i=0;i<value.Length;i++)h=unchecked(h*31+value[i]);return h;}
        private static string Text(int n)=> (n&3)==0?null:(n&3)==1?"":(n&3)==2?"A\0\ud800":"\udfffB";
        private static string Separator(int n)=>(n&3)==0?null:(n&3)==1?"":(n&3)==2?"|":"\0\ud800x";
        public static int Array(int a,int b)
        {
            var values=new string[a&3];for(int i=0;i<values.Length;i++)values[i]=Text(b+i);
            return Hash(string.Join(Separator(b),(IEnumerable<string>)values));
        }
        public static int List(int a,int b)
        {
            var values=new JoinList();for(int i=0;i<(a&3);i++)values.Add(Text(b+i));
            return Hash(string.Join(Separator(b),(IEnumerable<string>)values));
        }
        public static int Cursor(int a,int b)
        {
            var trace=new JoinTrace();var values=new string[a&3];for(int i=0;i<values.Length;i++)values[i]=Text(b+i);
            int result;
            try{result=Hash(string.Join(Separator(b),new JoinSource(trace,values,(a>>2)&7)));}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            return unchecked(result*31+trace.Value);
        }
        public static int Identity(int a,int b)
        {
            string text=string.Concat("dynamic",a.ToString());string value=(b&1)==0?text:null;
            var values=new[]{value};var trace=new JoinTrace();
            string x=string.Join(Separator(b),(IEnumerable<string>)values);
            string y=string.Join(Separator(b),new JoinSource(trace,values,0));
            var list=new List<string>();list.Add(value);string z=string.Join(Separator(b),(IEnumerable<string>)list);
            string expected=value??"";
            int result=(ReferenceEquals(x,expected)?1:0)+(ReferenceEquals(y,expected)?7:0)+(ReferenceEquals(z,expected)?31:0)+trace.Value;
            var multiple=new[]{"",text,null};
            string many=string.Join("",(IEnumerable<string>)multiple);
            string enumerated=string.Join("",new JoinSource(new JoinTrace(),multiple,0));
            string empty=string.Join(null,(IEnumerable<string>)new string[]{null,""});
            return result+(ReferenceEquals(many,text)?101:0)+(ReferenceEquals(enumerated,text)?503:0)+(ReferenceEquals(empty,"")?2003:0);
        }
        private static string Project(int n)=>Text(n);
        public static int Projection(int a,int b)=>Hash(string.Join(Separator(b),new[]{a,b,a+b}.Select(Project)));
        public static int Growth(int a,int b)
        {
            var values=new string[32+(a&7)];for(int i=0;i<values.Length;i++)values[i]=string.Concat("abcdef0123456789",Text(b+i));
            var trace=new JoinTrace();string x=string.Join(Separator(b),new JoinSource(trace,values,0));
            string y=string.Join(Separator(b),(IEnumerable<string>)values);
            return unchecked(Hash(x)*31+Hash(y)+trace.Value);
        }
        public static int Null(int a,int b)
        {
            try{return Hash(string.Join(Separator(b),(IEnumerable<string>)null));}
            catch(ArgumentNullException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName));}
        }
        public static int UnsupportedGeneric(int a,int b)=>Hash(string.Join<int>(",",new[]{a,b}));
        public static int UnsupportedChar(int a,int b)=>Hash(string.Join(',',new[]{"a","b"}));
    }
}
