namespace Cilica.Fixtures.Execution
{
    public sealed class EmbeddingStringOther { public int Value; }
    public static class EmbeddingStringProbe
    {
        public static string Echo(string value) => value;
        public static string Append(string value) => value + "\0\ud800\udfff";
        public static int Length(string value) => value.Length;
        public static int Unit(string value, int index) => value[index];
        public static EmbeddingStringOther Other() => new EmbeddingStringOther();
        public static int Root(int mode, int input)
        {
            string text = Echo("probe");
            if (mode == 0) return Length(Append(text));
            if (mode == 1) return Unit(text, input);
            return Other().Value;
        }
    }
}
