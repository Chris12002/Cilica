using System;
namespace Cilica.Fixtures.Execution
{
    public struct FormattingValue : IEquatable<FormattingValue>
    {
        public int Calls;
        public string Text;
        public bool Equals(FormattingValue other) => Text == other.Text;
        public override string ToString()
        {
            Calls++;
            if (Calls == 2) return string.Concat(Text, "second");
            return Text;
        }
    }
    public readonly struct GenericFormattingValue<T>
    {
        public readonly T Value;
        public GenericFormattingValue(T value) { Value = value; }
        public override string ToString() => (object)Value == null ? "null" : "generic";
    }
    public struct ThrowingFormattingValue
    {
        public override string ToString() { throw new InvalidOperationException("callback"); }
    }
    public struct CustomFormattingValue : IFormattable
    {
        public string ToString(string format, IFormatProvider provider) => "custom";
        public override string ToString() => "ordinary";
    }
    public static class BoxedFormattingProbe
    {
        private static int Observe(string text)
        {
            int hash = 17;
            for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]);
            return hash;
        }
        private static string Pattern(int mode)
        {
            if (mode == 0) return "<{0}>";
            if (mode == 1) return "{0,8}/{0,-9:ignored}";
            if (mode == 2) return "{{{0}}}/{0:}";
            if (mode == 3) return "plain\0\ud800";
            if (mode == -1) return "";
            if (mode == int.MinValue) return "{00 , -12 :anything}";
            return "{0}{0}{0}";
        }
        public static int Text(int input, int mode)
        {
            string text = input < 0 ? null : "a\0\ud800";
            var value = new FormattingValue(); value.Text = text;
            return Observe(string.Format(Pattern(mode), value)) + value.Calls;
        }
        private static string BadPattern(int mode)
        {
            if (mode == 0) return "{";
            if (mode == 1) return "}";
            if (mode == 2) return "{x}";
            if (mode == 3) return "{1}";
            if (mode == -1) return "{0,+2}";
            if (mode == int.MinValue) return "{0:{bad}}";
            return null;
        }
        public static int Generic(int input, int mode)
        {
            string text = input < 0 ? null : "value";
            var value = new GenericFormattingValue<string>(text);
            return Observe(string.Format("<{0, 10}>/{0:ignored}", value));
        }
        public static int Faults(int input, int mode)
        {
            var value = new ThrowingFormattingValue();
            try { return Observe(string.Format(BadPattern(mode), value)); }
            catch (ArgumentNullException e) { return Observe(e.Message); }
            catch (FormatException e) { return Observe(e.Message); }
        }
        public static int Callback(int input, int mode)
        {
            var value = new ThrowingFormattingValue();
            string format = mode < 0 ? "{0}{" : "before{0}";
            try { return Observe(string.Format(format, value)); }
            catch (InvalidOperationException e) { return Observe(e.Message); }
        }
        public static int UnsupportedJoin(int input, int mode)
        {
            return string.Format("{0}", mode < 0 ? null : (object)new FormattingValue()).Length;
        }
        public static int UnsupportedAlias(int input, int mode)
        {
            object value = new FormattingValue(); return string.Format("{0}", value).Length;
        }
        public static int UnsupportedCustom(int input, int mode)
        {
            return string.Format("{0}", new CustomFormattingValue()).Length;
        }
    }
}
