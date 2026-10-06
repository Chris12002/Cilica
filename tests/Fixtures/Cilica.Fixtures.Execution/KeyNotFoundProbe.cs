using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class KeyNotFoundProbe
    {
        private static int Observe(string text)
        {
            int hash = 17;
            for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]);
            return hash;
        }
        public static int Default(int a, int b)
        { return unchecked(Observe(new KeyNotFoundException().Message) + a + b); }
        public static int Message(int a, int b)
        {
            string message = b == 0 ? null : b < 0 ? "" : "missing\0\ud800";
            var fault = new KeyNotFoundException(message);
            return unchecked(Observe(fault.Message) + a + (message != null && fault.Message == message ? 3 : 0));
        }
        public static int Catch(int a, int b)
        {
            int result = 0;
            try
            {
                try { if (a < 0) throw new InvalidOperationException("other"); throw new KeyNotFoundException(b < 0 ? null : "key"); }
                catch (KeyNotFoundException e) { result = Observe(e.Message); throw; }
                finally { result = unchecked(result + 7); }
            }
            catch (KeyNotFoundException e) { return unchecked(result + Observe(e.Message)); }
            catch (SystemException e) { return unchecked(result + Observe(e.Message) + 11); }
        }
        public static int ConstructedMessage(int a, int b)
        {
            var first = new FormattingValue(); first.Text = "player";
            var second = new GenericFormattingValue<string>("match");
            var fault = new KeyNotFoundException(string.Format("Player '{0}' is not part of match '{1}'.", first, second));
            try { throw fault; } catch (KeyNotFoundException caught) { return unchecked(Observe(caught.Message) + (caught == fault ? a : b)); }
        }
        public static int UnsupportedInner(int a, int b)
        { return new KeyNotFoundException("key", new Exception("inner")).Message.Length; }
    }
}
