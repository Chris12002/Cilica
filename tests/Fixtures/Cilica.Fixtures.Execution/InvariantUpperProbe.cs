using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public static class InvariantUpperProbe
    {
        private static string ScalarText(int scalar)
        {
            if (scalar <= 0xffff) return new string(new char[] { (char)scalar });
            scalar -= 0x10000;
            return new string(new char[] { (char)(0xd800 + (scalar >> 10)), (char)(0xdc00 + (scalar & 1023)) });
        }
        private static int Hash(string value)
        {
            int hash = value.Length;
            for (int i = 0; i < value.Length; ++i) hash = unchecked(hash * 31 + value[i]);
            return hash;
        }
        public static int Scalar(int a, int b)
        {
            string upper = ScalarText(a).ToUpperInvariant();
            if (upper.Length == 1) return upper[0];
            return 0x10000 + ((upper[0] - 0xd800) << 10) + upper[1] - 0xdc00;
        }
        public static int Block(int a, int b)
        {
            int width = a <= 0xffff ? 1 : 2;
            var chars = new char[b * width];
            for (int i = 0; i < b; ++i)
            {
                int scalar = a + i;
                if (width == 1) chars[i] = (char)scalar;
                else
                {
                    scalar -= 0x10000;
                    chars[i * 2] = (char)(0xd800 + (scalar >> 10));
                    chars[i * 2 + 1] = (char)(0xdc00 + (scalar & 1023));
                }
            }
            return Hash(new string(chars).ToUpperInvariant());
        }
        public static int Identity(int a, int b)
        {
            string source;
            switch (a & 7)
            {
                case 0: source = ""; break;
                case 1: source = new string(new char[] { 'A', (char)(b & 31), 'Z' }); break;
                case 2: source = "A\0z"; break;
                case 3: source = "\u0130\u0131"; break;
                case 4: source = "\u00e9\u00df"; break;
                case 5: source = "\ud800"; break;
                case 6: source = "\ud801\udc00"; break;
                default: source = "A\ud800z\udfff"; break;
            }
            string first = source.ToUpperInvariant(), second = source.ToUpperInvariant();
            string third = first.ToUpperInvariant();
            return unchecked(Hash(source) + Hash(first) * 17 + ((object)source == (object)first ? 1 : 2)
                + ((object)first == (object)second ? 4 : 8) + ((object)first == (object)third ? 16 : 32));
        }
        public static int Mixed(int a, int b)
        {
            var chars = new char[] { 'I', '\u0130', '\u0131', '\u017f', '\u03a3', '\u03c2', '\0', (char)a,
                '\ud801', '\udc00', '\ud803', '\udd50', '\ud800', 'A', '\udfff', (char)b };
            string source = new string(chars), upper = source.ToUpperInvariant();
            chars[0] = 'Z';
            for (int i = 0; i < 5; ++i) _ = new string(chars).ToUpperInvariant();
            return unchecked(Hash(source) + Hash(upper));
        }
        public static int Null(int a, int b)
        {
            string source = a < 0 ? null : "ABC";
            try { return Hash(source.ToUpperInvariant()) + b; }
            catch (NullReferenceException) { return -71; }
        }
        public static int UnsupportedCulture(int a, int b) => "I".ToUpper(CultureInfo.InvariantCulture).Length;
        public static int UnsupportedCurrent(int a, int b) => "I".ToUpper().Length;
    }
}
