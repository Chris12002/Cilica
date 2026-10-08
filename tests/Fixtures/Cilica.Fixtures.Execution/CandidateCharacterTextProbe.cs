using System;
namespace Cilica.Fixtures.Execution
{
    // Independent semantic reduction; no candidate source is executed or copied.
    public static class CandidateCharacterTextProbe
    {
        public static int Units(int first, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                char value = unchecked((char)(first + i));
                string instance = value.ToString();
                string again = value.ToString();
                string direct = char.ToString(value);
                if (instance.Length != 1 || instance[0] != value || again.Length != 1 || again[0] != value
                    || direct.Length != 1 || direct[0] != value) throw new InvalidOperationException("payload");
                if (ReferenceEquals(instance, again) || ReferenceEquals(instance, direct)
                    || ReferenceEquals(again, direct) || ReferenceEquals(instance, string.Empty))
                    throw new InvalidOperationException("identity");
            }
            return count;
        }
        private static string FromParameter(char value) => value.ToString();
        private static string FromBorrow(ref char value) => value.ToString();
        public static int Aliases(int input, int replacement)
        {
            char value = unchecked((char)input);
            var first = FromBorrow(ref value);
            value = unchecked((char)replacement);
            var second = FromBorrow(ref value);
            var parameter = FromParameter(unchecked((char)input));
            if (first.Length != 1 || second.Length != 1 || parameter.Length != 1
                || ReferenceEquals(first, parameter) || ReferenceEquals(first, second)) return -1;
            if (first[0] != unchecked((char)input) || parameter[0] != first[0]) return -2;
            return second[0];
        }
        public static int Roots(int input, int query)
        {
            char value = unchecked((char)input);
            var saved = new string[] { value.ToString(), char.ToString(value) };
            value = unchecked((char)(value + 1));
            var later = new string(new[] { value, 'x' });
            if (ReferenceEquals(saved[0], saved[1]) || ReferenceEquals(saved[0], "a")) return -1;
            if (query == 0) return saved[0][0];
            if (query == 1) return saved[1][0];
            return later[0];
        }
        private static char Argument(ref int order, int value)
        { order = order * 10 + 1; if (value < 0) throw new ArgumentException("character"); return unchecked((char)value); }
        public static int FaultOrder(int input, int mode)
        {
            int order = 0;
            try
            {
                string text = mode == 0 ? Argument(ref order, input).ToString() : char.ToString(Argument(ref order, input));
                order = order * 10 + 2;
                return order * 100000 + text[0];
            }
            catch (ArgumentException error) { return error.Message == "character" ? order : -1; }
        }
        public static int UnsupportedProvider(int input, int unused) => unchecked((char)input).ToString((IFormatProvider)null).Length;
        public static int UnsupportedBox(int input, int unused) => ((object)unchecked((char)input)).ToString().Length;
        public static int UnsupportedArrayBorrow(int input, int unused) => new[] { unchecked((char)input) }[0].ToString().Length;
        public sealed class Cell { public char Value; }
        public static int UnsupportedFieldBorrow(int input, int unused) => new Cell { Value = unchecked((char)input) }.Value.ToString().Length;
    }
}
