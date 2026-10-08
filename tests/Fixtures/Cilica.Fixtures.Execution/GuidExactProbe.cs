using System;
namespace Cilica.Fixtures.Execution
{
    public static class GuidExactProbe
    {
        private static string Format(int value)
        {
            switch ((uint)value % 17)
            {
                case 0: return "D"; case 1: return "d"; case 2: return "N"; case 3: return "n";
                case 4: return "B"; case 5: return "b"; case 6: return "P"; case 7: return "p";
                case 8: return "X"; case 9: return "x"; case 10: return null; case 11: return "";
                case 12: return " D"; case 13: return "DD"; case 14: return "Z";
                case 15: return "\ud800"; default: return "\0";
            }
        }
        private static int Outcome(string text, string format, int seed)
        {
            var value = GuidTextProbe.Value(seed, ~seed);
            bool success = Guid.TryParseExact(text, format, out value);
            return GuidTextProbe.Hash(value.ToString("D"), unchecked(seed * 31 + (success ? 1 : 0)));
        }
        public static int RoundTrip(int a, int b)
        {
            string text = GuidTextProbe.Value(a, b).ToString(GuidTextProbe.Format(a));
            return Outcome(text, Format(b), a);
        }
        public static int Storage(int a, int b)
        {
            var original = GuidTextProbe.Value(a, b);
            var holder = new GuidTextHolder(); holder.Value = original;
            var values = new Guid[1]; values[0] = holder.Value;
            bool first = Guid.TryParseExact(GuidTextProbe.Pick((int)((uint)a % 51)), Format(b), out holder.Value);
            bool second = Guid.TryParseExact(GuidTextProbe.Pick((int)((uint)b % 51)), Format(a), out values[0]);
            return GuidTextProbe.Hash(holder.Value.ToString("D"), GuidTextProbe.Hash(values[0].ToString("D"), (first ? 1 : 0) + (second ? 2 : 0)));
        }
        public static int Repeated(int a, int b)
        {
            var value = GuidTextProbe.Value(a, b);
            string text = value.ToString("N");
            int hash = b;
            for (int i = 0; i < 9; i++)
            {
                bool success = Guid.TryParseExact(i % 3 == 0 ? text : i % 3 == 1 ? "bad" : null, "N", out value);
                hash = GuidTextProbe.Hash(value.ToString("D"), unchecked(hash * 31 + (success ? 1 : 0)));
            }
            return hash;
        }
        public static int Whitespace(int a, int b)
        {
            var characters = new char[1]; characters[0] = unchecked((char)a);
            string space = new string(characters);
            string text = GuidTextProbe.Value(a, b).ToString(GuidTextProbe.Format(b));
            return Outcome(space + text + space, GuidTextProbe.Format(b), b);
        }
        public static int Mutation(int a, int b)
        {
            string format = GuidTextProbe.Format(a);
            string text = GuidTextProbe.Value(a, b).ToString(format);
            var characters = new char[text.Length];
            for (int i = 0; i < text.Length; i++) characters[i] = text[i];
            characters[(int)((uint)a % (uint)text.Length)] = unchecked((char)b);
            return Outcome(new string(characters), format, a);
        }
        public static int Corpus(int a, int b) => Outcome(GuidTextProbe.Pick(a), Format(b), b);
    }
}
