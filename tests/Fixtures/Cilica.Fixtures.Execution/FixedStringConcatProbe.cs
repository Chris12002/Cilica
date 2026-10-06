using System;
namespace Cilica.Fixtures.Execution
{
    public static class FixedStringConcatProbe
    {
        private static string Piece(int code, int b)
        {
            int kind = code % 3;
            if (kind == 0) return null;
            if (kind == 1) return "";
            return b < 0 ? string.Concat("é\0", "\ud800") : string.Concat("\udc00", "\ud83d\ude00");
        }
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Text3(int a, int b) => Hash(string.Concat(Piece(a, b), Piece(a / 3, b + 1), Piece(a / 9, b - 1)));
        public static int Text4(int a, int b) => Hash(string.Concat(Piece(a, b), Piece(a / 3, b + 1), Piece(a / 9, b - 1), Piece(a / 27, b)));
        public static int Identity3(int a, int b)
        {
            string first = Piece(a, b), second = Piece(a / 3, b), third = Piece(a / 9, b);
            string result = string.Concat(first, second, third);
            return (((object)result == (object)first) ? 1 : 0) + (((object)result == (object)second) ? 3 : 0)
                + (((object)result == (object)third) ? 7 : 0) + (((object)result == (object)"") ? 11 : 0);
        }
        public static int Identity4(int a, int b)
        {
            string first = Piece(a, b), second = Piece(a / 3, b), third = Piece(a / 9, b), fourth = Piece(a / 27, b);
            string result = string.Concat(first, second, third, fourth);
            return (((object)result == (object)first) ? 1 : 0) + (((object)result == (object)second) ? 3 : 0)
                + (((object)result == (object)third) ? 7 : 0) + (((object)result == (object)fourth) ? 11 : 0)
                + (((object)result == (object)"") ? 17 : 0);
        }
        public static int Repeated(int a, int b)
        {
            string text = Piece(2, b);
            for (int i = 0; i < (a & 7); i++) text = string.Concat("[", text, Piece(a / 3, b), "]");
            return Hash(text);
        }
        public static int FaultMessage(int a, int b)
        {
            try { throw new InvalidOperationException(string.Concat("mode:", Piece(a, b), ":", Piece(a / 3, b))); }
            catch (InvalidOperationException e) { return Hash(e.Message); }
        }
    }
}
