#nullable disable
using System;
namespace Cilica.Fixtures.Execution
{
    public struct MutableNullableText
    {
        public int Count,Mode;
        public ProbeNode Node;
        public override string ToString()
        {
            Count=unchecked(Count+1);Node=new ProbeNode(Count);
            if(Mode==1)throw new InvalidOperationException("nullable text");
            if(Mode==2)return null;
            return Count.ToString();
        }
    }
    public struct NullableTextId
    {
        public string Text;
        public override string ToString()=>Text??string.Empty;
    }
    public sealed class NullableTextHolder { public MutableNullableText? Value; }
    public static class NullableTextProbe
    {
        private static int Hash(string text,int seed) { if(text==null)return unchecked(seed*31+13);for(int i=0;i<text.Length;i++)seed=unchecked(seed*31+text[i]);return seed; }
        private static string FormatText(string value)=>string.Format("value:{0}.",value);
        private static string Generic<T>(ref T? value) where T:struct=>value.ToString();
        public static int Integer(int a,int b) { int? value=(b&1)==0?(int?)a:null;return Hash(value.ToString(),b)+(value.HasValue?7:11); }
        public static int Wide(int a,int b) { long? value=(b&1)==0?(long?)unchecked(((long)a<<32)|(uint)b):null;return Hash(value.ToString(),a); }
        public static int Boolean(int a,int b) { bool? value=(b&1)==0?(bool?)(a!=0):null;return Hash(value.ToString(),b); }
        public static int Character(int a,int b) { char? value=(b&1)==0?(char?)(char)a:null;return Hash(value.ToString(),b); }
        public static int Named(int a,int b) { string text=a<0?null:a.ToString();NullableTextId? value=(b&1)==0?(NullableTextId?)new NullableTextId{Text=text}:null;return Hash(value.ToString(),b); }
        public static int Mutable(int a,int b)
        {
            MutableNullableText? value=new MutableNullableText{Count=a,Mode=b%3};int h=17;
            try{h=Hash(value.ToString(),h);}catch(InvalidOperationException e){h=Hash(e.Message,h);}
            try{h=Hash(Generic(ref value),h);}catch(InvalidOperationException e){h=Hash(e.Message,h);}
            return unchecked(h*31+value.Value.Count+value.Value.Node.Initial);
        }
        public static int EmptyMutable(int a,int b) { MutableNullableText? value=null;return Hash(Generic(ref value),b)+(value.HasValue?7:11); }
        public static int Heap(int a,int b)
        {
            var holder=new NullableTextHolder();holder.Value=new MutableNullableText{Count=a,Mode=b%3};int h=17;
            try{h=Hash(holder.Value.ToString(),h);}catch(InvalidOperationException e){h=Hash(e.Message,h);}
            return unchecked(h*31+holder.Value.Value.Count+holder.Value.Value.Node.Initial);
        }
        public static int Array(int a,int b)
        {
            var values=new MutableNullableText?[]{new MutableNullableText{Count=a,Mode=b%3}};int h=17;
            try{h=Hash(values[0].ToString(),h);}catch(InvalidOperationException e){h=Hash(e.Message,h);}
            return unchecked(h*31+values[0].Value.Count+values[0].Value.Node.Initial);
        }
        public static int UnsupportedNullableComposite(int a,int b) { NullableTextId? value=(b&1)==0?(NullableTextId?)new NullableTextId{Text=a.ToString()}:null;return Hash(string.Format("value:{0}.",value),b); }
        public static int TextComposite(int a,int b) { NullableTextId? value=(b&1)==0?(NullableTextId?)new NullableTextId{Text=a.ToString()}:null;return Hash(FormatText(value.ToString()),b); }
        public static int Copies(int a,int b)
        {
            MutableNullableText? value=new MutableNullableText{Count=a};var copy=value;int h=Hash(copy.ToString(),b);return unchecked(h+copy.Value.Count*31+value.Value.Count+(value.Value.Node==null?7:11));
        }
    }
}
