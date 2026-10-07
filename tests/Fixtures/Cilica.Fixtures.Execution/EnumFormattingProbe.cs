using System;
namespace Cilica.Fixtures.Execution
{
    public enum TextEnum { Eight = 8, Zero = 0, ΔOne = 1, Two = 2, Pair = 3, Four = 4, High = int.MinValue, All = -1, Max = int.MaxValue }
    public enum EmptyTextEnum { }
    [Flags] public enum FlagTextEnum { One = 1, Two = 2 }
    public enum AliasTextEnum { First = 1, Second = 1 }
    [Obsolete] public enum AttributedTextEnum { One = 1 }
    public enum WideTextEnum : long { One = 1 }
    public static class EnumFormattingProbe
    {
        private static int Observe(string value)
        { int hash = 17; for (int i = 0; i < value.Length; i++) hash = unchecked(hash * 31 + value[i]); return hash; }
        private static string Pattern(int mode)
        {
            if (mode == 0) return "<{0}>";
            if (mode == 1) return "{0:G}/{0:g}";
            if (mode == 2) return "{0:D}/{0:d}";
            if (mode == 3) return "{0:X}/{0:x}";
            if (mode == 4) return "{0:F}/{0:f}";
            if (mode == 5) return "{0,16}/{0,-16:f}";
            if (mode == 6) return "{{{0:}}}\0\ud800";
            if (mode == 7) return "{0:Q}";
            if (mode == 8) return "{0:GG}";
            if (mode == 9) return "{0: }";
            if (mode == 10) return "{0:\0}";
            if (mode == 11) return "{0:F2}";
            return "no placeholders";
        }
        public static int Names(int a, int b)
        { try { return Observe(string.Format(Pattern(b), (TextEnum)a)); } catch (FormatException e) { return Observe(e.Message); } }
        public static int Empty(int a, int b)
        { try { return Observe(string.Format(Pattern(b), (EmptyTextEnum)a)); } catch (FormatException e) { return Observe(e.Message); } }
        public static int Pair(int a, int b)
        { return Observe(string.Format("{1:x}/{0:G}/{1:f}/{0:D}", (TextEnum)a, (EmptyTextEnum)b)); }
        public static int Mixed(int a, int b)
        {
            var other = new FormattingValue(); other.Text = "ordinary";
            return Observe(string.Format("{1:Q}/{0:f}/{1:GG}/{0:X}", (TextEnum)a, other)) + other.Calls;
        }
        public static int MixedFirst(int a, int b)
        {
            var other = new FormattingValue(); other.Text = "ordinary";
            return Observe(string.Format("{1:f}/{0:Q}/{1:X}/{0:GG}", other, (TextEnum)a)) + other.Calls;
        }
        public static int FaultOrder(int a, int b)
        {
            string format = b == 0 ? "{1:Q}" : b == 1 ? "{0:Q}{" : b == 2 ? "{0:F}{" : b == 3 ? "{0:{Q}}" : b == 4 ? "{0,+2:Q}" : null;
            try { return Observe(string.Format(format, (TextEnum)a)); }
            catch (FormatException e) { return Observe(e.Message); }
            catch (ArgumentNullException e) { return Observe(e.Message); }
        }
        public static int UnsupportedFlags(int a, int b) => string.Format("{0}", (FlagTextEnum)a).Length;
        public static int UnsupportedAliases(int a, int b) => string.Format("{0}", (AliasTextEnum)a).Length;
#pragma warning disable CS0612
        public static int UnsupportedAttributes(int a, int b) => string.Format("{0}", (AttributedTextEnum)a).Length;
#pragma warning restore CS0612
        public static int UnsupportedWide(int a, int b) => string.Format("{0}", (WideTextEnum)a).Length;
        public static int UnsupportedAlias(int a, int b) { object value = (TextEnum)a; return string.Format("{0}", value).Length; }
    }
}
