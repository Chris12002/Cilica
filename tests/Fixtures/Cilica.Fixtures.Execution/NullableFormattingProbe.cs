#nullable disable
using System;
namespace Cilica.Fixtures.Execution
{
    public static class NullableFormattingProbe
    {
        private static int Hash(string text,int seed=17) { for(int i=0;i<text.Length;i++)seed=unchecked(seed*31+text[i]);return seed; }
        public static int Named(int a,int b)
        {
            string text=a<0?null:"x\0\ud800";FormattingValue? value=(b&1)==0?(FormattingValue?)new FormattingValue{Text=text}:null;
            string result=string.Format("<{0,8}>/{0,-9:ignored}/{0}",value);return Hash(result)+(value.HasValue?value.Value.Calls:17);
        }
        public static int Mutation(int a,int b)
        {
            MutableNullableText? value=new MutableNullableText{Count=a,Mode=b%3};int h=17;
            try{h=Hash(string.Format("{0}/{0}/{0}",value));}catch(InvalidOperationException e){h=Hash(e.Message);}
            return unchecked(h*31+value.Value.Count+(value.Value.Node==null?7:11));
        }
        public static int Faults(int a,int b)
        {
            ThrowingFormattingValue? value=a<0?null:(ThrowingFormattingValue?)new ThrowingFormattingValue();string pattern=b==0?null:b==1?"{":b==2?"{0}{":b==3?"{1}":"[{0}]";
            try{return Hash(string.Format(pattern,value));}catch(ArgumentNullException e){return Hash(e.Message);}catch(FormatException e){return Hash(e.Message);}catch(InvalidOperationException e){return Hash(e.Message);}
        }
        public static int Two(int a,int b)
        {
            NullableTextId? first=(a&1)==0?(NullableTextId?)new NullableTextId{Text="first"}:null;
            FormattingValue? second=(b&1)==0?(FormattingValue?)new FormattingValue{Text="second"}:null;
            return Hash(string.Format("{1}/{0}/{1}",first,second));
        }
        public static int Three(int a,int b)
        {
            NullableTextId? first=(a&1)==0?(NullableTextId?)new NullableTextId{Text="first"}:null;
            FormattingValue? second=(b&1)==0?(FormattingValue?)new FormattingValue{Text="second"}:null;
            return Hash(string.Format("{2}/{1}/{0}/{1}",first,second,"text"));
        }
        private static string Generic<T>(T? value)where T:struct=>string.Format("[{0}]",value);
        public static int GenericCall(int a,int b)
        {
            GenericFormattingValue<string>? value=(b&1)==0?(GenericFormattingValue<string>?)new GenericFormattingValue<string>(a<0?null:"text"):null;
            return Hash(Generic(value));
        }
        public static int Enum(int a,int b) { TextEnum? value=(b&1)==0?(TextEnum?)a:null;return Hash(string.Format("{0}/{0:X}/{0:D}",value)); }
        public static int MixedEnum(int a,int b) { TextEnum? value=(b&1)==0?(TextEnum?)a:null;NullableTextId? other=new NullableTextId{Text="id"};return Hash(string.Format("{0}/{1}/{2}","text",value,other)); }
        public static int UnsupportedAlias(int a,int b) { object value=(NullableTextId?)new NullableTextId{Text="text"};return string.Format("{0}",value).Length; }
        public static int UnsupportedJoin(int a,int b)=>string.Format("{0}",a<0?null:(object)(NullableTextId?)new NullableTextId{Text="text"}).Length;
        public static int UnsupportedMixedAlias(int a,int b) { object first=(NullableTextId?)new NullableTextId{Text="text"};FormattingValue? second=new FormattingValue{Text="next"};return string.Format("{0}/{1}",first,second).Length; }
        public static int UnsupportedCustom(int a,int b) { CustomFormattingValue? value=new CustomFormattingValue();return string.Format("{0:X}",value).Length; }
        public static int UnsupportedPrimitive(int a,int b) { int? value=a;return string.Format("{0}",value).Length; }
    }
}
