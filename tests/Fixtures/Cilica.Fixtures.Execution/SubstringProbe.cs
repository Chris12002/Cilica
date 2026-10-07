using System;
namespace Cilica.Fixtures.Execution
{
    public static class SubstringProbe
    {
        private static string Text(int a) => a < 0 ? "" : a == 0 ? "abc" : "A\0\ud800\udfff\udc00Z";
        private static int Hash(string text)
        {
            int hash = text.Length;
            for (int i = 0; i < text.Length; ++i) hash = unchecked(hash * 31 + text[i]);
            return hash;
        }
        private static int Fault(Exception error)
        {
            int hash = error is ArgumentOutOfRangeException ? 11 : error is NullReferenceException ? 13 : 17;
            hash = unchecked(hash * 31 + Hash(error.Message));
            var argument = error as ArgumentException;
            if (argument != null && argument.ParamName != null) hash = unchecked(hash * 31 + Hash(argument.ParamName));
            return hash;
        }
        public static int Tail(int a, int b)
        {
            try { return Hash(Text(a).Substring(b)); } catch (Exception e) { return Fault(e); }
        }
        public static int Range(int a, int b)
        {
            try { return Hash("A\0\ud800\udfff\udc00Z".Substring(a, b)); } catch (Exception e) { return Fault(e); }
        }
        public static int Empty(int a, int b)
        {
            try { return Hash("".Substring(a, b)); } catch (Exception e) { return Fault(e); }
        }
        public static int Identity(int a, int b)
        {
            string source = Text(a);
            int result = (object)source == (object)source.Substring(0) ? 1 : 0;
            result += (object)source == (object)source.Substring(0, source.Length) ? 2 : 0;
            result += (object)string.Empty == (object)source.Substring(source.Length) ? 4 : 0;
            result += (object)string.Empty == (object)source.Substring(source.Length, 0) ? 8 : 0;
            if (source.Length > 1) result += (object)source.Substring(1) == (object)source.Substring(1) ? 16 : 32;
            return unchecked(result + b);
        }
        public static int NullReceiver(int a, int b)
        {
            string source = null;
            try { return Hash(a < 0 ? source.Substring(b) : source.Substring(a, b)); } catch (Exception e) { return Fault(e); }
        }
        public static int Composed(int a, int b)
        {
            string source = Text(a);
            for (int i = 0; i < 5; ++i)
            {
                string extended = string.Concat(source, "x");
                source = extended.Substring(0, extended.Length - 1);
            }
            return unchecked(Hash(source) + b);
        }
    }
}
