using System;

namespace Cilica.Fixtures.Execution
{
    public static class CoreLibraryProbe
    {
        public static int InvariantIdentity(int input, int mode)
        {
            object first = System.Globalization.CultureInfo.InvariantCulture;
            IFormatProvider provider = System.Globalization.CultureInfo.InvariantCulture;
            string text = string.Concat(input < 0 ? "negative" : "positive", mode < 0 ? "left" : "right");
            object second = System.Globalization.CultureInfo.InvariantCulture;
            return (first == second ? 11 : 17) + (first == provider ? 23 : 29)
                + (provider is System.Globalization.CultureInfo ? 31 : 37) + text.Length;
        }
        public static int UnsupportedCulture(int input, int mode) => System.Globalization.CultureInfo.InvariantCulture.Name.Length + input + mode;
        public static int UnsupportedHash(int input, int mode) => "text".GetHashCode() ^ input ^ mode;
        public static int Text(int input, int mode)
        {
            string first = input < 0 ? "A\0\ud800" : "Z\ud83d\ude00";
            string second = mode < 0 ? null : "\udfffB";
            string result = string.Concat(first, second);
            int index = (int)((uint)input % (uint)result.Length);
            return result.Length + result[index] + (result == first ? 11 : 13)
                + (string.Equals(first, result) ? 17 : 19) + (result != second ? 23 : 29);
        }
        public static int Fault(int input, int mode)
        {
            try
            {
                if (mode < 0) throw new ArgumentNullException("value", "missing");
                if (mode == 0) throw new ArgumentOutOfRangeException("value", "outside");
                if (input < 0) throw new ArgumentException("wrong", "value");
                return input / (mode - 1);
            }
            catch (ArgumentException error) { return error.Message.Length + error.ParamName.Length; }
            catch (DivideByZeroException error) { return error.Message.Length + 31; }
        }
        public static int Formatting(int input, int mode)
        {
            long value = ((long)input << 32) | (uint)mode;
            string text = Cilica.Library.IntegerFormatting.Int64Invariant(value);
            return text.Length + text[(int)((uint)mode % (uint)text.Length)];
        }
    }
}
