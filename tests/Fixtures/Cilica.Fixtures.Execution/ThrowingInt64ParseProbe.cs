using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public static class ThrowingInt64ParseProbe
    {
        public static string SpecialInput(int index)
        {
            switch (index & 31)
            {
                case 0: return "9223372036854775808x"; case 1: return "9223372036854775808 "; case 2: return "9223372036854775808\0";
                case 3: return "9223372036854775808\0 "; case 4: return "-9223372036854775809x"; case 5: return "-9223372036854775809\t\0";
                case 6: return "999999999999999999999999999999999x"; case 7: return "999999999999999999999999999999999\0";
                case 8: return "999999999999999999999999999999999\u00a0"; case 9: return "9223372036854775808+";
                case 10: return "9223372036854775808\ud800"; case 11: return "a'b"; case 12: return "\ud800\0\udc00";
                case 13: return "-0000000000000000000000000000000009223372036854775808";
                case 14: return "+0000000000000000000000000000000009223372036854775807";
                case 15: return "000000000000000000000000000000000000000000";
                case 16: return "9223372036854775808\n\0\0"; case 17: return "9223372036854775808\n\0x";
                case 18: return "9223372036854775808 1"; case 19: return "999999999999999999999999999999999";
                default: return Int64ParseProbe.Input(index);
            }
        }
        private static int Hash(string text) { int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        private static int ParseText(string text,int mode)
        {
            try { long value=long.Parse(text,CultureInfo.InvariantCulture);return mode==0?unchecked((int)value):mode==1?unchecked((int)(value>>32)):1; }
            catch(ArgumentNullException error){return mode==0?11:unchecked(Hash(error.Message)*31+Hash(error.ParamName));}
            catch(FormatException error){return mode==0?13:Hash(error.Message);}
            catch(OverflowException error){return mode==0?17:Hash(error.Message);}
        }
        public static int Catalog(int a,int b)=>ParseText(Int64ParseProbe.Input(a),b);
        public static int Precedence(int a,int b)=>ParseText(SpecialInput(a),b);
        public static int Roundtrip(int a,int b)
        {
            long value=unchecked(((long)a<<32)|(uint)b);
            return long.Parse(value.ToString(CultureInfo.InvariantCulture),CultureInfo.InvariantCulture)==value?1:0;
        }
        public static int CodeUnits(int a,int b)=>ParseText(string.Concat("9223372036854775808",new string(new char[]{unchecked((char)a)})),b);
        public static int UnsupportedStyle(int a,int b)=>unchecked((int)long.Parse("17",NumberStyles.HexNumber,CultureInfo.InvariantCulture));
        public static int UnsupportedProvider(int a,int b)=>unchecked((int)long.Parse("17",null));
        public static int UnsupportedDynamicStyle(int a,int b)=>unchecked((int)long.Parse("17",(NumberStyles)a,CultureInfo.InvariantCulture));
        private static IFormatProvider Provider()=>CultureInfo.InvariantCulture;
        public static int UnsupportedProviderAlias(int a,int b)=>unchecked((int)long.Parse("17",Provider()));
        public static int UnsupportedDefault(int a,int b)=>unchecked((int)long.Parse("17"));
    }
}
