using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public static class StringWhitespaceProbe
    {
        public static int Mask(int first, int unused)
        {
            int mask = 0;
            for (int i = 0; i < 32; i++)
            {
                string value = new StringBuilder().Append((char)(first + i)).ToString();
                if (string.IsNullOrWhiteSpace(value)) mask |= 1 << i;
            }
            return mask;
        }
        public static int Strings(int input, int mode)
        {
            string value = null;
            if (input == 0) value = "";
            if (input == 1) value = "\t\r\n \u0085\u00a0\u1680\u2000\u200a\u2028\u2029\u202f\u205f\u3000";
            if (input == -1) value = "\u3000x\t";
            if (input == 127) value = "\u200b";
            if (input == 32768) value = "\ufeff";
            if (input == int.MinValue) value = "\ud800\udfff";
            if (mode == 1) value = string.Concat("\t", value);
            if (mode == 2) value = string.Concat(value, "\0");
            if (mode == 3) value = string.Concat("X", value);
            return string.IsNullOrWhiteSpace(value) ? 1 : 0;
        }
    }
}
