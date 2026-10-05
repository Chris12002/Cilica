using System;

namespace Cilica.Fixtures.Execution
{
    public static class GuidProbe
    {
        public static Guid Make(int input, int mode) => new Guid(input, unchecked((short)mode), unchecked((short)(input >> 16)),
            (byte)mode, (byte)(mode >> 8), (byte)(input >> 8), (byte)(input >> 24), (byte)(mode >> 16), (byte)(mode >> 24), (byte)input, (byte)(input ^ mode));
        public static int Identity(int input, int mode)
        {
            var first = Make(input, mode);
            var copy = first;
            var unsigned = new Guid((uint)input, unchecked((ushort)mode), unchecked((ushort)(input >> 16)),
                (byte)mode, (byte)(mode >> 8), (byte)(input >> 8), (byte)(input >> 24), (byte)(mode >> 16), (byte)(mode >> 24), (byte)input, (byte)(input ^ mode));
            return first.GetHashCode() + (first.Equals(copy) ? 11 : 13) + (first == unsigned ? 17 : 19)
                + (first != Guid.Empty ? 23 : 29) + (default(Guid) == Guid.Empty ? 31 : 37);
        }
        public static int Compare(int input, int mode)
        {
            var first = Make(input, mode);
            var second = Make(mode, input);
            return first.CompareTo(second) * 31 + first.CompareTo(first) + second.CompareTo(first);
        }
        public static int CompareFields(int input, int mode)
        {
            var first = new Guid(int.MinValue, short.MinValue, short.MinValue, 128, 128, 128, 128, 128, 128, 128, 128);
            var second = ChangedField(input, mode);
            return first.CompareTo(second) * 31 + second.CompareTo(first);
        }
        // Keep this library probe inside the existing borrow-flow contract:
        // constructor addresses crossing conditional joins are a separate gate.
        private static Guid ChangedField(int input, int mode)
        {
            int a = input == 0 ? mode : int.MinValue;
            short b = input == 1 ? unchecked((short)mode) : short.MinValue;
            short c = input == 2 ? unchecked((short)mode) : short.MinValue;
            byte d = input == 3 ? (byte)mode : (byte)128, e = input == 4 ? (byte)mode : (byte)128;
            byte f = input == 5 ? (byte)mode : (byte)128, g = input == 6 ? (byte)mode : (byte)128;
            byte h = input == 7 ? (byte)mode : (byte)128, i = input == 8 ? (byte)mode : (byte)128;
            byte j = input == 9 ? (byte)mode : (byte)128, k = input == 10 ? (byte)mode : (byte)128;
            return new Guid(a, b, c, d, e, f, g, h, i, j, k);
        }
        public static int BaseExceptionMessages(int input, int mode)
        {
            string message = input < 0 ? null : input == 0 ? "" : "explicit";
            Exception error = (uint)mode % 3 == 0 ? new SystemException(message)
                : (uint)mode % 3 == 1 ? new ArithmeticException(message) : new FormatException(message);
            return ObserveMessage(error);
        }
        public static int DefaultExceptionMessages(int input, int mode)
        {
            Exception error = (uint)input % 3 == 0 ? new SystemException()
                : (uint)input % 3 == 1 ? new ArithmeticException() : new FormatException();
            return ObserveMessage(error) + mode;
        }
        private static int ObserveMessage(Exception error)
        {
            string message = error.Message;
            int hash = (object)message == error.Message ? 17 : 31;
            for (int i = 0; i < message.Length; ++i) hash = unchecked(hash * 31 + message[i]);
            return hash;
        }
        public static string Format(int index)
        {
            if (index == 0) return null;
            if (index == 1) return "";
            if (index == 2) return "D";
            if (index == 3) return "d";
            if (index == 4) return "N";
            if (index == 5) return "n";
            if (index == 6) return "B";
            if (index == 7) return "b";
            if (index == 8) return "P";
            if (index == 9) return "p";
            if (index == 10) return "X";
            if (index == 11) return "x";
            if (index == 12) return "Q";
            return "DD";
        }
        public static int Text(int input, int position)
        {
            int seed = input / 16;
            var value = seed == 0 ? Guid.Empty : seed == 1 ? Make(unchecked((int)0x89abcdef), 0x12345678) : Make(-1, -1);
            string text;
            try { text = value.ToString(Format(input & 15)); }
            catch (FormatException error) { text = error.Message; }
            return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        }
        public static int ProviderAndDefault(int input, int mode)
        {
            var value = Make(input, mode);
            string first = value.ToString();
            string second = value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
            return (first == second ? 11 : 13) + first.Length + second[(int)((uint)mode % (uint)second.Length)];
        }
        public static int Boxed(int input, int mode)
        {
            var value = Make(input, mode);
            object boxed = value;
            return value.Equals(boxed) ? input : mode;
        }
        public static int Parse(int input, int mode) => Guid.Parse("00000000-0000-0000-0000-000000000000").GetHashCode() + input + mode;
    }
}
