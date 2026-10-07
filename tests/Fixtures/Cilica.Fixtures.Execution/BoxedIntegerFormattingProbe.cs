using System;
namespace Cilica.Fixtures.Execution
{
    public static class BoxedIntegerFormattingProbe
    {
        private static int Observe(string s) { int h=17;for(int i=0;i<s.Length;i++)h=unchecked(h*31+s[i]);return h; }
        public static int Signed(int a,int b)=>Observe(string.Format("({0}:{1})/{0,14}/{1,-15}",a,b));
        public static int Unsigned(int a,int b)=>Observe(string.Format("{{{0}}}:{1,14}:{0,-15}",(uint)a,(uint)b));
        public static int Wide(int a,int b) { long value=((long)a<<32)|(uint)b;return Observe(string.Format("{0}:{1}:{2}",value,(ulong)value,long.MinValue)); }
        public static int Mixed(int a,int b) { string text=new string(new[]{(char)b,'\0','\ud800'});return Observe(string.Format("{2}/{1,-14}/{0}/{1}",text,a,(TextEnum)b)); }
        public static int Values(int a,int b) { var value=new FormattingValue();value.Text="ordinary";return unchecked(Observe(string.Format("{0}/{2}/{1}/{2}/{0}",value,(long)a,(uint)b))+value.Calls); }
        public static int Syntax(int a,int b)
        {
            var value=new ThrowingFormattingValue();
            try {
                switch(b&7) {
                    case 0:return Observe(string.Format("{1}{2}",a,value));
                    case 1:return Observe(string.Format("{2}{1}",a,value));
                    case 2:return Observe(string.Format("{0}{",a));
                    case 3:return Observe(string.Format("}",a));
                    case 4:return Observe(string.Format("{0,+2}",a));
                    case 5:return Observe(string.Format("{0,10000001}",a));
                    case 6:return Observe(string.Format("{0}\0\ud800",a));
                    default:return Observe(string.Format("{{literal:colon}}{0}",a));
                }
            }catch(FormatException e){return Observe(e.Message);}catch(InvalidOperationException e){return Observe(e.Message);}
        }
        private static int Argument(FormatObserver observer,int index,int stop)
        {observer.Calls=unchecked(observer.Calls*3+index);if(index==stop)throw new InvalidOperationException("argument");return index;}
        public static int Order(int a,int b)
        {
            var observer=new FormatObserver();int result=0;
            try{result=Observe(string.Format("{9}",Argument(observer,1,a&3),Argument(observer,2,a&3),Argument(observer,3,a&3)));}
            catch(InvalidOperationException e){result=Observe(e.Message);}catch(FormatException e){result=Observe(e.Message);}
            return unchecked(result+observer.Calls+b);
        }
        public static int UnsupportedSuffix(int a,int b)=>string.Format("{0:X}",a).Length;
        public static int UnsupportedEmptySuffix(int a,int b)=>string.Format("{0:}",a).Length;
        public static int UnsupportedDynamic(int a,int b)=>string.Format(a<0?"{0}":"<{0}>",b).Length;
        public static int UnsupportedLiteralAlias(int a,int b) { string format="{0}";return string.Format(format,a).Length; }
        public static int UnsupportedAlias(int a,int b) { object value=a;return string.Format("{0}",value).Length; }
        public static int UnsupportedBoolean(int a,int b)=>string.Format("{0}",a<0).Length;
        public static int UnsupportedCharacter(int a,int b)=>string.Format("{0}",(char)a).Length;
        public static int UnsupportedNarrow(int a,int b)=>string.Format("{0}",(short)a).Length;
    }
}
