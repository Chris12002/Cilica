using System;
namespace Cilica.Fixtures.Execution
{
    public static class OrdinalStringProbe
    {
        private static string Text(int value)
        {
            if (value == 0) return null;
            if (value == 1) return "";
            if (value == -1) return "A";
            if (value == 127) return "a";
            if (value == 32768) return "\ud800\0\udfff";
            if (value == int.MinValue) return "i\u0130";
            return string.Concat("\ud800", "\0\udfff");
        }
        public static int Equality(int input, int mode)
        {
            string left = Text(input), right = Text(mode);
            return (string.Equals(left, right, StringComparison.Ordinal) ? 1 : 0)
                + (string.Equals(left, left, StringComparison.Ordinal) ? 2 : 0)
                + (string.Equals(left, null, StringComparison.Ordinal) ? 4 : 0);
        }
        public static int IgnoreCase(int input, int mode) => string.Equals("a", "A", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        public static int Culture(int input, int mode) => string.Equals("a", "a", StringComparison.CurrentCulture) ? 1 : 0;
        public static int Unknown(int input, int mode) => string.Equals("a", "a", (StringComparison)99) ? 1 : 0;
        public static int Alias(int input, int mode) => string.Equals("a", "a", (StringComparison)mode) ? 1 : 0;
    }
}
