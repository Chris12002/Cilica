using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public sealed class Int64IntegerParseHolder { public long Value; }
    public static class Int64IntegerParseProbe
    {
        public static string Input(int index) => Int64ParseProbe.Input(index);

        public static int Parse(int a,int b) {long value=731;bool ok=long.TryParse(Input(a),NumberStyles.Integer,CultureInfo.InvariantCulture,out value);return (b&3)==0?(int)value:(b&3)==1?(int)(value>>32):ok?1:0;}
        public static int Roundtrip(int a,int b) {long input=unchecked(((long)a<<32)|(uint)b),value;bool ok=long.TryParse(input.ToString(CultureInfo.InvariantCulture),NumberStyles.Integer,CultureInfo.InvariantCulture,out value);return unchecked((int)value*31+(int)(value>>32)+(ok?17:19));}
        public static int Heap(int a,int b) {var holder=new Int64IntegerParseHolder();holder.Value=731;bool ok=long.TryParse(Input(a),NumberStyles.Integer,CultureInfo.InvariantCulture,out holder.Value);var pressure=new Int64IntegerParseHolder();pressure.Value=b;return unchecked((int)holder.Value*31+(int)(holder.Value>>32)+(ok?17:31)+(int)pressure.Value);}
        public static int NullOutput(int a,int b) {Int64IntegerParseHolder holder=a<0?null:new Int64IntegerParseHolder();try{bool ok=long.TryParse(Input(b),NumberStyles.Integer,CultureInfo.InvariantCulture,out holder.Value);return ok?(int)holder.Value:19;}catch(NullReferenceException){return 23;}}
        public static int UnsupportedStyle(int a,int b) {long value;return long.TryParse("17",NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value)?(int)value:0;}
        public static int UnsupportedProvider(int a,int b) {long value;return long.TryParse("17",NumberStyles.Integer,null,out value)?(int)value:0;}
        public static int UnsupportedFlagsText(int a,int b) => ((NumberStyles)a).ToString().Length;
        public static int UnsupportedDynamicStyle(int a,int b) {long value;return long.TryParse("17",(NumberStyles)a,CultureInfo.InvariantCulture,out value)?(int)value:0;}
        public static int UnsupportedDefault(int a,int b) {long value;return long.TryParse("17",out value)?(int)value:0;}
    }
}
