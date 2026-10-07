using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public sealed class ParseHolder { public int Value; }
    public static class IntegerParseProbe
    {
        public static string Input(int index)
        {
            switch (index & 63)
            {
                case 0: return null; case 1: return ""; case 2: return "0"; case 3: return "-0";
                case 4: return "+0"; case 5: return "2147483647"; case 6: return "-2147483648";
                case 7: return "2147483648"; case 8: return "-2147483649"; case 9: return "0000000000000000000000000000000017";
                case 10: return "\t\r\n -127 \v\f "; case 11: return "- 1"; case 12: return "+"; case 13: return "-";
                case 14: return "1.0"; case 15: return "1,000"; case 16: return "1e2"; case 17: return "(1)";
                case 18: return "12\0"; case 19: return "12\0\0"; case 20: return "12 \0\0";
                case 21: return "12\0 "; case 22: return "\012"; case 23: return "12\0x";
                case 24: return " \0"; case 25: return "\u00a017"; case 26: return "17\u00a0";
                case 27: return "\u200317"; case 28: return "\u0661"; case 29: return "\uff11";
                case 30: return "\u22121"; case 31: return "--1"; case 32: return "++1"; case 33: return "+-1";
                case 34: return "1-"; case 35: return "1+"; case 36: return "  "; case 37: return "\0";
                case 38: return "9999999999999999999999999999999"; case 39: return "-0002147483648";
                case 40: return "+2147483647\t\0"; case 41: return "\ud800"; case 42: return "1\udc00";
                case 43: return "0x10"; case 44: return "2_000"; case 45: return "0123";
                case 46: return "\t+42\r"; case 47: return "1\t2"; case 48: return "-2147483648\0\0";
                default: return "123456789";
            }
        }
        public static int Parse(int a, int b) { int value=731; bool ok=int.TryParse(Input(a),NumberStyles.Integer,CultureInfo.InvariantCulture,out value); return b == 0 ? value : ok ? 1 : 0; }
        public static int Roundtrip(int a,int b) { int value; bool ok=int.TryParse(a.ToString(CultureInfo.InvariantCulture),NumberStyles.Integer,CultureInfo.InvariantCulture,out value); return ok ? value : b; }
        public static int Heap(int a,int b) { var holder=new ParseHolder(); holder.Value=731; bool ok=int.TryParse(Input(a),NumberStyles.Integer,CultureInfo.InvariantCulture,out holder.Value); var pressure=new ParseHolder(); pressure.Value=b; return unchecked(holder.Value+(ok?17:31)+pressure.Value); }
        public static int NullOutput(int a,int b) { ParseHolder holder=a<0?null:new ParseHolder();try { bool ok=int.TryParse(Input(b),NumberStyles.Integer,CultureInfo.InvariantCulture,out holder.Value);return ok?holder.Value:19;} catch(NullReferenceException){return 23;} }
        public static int UnsupportedStyle(int a,int b) { int value; return int.TryParse("17",NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value)?value:0; }
        public static int UnsupportedProvider(int a,int b) { int value; return int.TryParse("17",NumberStyles.Integer,null,out value)?value:0; }
        public static int UnsupportedFlagsText(int a,int b) => ((NumberStyles)a).ToString().Length;
        public static int UnsupportedDynamicStyle(int a,int b) { int value; return int.TryParse("17",(NumberStyles)a,CultureInfo.InvariantCulture,out value)?value:0; }
        public static int UnsupportedDefault(int a,int b) { int value; return int.TryParse("17",out value)?value:0; }
    }
}
