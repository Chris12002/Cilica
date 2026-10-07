using System;
namespace Cilica.Fixtures.Execution
{
    public class DerivedDisposedFault : ObjectDisposedException
    { public DerivedDisposedFault(string name) : base(name) { } }
    public static class ObjectDisposedProbe
    {
        private static string Name(int a) => (a & 3) == 0 ? null : (a & 3) == 1 ? "" : (a & 3) == 2 ? "object" : new string(new[] { 'x', '\0', '\ud800', '\udfff' });
        private static string Text(int a) => (a & 3) == 0 ? null : (a & 3) == 1 ? "" : (a & 3) == 2 ? "message" : "custom\0\ud800";
        public static int Default(int a, int b)
        {
            var name = Name(a); var error = new ObjectDisposedException(name);
            string text = error.Message;
            return b < 0 ? text.Length : b < text.Length ? text[b] : -1;
        }
        public static int Message(int a, int b)
        {
            var error = new ObjectDisposedException(Name(a), Text(a >> 2));
            string text = error.Message;
            return b < 0 ? text.Length : b < text.Length ? text[b] : -1;
        }
        public static int Payload(int a, int b)
        {
            var name = Name(a); var message = Text(b);
            var error = new ObjectDisposedException(name, message);
            var copy = error.ObjectName;
            int result = ReferenceEquals(copy, name ?? string.Empty) ? 1 : -100;
            var first = error.Message; var second = error.Message;
            result += ReferenceEquals(first, second) ? 3 : 5;
            result += error.ObjectName.Length * 7;
            try { throw error; }
            catch (InvalidOperationException caught)
            {
                if (!ReferenceEquals(caught, error)) return -101;
                result += caught.Message.Length * 11;
            }
            return result + (error.InnerException == null ? 13 : -102);
        }
        public static int Catch(int a, int b)
        {
            ObjectDisposedException error = null; int result = 0;
            try
            {
                try { throw new ObjectDisposedException(Name(a)); }
                catch (ObjectDisposedException caught) { error = caught; throw; }
                finally { result = 3; }
            }
            catch (Exception caught) { result += ReferenceEquals(caught, error) ? 5 : -100; }
            return result + error.ObjectName.Length * 7 + error.Message.Length * 11;
        }
        public static int Null(int a, int b)
        {
            ObjectDisposedException error = null; int result = 0;
            try { result = error.ObjectName.Length; } catch (NullReferenceException) { result += 1; }
            try { result = error.Message.Length; } catch (NullReferenceException) { result += 2; }
            return result;
        }
        public static int ResourceSet(int a, int b) => new FormatException().Message.Length
            + new System.Collections.Generic.KeyNotFoundException().Message.Length
            + new ObjectDisposedException(Name(a)).Message.Length;
        public static int UnsupportedInner(int a, int b) => new ObjectDisposedException("message", new Exception()).Message.Length;
        public static int UnsupportedDerived(int a, int b) => new DerivedDisposedFault("name").Message.Length;
    }
}
