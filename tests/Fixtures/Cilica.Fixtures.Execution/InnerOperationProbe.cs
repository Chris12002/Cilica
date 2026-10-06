using System;
namespace Cilica.Fixtures.Execution
{
    public static class InnerOperationProbe
    {
        public static int Payload(int a, int b)
        {
            Exception inner = a < 0 ? null! : new ArgumentException("inner");
            var outer = new InvalidOperationException(b < 0 ? null : "outer", inner);
            return outer.Message.Length + (outer.InnerException == inner ? 17 : 0)
                + (outer.InnerException == null ? 31 : outer.InnerException.Message.Length);
        }
        public static int Chain(int a, int b)
        {
            var leaf = new DivideByZeroException("leaf");
            var middle = new InvalidOperationException("middle", leaf);
            var outer = new InvalidOperationException("outer", middle);
            return outer.InnerException == middle && outer.InnerException.InnerException == leaf
                && leaf.InnerException == null ? outer.Message.Length + middle.Message.Length + a : b;
        }
        public static int Catch(int a, int b)
        {
            try
            {
                try { return a / b; }
                catch (Exception inner) { throw new InvalidOperationException("comparison", inner); }
            }
            catch (InvalidOperationException outer)
            {
                return outer.InnerException is DivideByZeroException ? 23 : outer.InnerException is OverflowException ? 29 : 31;
            }
        }
        public static int Filter(int a, int b)
        {
            int trace = 0;
            try
            {
                try { throw new InvalidOperationException("sort", a < 0 ? new DivideByZeroException() : new OverflowException()); }
                finally { trace = 7; }
            }
            catch (Exception outer) when (outer is InvalidOperationException)
            { return trace + (outer.InnerException is DivideByZeroException ? b : outer.InnerException.Message.Length); }
        }
        public static int Rethrow(int a, int b)
        {
            Exception inner = new ArgumentException("inner");
            var outer = new InvalidOperationException("outer", inner);
            try { try { throw outer; } catch (InvalidOperationException) { if (a < 0) throw; return b; } }
            catch (Exception caught) { return caught == outer && caught.InnerException == inner ? 37 : 41; }
        }
        public static int NullRead(int a, int b)
        {
            Exception value = a < 0 ? null! : new InvalidOperationException(null, null);
            try { return value.InnerException == null ? b : a; } catch (NullReferenceException) { return 43; }
        }
        public static int CodeUnit(int a, int b)
        {
            var outer = new InvalidOperationException(a < 0 ? null : a == 0 ? "" : "A\0\ud800\ud83d\ude00", new Exception("inner"));
            string message = outer.Message;
            return b >= 0 && b < message.Length ? message[b] : message.Length;
        }
        public static int UnsupportedInnerFilter(int a, int b)
        {
            try { throw new InvalidOperationException("outer", new DivideByZeroException()); }
            catch (Exception outer) when (outer.InnerException is DivideByZeroException) { return a; }
        }
        public static int UnsupportedBase(int a, int b) { return new Exception("outer", new Exception()).Message.Length; }
        public static int UnsupportedArgument(int a, int b) { return new ArgumentException("outer", new Exception()).Message.Length; }
    }
}
