using System;
namespace Cilica.Fixtures.Execution
{
    public static class OrdinalPrefixProbe
    {
        private static int Check(string text,string prefix)
        {
            try { return text.StartsWith(prefix,StringComparison.Ordinal)?1:0; }
            catch(ArgumentNullException e) { return e.ParamName=="value" && e.Message=="Value cannot be null. (Parameter 'value')"?-3:-5; }
            catch(NullReferenceException) { return -7; }
        }
        public static int Compare(int a,int b) => Check(OrdinalTextProbe.Pick(a),OrdinalTextProbe.Pick(b));
        public static int Units(int a,int b)
        {
            var chars=new char[3];chars[0]=unchecked((char)a);chars[1]='\0';chars[2]=unchecked((char)b);
            var prefix=new char[2];prefix[0]=unchecked((char)b);prefix[1]='\0';
            return Check(new string(chars),new string(prefix));
        }
        public static int Identity(int a,int b) { var text=OrdinalTextProbe.Pick(a); return Check(text,text); }
        public static int Empty(int a,int b) => Check(a<0?null:"abc","");
        public static int Fault(int a,int b) => Check(a<0?null:"abc",b<0?null:"a");
        public static int Composed(int a,int b)
        {
            var chars=new char[3];chars[0]=unchecked((char)a);chars[1]='\0';chars[2]=unchecked((char)b);
            var text=new string(chars);return Check(text,text.Substring(0,2));
        }
        public static int Literals(int a,int b) => ("abc".StartsWith("ab",StringComparison.Ordinal)?1:0)
            +("abc".StartsWith("abc\0",StringComparison.Ordinal)?4:0)+("".StartsWith("",StringComparison.Ordinal)?2:0);
        public static int IgnoreCase(int a,int b) => "abc".StartsWith("a",StringComparison.OrdinalIgnoreCase)?1:0;
        public static int Culture(int a,int b) => "abc".StartsWith("a",StringComparison.InvariantCulture)?1:0;
        public static int Unknown(int a,int b) => "abc".StartsWith("a",(StringComparison)a)?1:0;
        public static int Alias(int a,int b) { var mode=a<0?StringComparison.Ordinal:StringComparison.OrdinalIgnoreCase;return "abc".StartsWith("a",mode)?1:0; }
        public static int DefaultCulture(int a,int b) => "abc".StartsWith("a")?1:0;
    }
}
