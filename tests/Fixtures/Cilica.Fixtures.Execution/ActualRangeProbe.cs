using System;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class ActualRangeProbe
    {
        private static int Observe(ArgumentOutOfRangeException fault)
        {
            string text = fault.Message; int hash = 17;
            for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]);
            return unchecked(hash + (fault.Message == text ? 7 : 13) + (fault.InnerException == null ? 19 : 23));
        }
        public static int Integer(int a, int b)
        {
            string name = b < 0 ? null : b == 0 ? "" : "index\0\ud800";
            string message = b < 0 ? null : b == 0 ? "" : "bad\0\udc00";
            return Observe(new ArgumentOutOfRangeException(name, a, message));
        }
        public static int Null(int a, int b)
        {
            string name = b < 0 ? null : b == 0 ? "" : "index";
            string message = a < 0 ? null : a == 0 ? "" : "bad";
            return Observe(new ArgumentOutOfRangeException(name, null, message));
        }
        public static int Caught(int a, int b)
        {
            var fault = new ArgumentOutOfRangeException("index", a, "bad");
            int result = 0;
            try { try { throw fault; } finally { result = b; } }
            catch (ArgumentOutOfRangeException caught) { return unchecked(result + Observe(caught) + (caught == fault ? 1 : 2)); }
        }
        public static int InterfaceClosure(int a, int b)
        {
            var fault = new ArgumentOutOfRangeException("index", a, "bad");
            return unchecked(Observe(fault) + new[] { a, b }.Count());
        }
        public static int Alias(int a, int b) { object value = a; return new ArgumentOutOfRangeException("i", value, "bad").Message.Length; }
        public static int WrongInteger(int a, int b) { return new ArgumentOutOfRangeException("i", (uint)a, "bad").Message.Length; }
        public static int Text(int a, int b) { return new ArgumentOutOfRangeException("i", "value", "bad").Message.Length; }
        public static int Getter(int a, int b) { return new ArgumentOutOfRangeException("i", a, "bad").ActualValue == null ? a : b; }
    }
}
