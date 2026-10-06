using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class FormatObserver { public int Calls; }
    public struct ObservedFormattingValue
    {
        public FormatObserver Observer;
        public int Calls;
        public override string ToString()
        {
            Calls++;
            Observer.Calls = unchecked(Observer.Calls * 3 + Calls);
            return Calls == 1 ? "first\0\ud800" : "again";
        }
    }
    public static class PairFormattingProbe
    {
        private static int Observe(string text)
        {
            int hash = 17;
            for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]);
            return hash;
        }
        private static string Pattern(int mode)
        {
            if (mode == 0) return "<{0}>/{1}";
            if (mode == 1) return "{1,12:ignored}/{0,-9}/{1}";
            if (mode == 2) return "{{{1}}}/{0:}";
            if (mode == 3) return "plain\0\ud800";
            if (mode == -1) return "";
            if (mode == int.MinValue) return "{01 , -12 :anything}/{00}";
            return "{1}{0}{1}{0}";
        }
        public static int Different(int a, int b)
        {
            string leftText = a < 0 ? null : "left"; string rightText = b < 0 ? null : "right";
            var left = new FormattingValue(); left.Text = leftText;
            var right = new GenericFormattingValue<string>(rightText);
            return unchecked(Observe(string.Format(Pattern(b), left, right)) + left.Calls);
        }
        public static int Copies(int a, int b)
        {
            string text = a < 0 ? null : "shared"; var value = new FormattingValue(); value.Text = text;
            return unchecked(Observe(string.Format(Pattern(b), value, value)) + value.Calls);
        }
        public static int Callbacks(int a, int b)
        {
            var first = new FormatObserver(); first.Calls = a;
            var second = new FormatObserver(); second.Calls = b;
            var left = new ObservedFormattingValue(); left.Observer = first;
            var right = new ObservedFormattingValue(); right.Observer = second;
            var text = string.Format("{1}/{0}/{1}/{0}/{0}", left, right);
            return unchecked(Observe(text) + first.Calls * 7 + second.Calls * 13 + left.Calls + right.Calls);
        }
        private static FormattingValue Returned(int value)
        { string text = value < 0 ? "negative" : "positive"; var result = new FormattingValue(); result.Text = text; return result; }
        public static int ReturnValue(int a, int b)
        {
            string text = a < 0 ? null : "a"; var first = new GenericFormattingValue<string>(text);
            return Observe(string.Format(Pattern(b), first, Returned(b)));
        }
        public static int Faults(int a, int b)
        {
            var first = new FormattingValue(); first.Text = "first";
            var second = new ThrowingFormattingValue();
            string pattern = b == 0 ? "{2}" : b == 1 ? "{0}{" : b == 2 ? "{1}" : b == 3 ? "}" : b == -1 ? "{0,+2}" : b == int.MinValue ? "{0:{bad}}" : null;
            try { return Observe(string.Format(pattern, first, second)); }
            catch (ArgumentNullException e) { return Observe(e.Message); }
            catch (FormatException e) { return Observe(e.Message); }
            catch (InvalidOperationException e) { return Observe(e.Message); }
        }
        public static int UnsupportedFirstAlias(int a, int b)
        { object first = new FormattingValue(); return string.Format("{0}{1}", first, new FormattingValue()).Length; }
        public static int UnsupportedLastAlias(int a, int b)
        { object second = new FormattingValue(); return string.Format("{0}{1}", new FormattingValue(), second).Length; }
        public static int UnsupportedJoin(int a, int b)
        { return string.Format("{0}{1}", a < 0 ? null : (object)new FormattingValue(), new FormattingValue()).Length; }
        public static int UnsupportedFirstCustom(int a, int b)
        { return string.Format("{0}{1}", new CustomFormattingValue(), new FormattingValue()).Length; }
        public static int UnsupportedLastCustom(int a, int b)
        { return string.Format("{0}{1}", new FormattingValue(), new CustomFormattingValue()).Length; }
    }
}
