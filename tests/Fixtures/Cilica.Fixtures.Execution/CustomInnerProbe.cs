using System;
namespace Cilica.Fixtures.Execution
{
    public class CustomInnerFault : Exception { public CustomInnerFault(string message, Exception inner) : base(message, inner) { } }
    internal sealed class CustomInnerDerivedFault : CustomInnerFault { internal CustomInnerDerivedFault(string message, Exception inner) : base(message, inner) { } }
    public sealed class CustomInnerSideEffect : Exception { public CustomInnerSideEffect(string message, Exception inner) : base(message, inner) { if (message == null) throw new ArgumentException(); } }
    public sealed class CustomInnerReplaced : Exception { public CustomInnerReplaced(string message, Exception inner) : base(message, null) { } }
    public static class CustomInnerProbe
    {
        private static int Hash(string text) { int result = 17; for (int i = 0; i < text.Length; ++i) result = unchecked(result * 31 + text[i]); return result; }
        public static int Payload(int a, int b)
        {
            Exception inner = a < 0 ? null : new ArgumentException("inner");
            var outer = new CustomInnerDerivedFault(b < 0 ? null : b == 0 ? "" : "A\0\ud800\ud83d\ude00", inner);
            return unchecked(Hash(outer.Message) + (outer.InnerException == inner ? 17 : 0) + (outer.InnerException == null ? 31 : Hash(outer.InnerException.Message)));
        }
        public static int Chain(int a, int b)
        {
            var leaf = new DivideByZeroException("leaf"); var middle = new CustomInnerFault("middle", leaf);
            var outer = new CustomInnerDerivedFault("outer", middle);
            return outer.InnerException == middle && middle.InnerException == leaf && leaf.InnerException == null ? unchecked(Hash(outer.Message) + Hash(middle.Message) + a) : b;
        }
        public static int Catch(int a, int b)
        {
            try { try { return a / b; } catch (Exception inner) { throw new CustomInnerDerivedFault("compiled", inner); } }
            catch (CustomInnerFault outer) { return outer.InnerException is DivideByZeroException ? 23 : outer.InnerException is OverflowException ? 29 : 31; }
        }
        public static int Filter(int a, int b)
        {
            int trace = 0;
            try { try { throw new CustomInnerDerivedFault("outer", a < 0 ? new DivideByZeroException() : new OverflowException()); } finally { trace = 7; } }
            catch (Exception outer) when (outer is CustomInnerFault) { return unchecked(trace + (outer.InnerException is DivideByZeroException ? b : Hash(outer.InnerException.Message))); }
        }
        public static int Rethrow(int a, int b)
        {
            Exception inner = new ArgumentException("inner"); var outer = new CustomInnerFault("outer", inner);
            try { try { throw outer; } catch (CustomInnerFault) { if (a < 0) throw; return b; } }
            catch (Exception caught) { return caught == outer && caught.InnerException == inner ? 37 : 41; }
        }
        public static int Base(int a, int b)
        {
            var inner = a < 0 ? null : new Exception("inner"); var outer = new Exception(b < 0 ? null : "outer", inner);
            return unchecked(Hash(outer.Message) + (outer.InnerException == inner ? 43 : 47));
        }
        private static string Message(ref int trace, int a) { trace = 1; if (a < 0) throw new ArgumentException(); return "outer"; }
        private static Exception Inner(ref int trace, int b) { trace = trace * 10 + 2; if (b < 0) throw new InvalidOperationException(); return new Exception("inner"); }
        public static int Evaluation(int a, int b)
        {
            int trace = 0;
            try { var outer = new CustomInnerFault(Message(ref trace,a), Inner(ref trace,b)); return trace + Hash(outer.InnerException.Message); }
            catch (ArgumentException) { return trace + 100; } catch (InvalidOperationException) { return trace + 200; }
        }
        public static int UnsupportedSideEffect(int a, int b) => new CustomInnerSideEffect("outer", new Exception()).Message.Length;
        public static int UnsupportedReplaced(int a, int b) => new CustomInnerReplaced("outer", new Exception()).Message.Length;
    }
}
