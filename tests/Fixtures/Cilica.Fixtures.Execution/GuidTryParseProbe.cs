using System;
namespace Cilica.Fixtures.Execution
{
    public static class GuidTryParseProbe
    {
        private static int Outcome(string text, int seed)
        {
            var value = GuidTextProbe.Value(seed, ~seed);
            bool success = Guid.TryParse(text, out value);
            return GuidTextProbe.Hash(value.ToString("D"), unchecked(seed * 31 + (success ? 1 : 0)));
        }

        public static int RoundTrip(int a, int b)
        {
            var original = GuidTextProbe.Value(a, b);
            Guid value;
            return Guid.TryParse(original.ToString(GuidTextProbe.Format(b)), out value) && value == original ? 1 : 0;
        }

        public static int Repeated(int a, int b)
        {
            var original = GuidTextProbe.Value(a, b);
            var value = original;
            int result = 0;
            for (int i = 0; i < 9; i++)
            {
                string text = i % 3 == 0 ? original.ToString("X") : i % 3 == 1 ? "invalid" : null;
                bool success = Guid.TryParse(text, out value);
                result = unchecked(result * 31 + (success ? 1 : 0));
                result = GuidTextProbe.Hash(value.ToString("N"), result);
            }
            return result;
        }

        public static int Storage(int a, int b)
        {
            var original = GuidTextProbe.Value(a, b);
            var holder = new GuidTextHolder(); holder.Value = original;
            var values = new Guid[1]; values[0] = original;
            bool first = Guid.TryParse(GuidTextProbe.Pick((int)((uint)a % 51)), out holder.Value);
            bool second = Guid.TryParse(GuidTextProbe.Pick((int)((uint)b % 51)), out values[0]);
            return GuidTextProbe.Hash(holder.Value.ToString("D"), GuidTextProbe.Hash(values[0].ToString("D"), (first ? 1 : 0) + (second ? 2 : 0)));
        }

        public static int Whitespace(int a, int b)
        {
            var characters = new char[1]; characters[0] = unchecked((char)a);
            string space = new string(characters);
            return Outcome(space + "00112233-4455-6677-8899-aabbccddeeff" + space, b);
        }

        public static int Mutation(int a, int b)
        {
            string text = GuidTextProbe.Value(a, b).ToString(GuidTextProbe.Format(a));
            var characters = new char[text.Length];
            for (int i = 0; i < text.Length; i++) characters[i] = text[i];
            characters[(int)((uint)a % (uint)text.Length)] = unchecked((char)b);
            return Outcome(new string(characters), a);
        }

        public static int Corpus(int a, int b) => Outcome(GuidTextProbe.Pick(a), b);
    }
}
