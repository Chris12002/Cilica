using System;
namespace Cilica.Fixtures.Execution
{
    public class WrappedMessageFault : Exception
    {
        public WrappedMessageFault(string value) : base("prefix:" + value + ":suffix") { }
    }
    internal sealed class DerivedWrappedMessageFault : WrappedMessageFault
    {
        internal DerivedWrappedMessageFault(string value) : base(value) { }
    }
    public sealed class Utf16MessageFault : InvalidOperationException
    {
        public Utf16MessageFault(string value) : base("\0\uD800:" + value + ":\uDC00\uFFFF") { }
    }
    public sealed class SideEffectMessageFault : Exception
    {
        public SideEffectMessageFault(string value) : base("p" + value + "s") { MessageConcat.Count++; }
    }
    public sealed class WrongBaseMessageFault : ArgumentException
    {
        public WrongBaseMessageFault(string value) : base("p" + value + "s") { }
    }
    public sealed class LookalikeMessageFault : Exception
    {
        public LookalikeMessageFault(string value) : base(MessageConcat.Concat("p", value, "s")) { }
    }
    public static class MessageConcat { public static int Count; public static string Concat(string a, string b, string c) => a + b + c; }
    public static class ExceptionMessageProbe
    {
        private static int order;
        private static string Text(int a) => a < 0 ? null : a == 0 ? "" : a == 1 ? "\0" : "\uD800x\uDC00";
        private static string Argument(int a) { order = 7; if (a == int.MinValue) throw new InvalidOperationException("argument"); return Text(a); }
        private static int Hash(string text) { int result = 0; for (int i = 0; i < text.Length; i++) result = unchecked(result * 31 + text[i]); return result; }
        public static int Plain(int a, int b) => unchecked(Hash(new WrappedMessageFault(Text(a)).Message) + b);
        public static int Derived(int a, int b) => unchecked(Hash(new DerivedWrappedMessageFault(Text(a)).Message) + b);
        public static int Utf16(int a, int b) => unchecked(Hash(new Utf16MessageFault(Text(a)).Message) + b);
        public static int Two(int a, int b)
        {
            var first = new WrappedMessageFault(Text(a)); var second = new WrappedMessageFault(Text(b));
            return unchecked(Hash(first.Message) + Hash(second.Message));
        }
        public static int Evaluation(int a, int b)
        {
            order = 0;
            try { var value = new WrappedMessageFault(Argument(a)); return unchecked(Hash(value.Message) + order + b); }
            catch (InvalidOperationException error) { return unchecked(Hash(error.Message) + order + b); }
        }
        public static int UnsupportedSideEffect(int a, int b) { throw new SideEffectMessageFault(Text(a)); }
        public static int UnsupportedBase(int a, int b) { throw new WrongBaseMessageFault(Text(a)); }
        public static int UnsupportedLookalike(int a, int b) { throw new LookalikeMessageFault(Text(a)); }
    }
}
