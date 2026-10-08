using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public static class ThrowingIntegerParseProbe
    {
        public static string SpecialInput(int index)
        {
            switch (index & 31)
            {
                case 0: return "2147483648x"; case 1: return "2147483648 "; case 2: return "2147483648\0";
                case 3: return "2147483648\0 "; case 4: return "-2147483649x"; case 5: return "-2147483649\t\0";
                case 6: return "999999999999999999999999999999999x"; case 7: return "999999999999999999999999999999999\0";
                case 8: return "999999999999999999999999999999999\u00a0"; case 9: return "2147483648+";
                case 10: return "2147483648\ud800"; case 11: return "a'b"; case 12: return "\ud800\0\udc00";
                case 13: return "-0000000000000000000000000000000002147483648";
                case 14: return "+0000000000000000000000000000000002147483647";
                case 15: return "000000000000000000000000000000000000000000";
                case 16: return "2147483648\n\0\0"; case 17: return "2147483648\n\0x";
                case 18: return "2147483648 1"; case 19: return "999999999999999999999999999999999";
                default: return IntegerParseProbe.Input(index);
            }
        }
        private static int Hash(string text) { int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        private static int ParseText(string text,int mode)
        {
            try { int value=int.Parse(text,NumberStyles.Integer,CultureInfo.InvariantCulture);return mode==0?value:1; }
            catch(ArgumentNullException error){return mode==0?11:unchecked(Hash(error.Message)*31+Hash(error.ParamName));}
            catch(FormatException error){return mode==0?13:Hash(error.Message);}
            catch(OverflowException error){return mode==0?17:Hash(error.Message);}
        }
        public static int Catalog(int a,int b)=>ParseText(IntegerParseProbe.Input(a),b);
        public static int Precedence(int a,int b)=>ParseText(SpecialInput(a),b);
        public static int Roundtrip(int a,int b)=>int.Parse(a.ToString(CultureInfo.InvariantCulture),NumberStyles.Integer,CultureInfo.InvariantCulture);
        public static int CodeUnits(int a,int b)=>ParseText(string.Concat("2147483648",new string(new char[]{unchecked((char)a)})),b);
        public static int UnsupportedStyle(int a,int b)=>int.Parse("17",NumberStyles.HexNumber,CultureInfo.InvariantCulture);
        public static int UnsupportedProvider(int a,int b)=>int.Parse("17",NumberStyles.Integer,null);
        public static int UnsupportedDynamicStyle(int a,int b)=>int.Parse("17",(NumberStyles)a,CultureInfo.InvariantCulture);
        public static int UnsupportedDefault(int a,int b)=>int.Parse("17");
    }
}
