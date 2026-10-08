using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public static class ProviderIntegerParseProbe
    {
        public static string SpecialInput(int index)=>ThrowingIntegerParseProbe.SpecialInput(index);
        private static int Hash(string text) { int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        private static int ParseText(string text,int mode)
        {
            try { int value=int.Parse(text,CultureInfo.InvariantCulture);return mode==0?value:1; }
            catch(ArgumentNullException error){return mode==0?11:unchecked(Hash(error.Message)*31+Hash(error.ParamName));}
            catch(FormatException error){return mode==0?13:Hash(error.Message);}
            catch(OverflowException error){return mode==0?17:Hash(error.Message);}
        }
        public static int Catalog(int a,int b)=>ParseText(IntegerParseProbe.Input(a),b);
        public static int Precedence(int a,int b)=>ParseText(SpecialInput(a),b);
        public static int Roundtrip(int a,int b)=>int.Parse(a.ToString(CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
        public static int CodeUnits(int a,int b)=>ParseText(string.Concat("2147483648",new string(new char[]{unchecked((char)a)})),b);
        public static int UnsupportedStyle(int a,int b)=>int.Parse("17",NumberStyles.HexNumber,CultureInfo.InvariantCulture);
        public static int UnsupportedProvider(int a,int b)=>int.Parse("17",null);
        public static int UnsupportedDynamicStyle(int a,int b)=>int.Parse("17",(NumberStyles)a,CultureInfo.InvariantCulture);
        public static int UnsupportedInt64IntegerStyle(int a,int b)=>unchecked((int)long.Parse("17",NumberStyles.Integer,CultureInfo.InvariantCulture));
        private static IFormatProvider Provider()=>CultureInfo.InvariantCulture;
        public static int UnsupportedProviderAlias(int a,int b)=>int.Parse("17",Provider());
        public static int UnsupportedDefault(int a,int b)=>int.Parse("17");
    }
}
