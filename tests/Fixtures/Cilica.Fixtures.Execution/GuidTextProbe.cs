using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class GuidTextHolder { public Guid Value; }
    public static class GuidTextProbe
    {
        private static Guid Value(int a,int b) => new Guid(a,(short)b,(short)(b>>16),(byte)a,(byte)(a>>8),(byte)(a>>16),(byte)(a>>24),(byte)b,(byte)(b>>8),(byte)(b>>16),(byte)(b>>24));
        private static string Format(int value)
        { switch((uint)value%5) {case 0:return "D";case 1:return "N";case 2:return "B";case 3:return "P";default:return "X";} }
        private static int Hash(string text,int seed)
        { for(int i=0;i<text.Length;i++) seed=unchecked(seed*31+text[i]);return seed; }
        private static int Outcome(string text,int seed)
        {
            try { var guid=new Guid(text);return Hash(guid.ToString("D"),seed); }
            catch(ArgumentNullException e) { return Hash(e.Message,Hash(e.ParamName,seed+1)); }
            catch(FormatException e) { return Hash(e.Message,seed+2); }
            catch(OverflowException e) { return Hash(e.Message,seed+3); }
        }
        public static int RoundTrip(int a,int b)
        { var original=Value(a,b);var parsed=new Guid(original.ToString(Format(b)));return parsed.Equals(original)?1:0; }
        public static int Repeated(int a,int b)
        { var original=Value(a,b);string text=original.ToString("X");var parsed=original;for(int i=0;i<9;i++)parsed=new Guid(text);return parsed.Equals(original)?1:0; }
        public static int Storage(int a,int b)
        {
            var original=Value(a,b);var parsed=new Guid(original.ToString("D"));
            var holder=new GuidTextHolder();holder.Value=parsed;var values=new Guid[1];values[0]=parsed;
            Guid? nullable=parsed;object boxed=parsed;
            return holder.Value.Equals(original)&&values[0].Equals(original)&&nullable.Value.Equals(original)&&((Guid)boxed).Equals(original)?1:0;
        }
        public static int Whitespace(int a,int b)
        {
            var chars=new char[1];chars[0]=unchecked((char)a);var space=new string(chars);
            return Outcome(space+"00112233-4455-6677-8899-aabbccddeeff"+space,b);
        }
        public static int Mutation(int a,int b)
        {
            string text=Value(a,b).ToString(Format(a));var chars=new char[text.Length];
            for(int i=0;i<text.Length;i++) chars[i]=text[i];
            chars[(int)((uint)a%(uint)text.Length)]=unchecked((char)b);
            return Outcome(new string(chars),a);
        }
        public static int Corpus(int a,int b) => Outcome(Pick(a),b);
        private static string Pick(int a)
        {
            switch(a)
            {
                case 0:return null;case 1:return "";case 2:return "   ";case 3:return "not a guid";
                case 4:return "00112233-4455-6677-8899-aabbccddeeff";
                case 5:return "00112233445566778899aabbccddeeff";
                case 6:return "{00112233-4455-6677-8899-AABBCCDDEEFF}";
                case 7:return "(00112233-4455-6677-8899-aabbccddeeff)";
                case 8:return "{0x00112233,0x4455,0x6677,{0x88,0x99,0xaa,0xbb,0xcc,0xdd,0xee,0xff}}";
                case 9:return "\t\u0085\u00a0\u1680\u2000\u200a\u2028\u2029\u202f\u205f\u300000112233-4455-6677-8899-aabbccddeeff\r\n";
                case 10:return "{ 0 X 00112233 , 0x4455, 0x6677 , {0x88,0x99,0xaa,0xbb,0xcc,0xdd,0xee,0xff} }";
                case 11:return "+0112233-+455-0x77-+899-0xbbccddeeff";
                case 12:return "0x112233-+0x5-0X77-0x99-+0xbbccddeeff";
                case 13:return "{0x+0x1,0X0000000002,0x12345678,{0x+0x88,0x99,0xaa,0xbb,0xcc,0xdd,0xee,0xff}}";
                case 14:return "{0x+,0x+,0x+,{0x+,0x+,0x+,0x+,0x+,0x+,0x+,0x+}}";
                case 15:return "{0x00000000000000000001,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 16:return "{0x100000000,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 17:return "{0x0,0x100000000,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 18:return "{0x0,0x0,0x100000000,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 19:return "{0x0,0x0,0x0,{0x100,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 20:return "{0x0,0x0,0x0,{0x100000000,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 21:return "{0x100000000g,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 22:return "{0x100g000000,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 23:return "{0x0,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x100}}";
                case 24:return "{0x0,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x}}";
                case 25:return "{0x0,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}";
                case 26:return "{0x0,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}junk";
                case 27:return "{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 28:return "{0x0,0x0,0x0,{0y0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 29:return "{0x,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}}";
                case 30:return "{0x0,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0}";
                case 31:return "00112233-4455-6677-8899-aabbccddeeff0";
                case 32:return "00112233-4455-6677-8899-aabbccddeef";
                case 33:return "001122334455-6677-8899-aabbccddeeff";
                case 34:return "00112233_4455-6677-8899-aabbccddeeff";
                case 35:return "00112233-4455_6677-8899-aabbccddeeff";
                case 36:return "00112233-4455-6677-8899-aabbccddeefg";
                case 37:return "{00112233-4455-6677-8899-aabbccddeeff)";
                case 38:return "(00112233-4455-6677-8899-aabbccddeeff}";
                case 39:return "{00112233445566778899aabbccddeeff}";
                case 40:return "(00112233445566778899aabbccddeeff)";
                case 41:return "00112233445566778899aabbccddeefg";
                case 42:return "+0112233445566778899aabbccddeeff";
                case 43:return "00112233-4455-6677-8899-aabb+cddeeff";
                case 44:return "00112233-4455-6677-8899-aabb0xddeeff";
                case 45:return "00112233-4455-6677-8899-aabbccddeef\0";
                case 46:return "\ufeff00112233-4455-6677-8899-aabbccddeeff";
                case 47:return "00112233-4455-6677-8899-aabbccddeeff\u200b";
                case 48:return "00112233-4455-6677-8899-aabbccddee\ud800\udfff";
                case 49:return "{0x0,0x0,0x0,{0x0,0x0,0x0,0x0,0x0,0x0,0x0,0x0},}";
                default:return "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF";
            }
        }
    }
}
