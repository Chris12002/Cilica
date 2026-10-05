using System;

namespace Cilica.Fixtures.Execution
{
    public sealed class CoreNumberHolder
    {
        public long Value;
        public CoreNumberHolder(long value) { Value = value; }
    }
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
        public static int FormatSigned32(int input, int position)
        {
            int value = unchecked((int)LibraryFormattingProbe.SignedCase(input));
            string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int FormatUnsigned32(int input, int position)
        {
            uint value = unchecked((uint)LibraryFormattingProbe.UnsignedCase(input));
            string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int FormatSigned64(int input, int position)
        {
            long value = LibraryFormattingProbe.SignedCase(input);
            string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int FormatUnsigned64(int input, int position)
        {
            ulong value = LibraryFormattingProbe.UnsignedCase(input);
            string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int IntegerHeapText(int input, int mode)
        {
            var holder = new CoreNumberHolder(((long)input << 32) | (uint)mode);
            string text = holder.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return text.Length + text[(int)((uint)mode % (uint)text.Length)];
        }
        public static int IntegerText(int input, int mode)
        {
            int signed = input;
            uint unsigned = (uint)mode;
            long wide = ((long)input << 32) | (uint)mode;
            ulong unsignedWide = unchecked((ulong)wide + (uint)input);
            string a = signed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string b = unsigned.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string c = wide.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string d = unsignedWide.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string all = string.Concat(string.Concat(a, b), string.Concat(c, d));
            int hash = 0;
            for (int i = 0; i < all.Length; ++i) hash = unchecked(hash * 31 + all[i]);
            return hash;
        }
        public static int IntegerTextIdentity(int input, int mode)
        {
            uint value = (uint)input % 301;
            string a = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string b = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int signed = (int)value;
            long wide = value;
            ulong unsignedWide = value;
            string c = signed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string d = wide.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string e = unsignedWide.ToString(System.Globalization.CultureInfo.InvariantCulture);
            object literal = value == 0 ? "0" : value == 1 ? "1" : "299";
            return ((object)a == b ? 1 : 2) + ((object)a == c ? 4 : 8) + ((object)a == d ? 16 : 32)
                + ((object)a == e ? 64 : 128) + ((object)a == literal ? 256 : 512) + mode;
        }
        public static int UnsupportedProvider(int input, int mode) => input.ToString((IFormatProvider)null).Length + mode;
        public static int AliasedProvider(int input, int mode) => WithProvider(input, System.Globalization.CultureInfo.InvariantCulture) + mode;
        private static int WithProvider(int input, IFormatProvider provider) => input.ToString(provider).Length;
        public static int IntegerHashes(int input, int mode)
        {
            int signed = input;
            uint unsigned = (uint)mode;
            long wide = ((long)input << 32) | (uint)mode;
            ulong unsignedWide = unchecked((ulong)wide + (uint)input);
            int first = signed.GetHashCode() ^ unsigned.GetHashCode();
            int second = wide.GetHashCode() + unsignedWide.GetHashCode();
            signed = mode;
            return first + second + signed.GetHashCode();
        }
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
