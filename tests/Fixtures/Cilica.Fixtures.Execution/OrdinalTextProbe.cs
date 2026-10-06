using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public static class OrdinalTextProbe
    {
        public static string Pick(int index)
        {
            switch (index)
            {
                case 0: return null;
                case 1: return "";
                case 2: return "a";
                case 3: return "ab";
                case 4: return "abc";
                case 5: return "abcd";
                case 6: return "abcde";
                case 7: return "abcdf";
                case 8: return "\0";
                case 9: return "\0A";
                case 10: return "a\0";
                case 11: return "a\0x";
                case 12: return "\ud800";
                case 13: return "\udfff";
                case 14: return "\ud800\udc00";
                case 15: return "\uffff";
                case 16: return "abcdefghijklmno";
                case 17: return "abcdefghijklmnop";
                case 18: return "abcdefghijklmnq";
                default: return "abcdefghi\0klmno";
            }
        }
        public static int Compare(int input, int mode) => string.CompareOrdinal(Pick(input), Pick(mode));
        public static int Empty(int input, int mode) => (string.IsNullOrEmpty(Pick(input)) ? 1 : 0) + (string.IsNullOrEmpty(Pick(mode)) ? 2 : 0);
        public static int Units(int input, int mode)
        {
            var first = new StringBuilder(); first.Append('a'); first.Append((char)input);
            var second = new StringBuilder(); second.Append('a'); second.Append((char)mode);
            return string.CompareOrdinal(first.ToString(), second.ToString());
        }
        public static int Identity(int input, int mode)
        {
            var builder = new StringBuilder(); builder.Append('x'); builder.Append((char)input); builder.Append((char)mode);
            string first = builder.ToString(), copy = builder.ToString();
            if ((object)first == (object)copy) return -100;
            return string.CompareOrdinal(first, copy) + string.CompareOrdinal(first, first);
        }
    }
}
