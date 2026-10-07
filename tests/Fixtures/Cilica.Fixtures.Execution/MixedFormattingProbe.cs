using System;
namespace Cilica.Fixtures.Execution
{
    public static class MixedFormattingProbe
    {
        private static int Observe(string value) { int h=17;for(int i=0;i<value.Length;i++)h=unchecked(h*31+value[i]);return h; }
        private static string Text(int value) => value<0?null:new string(new[]{(char)value,'\0','\ud800'});
        public static int One(int a,int b) { string text=Text(a);return Observe(string.Format("<{0,9:ignored}>/{0,-4:X}/{0}",text)); }
        public static int Two(int a,int b) { string text=Text(a);return Observe(string.Format("<{0,9:ignored}>/{1,-4:X}/{0}",text,(TextEnum)b)); }
        public static int Three(int a,int b) { string first=Text(a);string last=Text(b);return Observe(string.Format("{2,-12:Q}/{1:X}/{0,9}/{1:f}/{2}",first,(TextEnum)a,last)); }
        public static int Strings(int a,int b) => Observe(string.Format("{2,5:X}/{1,-7:Q}/{0}/{2}",Text(a),null,Text(b)));
        public static int Positions(int a,int b)
        {
            var value=new FormattingValue();value.Text=Text(b);string text=Text(a);
            return unchecked(Observe(string.Format("{2,9:X}/{0:f}/{1,-5}/{2:ignored}",(TextEnum)a,text,value))
                +Observe(string.Format("{1:f}/{2,4:Q}/{0}/{0}",value,(TextEnum)b,text))+value.Calls);
        }
        public static int Callbacks(int a,int b)
        {
            var observer=new FormatObserver();observer.Calls=a;
            var first=new ObservedFormattingValue();first.Observer=observer;
            var second=new ObservedFormattingValue();second.Observer=observer;
            var third=new ObservedFormattingValue();third.Observer=observer;
            return unchecked(Observe(string.Format("{2}/{0}/{1}/{2}/{0}/{2}",first,second,third))+observer.Calls+first.Calls+third.Calls);
        }
        private static string Pattern(int mode)
        {
            switch(mode & 15) {
                case 0:return null;case 1:return "{3}";case 2:return "{1:Q}";case 3:return "{2}";
                case 4:return "{0}{";case 5:return "}";case 6:return "{0,+3}";case 7:return "{0:{bad}}";
                case 8:return "{{{0,-5:X}}}/{1:x}";case 9:return "{0000002}";case 10:return "{0,10000001}";
                case 11:return "{0:}";case 12:return "{1:X2}";case 13:return "{2}{3}";case 14:return "{3}{2}";
                default:return "plain\0\ud800";
            }
        }
        public static int Faults(int a,int b)
        {
            string text=Text(a);var value=new ThrowingFormattingValue();
            try{return Observe(string.Format(Pattern(b),text,(TextEnum)a,value));}
            catch(ArgumentNullException e){return Observe(e.Message);}catch(FormatException e){return Observe(e.Message);}catch(InvalidOperationException e){return Observe(e.Message);}
        }
        private static string Argument(FormatObserver observer,int index,int stop)
        { observer.Calls=unchecked(observer.Calls*3+index);if(index==stop)throw new InvalidOperationException("argument");return "text"; }
        public static int ArgumentOrder(int a,int b)
        {
            var observer=new FormatObserver();int result=0;
            try{result=Observe(string.Format((string)null,Argument(observer,1,a&3),Argument(observer,2,a&3),Argument(observer,3,a&3)));}
            catch(InvalidOperationException e){result=Observe(e.Message);}catch(ArgumentNullException e){result=Observe(e.Message);}
            return unchecked(result+observer.Calls+b);
        }
        public static int UnsupportedAlias(int a,int b) { object value=(TextEnum)a;return string.Format("{0}{1}{2}","a",value,"b").Length; }
        public static int UnsupportedStringAlias(int a,int b) { object value=Text(a);return string.Format("{0}{1}{2}","a",value,"b").Length; }
        public static int UnsupportedJoin(int a,int b)=>string.Format("{0}{1}{2}","a",a<0?null:(object)(TextEnum)b,"b").Length;
        public static int UnsupportedScalar(int a,int b)=>string.Format("{0}{1:X}{2}","a",a,"b").Length;
        public static int UnsupportedCustom(int a,int b)=>string.Format("{0}{1}{2}","a",new CustomFormattingValue(),"b").Length;
        public static int UnsupportedFlags(int a,int b)=>string.Format("{0}{1}{2}","a",(FlagTextEnum)a,"b").Length;
    }
}
