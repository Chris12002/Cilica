using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public sealed class Int64ParseHolder { public long Value; }
    public static class Int64ParseProbe
    {
        public static string Input(int index)
        {
            switch (index & 63)
            {
                case 0: return null; case 1: return ""; case 2: return "0"; case 3: return "-0";
                case 4: return "+0"; case 5: return "9223372036854775807"; case 6: return "-9223372036854775808";
                case 7: return "9223372036854775808"; case 8: return "-9223372036854775809"; case 9: return "0000000000000000000000000000000017";
                case 10: return "\t\r\n -127 \v\f "; case 11: return "- 1"; case 12: return "+"; case 13: return "-";
                case 14: return "1.0"; case 15: return "1,000"; case 16: return "1e2"; case 17: return "(1)";
                case 18: return "12\0"; case 19: return "12\0\0"; case 20: return "12 \0\0";
                case 21: return "12\0 "; case 22: return "\012"; case 23: return "12\0x";
                case 24: return " \0"; case 25: return "\u00a017"; case 26: return "17\u00a0";
                case 27: return "\u200317"; case 28: return "\u0661"; case 29: return "\uff11";
                case 30: return "\u22121"; case 31: return "--1"; case 32: return "++1"; case 33: return "+-1";
                case 34: return "1-"; case 35: return "1+"; case 36: return "  "; case 37: return "\0";
                case 38: return "9999999999999999999999999999999"; case 39: return "-0009223372036854775808";
                case 40: return "+9223372036854775807\t\0"; case 41: return "\ud800"; case 42: return "1\udc00";
                case 43: return "0x10"; case 44: return "2_000"; case 45: return "0123";
                case 46: return "\t+42\r"; case 47: return "1\t2"; case 48: return "-9223372036854775808\0\0";
                default: return "123456789";
            }
        }

        public static int Parse(int a,int b) {long value=731;bool ok=long.TryParse(Input(a),NumberStyles.None,CultureInfo.InvariantCulture,out value);return (b&3)==0?(int)value:(b&3)==1?(int)(value>>32):ok?1:0;}
        public static int Roundtrip(int a,int b) {long input=unchecked(((long)a<<32)|(uint)b),value;bool ok=long.TryParse(input.ToString(CultureInfo.InvariantCulture),NumberStyles.None,CultureInfo.InvariantCulture,out value);return unchecked((int)value*31+(int)(value>>32)+(ok?17:19));}
        public static int Heap(int a,int b) {var holder=new Int64ParseHolder();holder.Value=731;bool ok=long.TryParse(Input(a),NumberStyles.None,CultureInfo.InvariantCulture,out holder.Value);var pressure=new Int64ParseHolder();pressure.Value=b;return unchecked((int)holder.Value*31+(int)(holder.Value>>32)+(ok?17:31)+(int)pressure.Value);}
        public static int NullOutput(int a,int b) {Int64ParseHolder holder=a<0?null:new Int64ParseHolder();try{bool ok=long.TryParse(Input(b),NumberStyles.None,CultureInfo.InvariantCulture,out holder.Value);return ok?(int)holder.Value:19;}catch(NullReferenceException){return 23;}}
        public static int UnsupportedStyle(int a,int b) {long value;return long.TryParse("17",NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value)?(int)value:0;}
        public static int UnsupportedProvider(int a,int b) {long value;return long.TryParse("17",NumberStyles.None,null,out value)?(int)value:0;}
        public static int UnsupportedFlagsText(int a,int b) => ((NumberStyles)a).ToString().Length;
        public static int UnsupportedDynamicStyle(int a,int b) {long value;return long.TryParse("17",(NumberStyles)a,CultureInfo.InvariantCulture,out value)?(int)value:0;}
        public static int UnsupportedDefault(int a,int b) {long value;return long.TryParse("17",out value)?(int)value:0;}
    }
}
