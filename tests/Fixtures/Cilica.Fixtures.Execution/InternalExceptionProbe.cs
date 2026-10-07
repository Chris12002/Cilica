using System;
namespace Cilica.Fixtures.Execution
{
    internal class InternalFault : Exception
    {
        internal InternalFault() { }
        internal InternalFault(string message) : base(message) { }
    }
    internal sealed class InternalChildFault : InternalFault
    {
        internal InternalChildFault(string message) : base(message) { }
    }
    public sealed class PublicInternalConstructorFault : Exception
    {
        internal PublicInternalConstructorFault(string message) : base(message) { }
    }
    internal sealed class InternalMessageFault : InvalidOperationException
    {
        internal InternalMessageFault(string value) : base("Unsupported '" + value + "'.") { }
    }
    public static class InternalExceptionProbe
    {
        private static string Text(int a) => a < 0 ? null : a == 0 ? "" : "message";
        private static int Hash(string text) { int h = 0; for (int i = 0; i < text.Length; i++) h = unchecked(h * 31 + text[i]); return h; }
        public static int Message(int a, int b) => unchecked(Hash(new InternalFault(Text(a)).Message) + b);
        public static int Default(int a, int b) => unchecked(Hash(new InternalFault().Message) + b);
        public static int Inheritance(int a, int b)
        {
            try { throw new InternalChildFault(Text(a)); }
            catch (InternalFault error) { return unchecked(Hash(error.Message) + b); }
        }
        public static int PublicOwner(int a, int b) => unchecked(Hash(new PublicInternalConstructorFault(Text(a)).Message) + b);
        public static int Identity(int a, int b)
        {
            var value = new InternalFault(Text(a));
            try { try { throw value; } catch (InternalFault) { throw; } }
            catch (Exception error) { return object.ReferenceEquals(error, value) && error is InternalFault ? b : a; }
        }
        public static int ConstructedMessage(int a, int b) { try { throw new InternalMessageFault(Text(a)); } catch (InvalidOperationException error) { return unchecked(Hash(error.Message) + b); } }
    }
}
