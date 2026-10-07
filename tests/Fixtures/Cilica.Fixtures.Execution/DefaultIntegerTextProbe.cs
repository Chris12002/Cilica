using System;
namespace Cilica.Fixtures.Execution
{
    public static class DefaultIntegerTextProbe
    {
        public static int FormatSigned32(int input, int position)
        {
            int value = unchecked((int)LibraryFormattingProbe.SignedCase(input));
            string text = value.ToString();
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int FormatUnsigned32(int input, int position)
        {
            uint value = unchecked((uint)LibraryFormattingProbe.UnsignedCase(input));
            string text = value.ToString();
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int FormatSigned64(int input, int position)
        {
            long value = LibraryFormattingProbe.SignedCase(input);
            string text = value.ToString();
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int FormatUnsigned64(int input, int position)
        {
            ulong value = LibraryFormattingProbe.UnsignedCase(input);
            string text = value.ToString();
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int IntegerHeapText(int input, int mode)
        {
            var holder = new CoreNumberHolder(((long)input << 32) | (uint)mode);
            string text = holder.Value.ToString();
            return text.Length + text[(int)((uint)mode % (uint)text.Length)];
        }
        public static int IntegerText(int input, int mode)
        {
            int signed = input;
            uint unsigned = (uint)mode;
            long wide = ((long)input << 32) | (uint)mode;
            ulong unsignedWide = unchecked((ulong)wide + (uint)input);
            string a = signed.ToString();
            string b = unsigned.ToString();
            string c = wide.ToString();
            string d = unsignedWide.ToString();
            string all = string.Concat(string.Concat(a, b), string.Concat(c, d));
            int hash = 0;
            for (int i = 0; i < all.Length; ++i) hash = unchecked(hash * 31 + all[i]);
            return hash;
        }
        public static int IntegerTextIdentity(int input, int mode)
        {
            uint value = (uint)input % 301;
            string a = value.ToString();
            string b = value.ToString();
            int signed = (int)value;
            long wide = value;
            ulong unsignedWide = value;
            string c = signed.ToString();
            string d = wide.ToString();
            string e = unsignedWide.ToString();
            string canonical = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            object literal = value == 0 ? "0" : value == 1 ? "1" : "299";
            return ((object)a == b ? 1 : 2) + ((object)a == c ? 4 : 8) + ((object)a == d ? 16 : 32)
                + ((object)a == e ? 64 : 128) + ((object)a == literal ? 256 : 512) + ((object)a == canonical ? 1024 : 2048) + mode;
        }
    }
}
