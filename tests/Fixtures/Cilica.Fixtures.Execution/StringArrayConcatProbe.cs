using System;
namespace Cilica.Fixtures.Execution
{
    public static class StringArrayConcatProbe
    {
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Text(int a, int b)
        {
            string[] source = new[] { "Board '", a < 0 ? null : "\uD800\0", "' is not in '", b < 0 ? "" : "\uDC00é", "'." };
            string value = string.Concat(source); source[0] = "changed";
            return Hash(value);
        }
        public static int SingleIdentity(int a, int b)
        {
            string item = a < 0 ? null : string.Concat("fi", "rst");
            string value = string.Concat(new[] { item });
            return unchecked(Hash(value) + ((object)value == (object)(item ?? "") ? 7 : -100) + b);
        }
        public static int MultiIdentity(int a, int b)
        {
            string item = string.Concat("fi", "rst"); string[] source = new[] { a < 0 ? null : "", item, b < 0 ? null : "" };
            string value = string.Concat(source);
            return Hash(value) + ((object)value == (object)item ? -100 : 7);
        }
        public static int EmptyAndNull(int a, int b)
        {
            string[] source = a < 0 ? null : new string[b & 3];
            try { string value = string.Concat(source); return (object)value == (object)"" ? 7 : -100; }
            catch (ArgumentNullException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int Repeated(int a, int b)
        {
            int count = (a & 31) + 1; var values = new string[count];
            for (int i = 0; i < count; i++) values[i] = (i & 1) == 0 ? "\0\uD800" : "é\uDC00";
            return Hash(string.Concat(values)) + b;
        }
        public static int NativeMessage(int a, int b)
        {
            try { throw new InvalidOperationException(string.Concat(new[] { "Board '", a < 0 ? null : "forest", "' is not in catalogue '", b < 0 ? null : "core", "'." })); }
            catch (InvalidOperationException e) { return Hash(e.Message); }
        }
        public static int TooLong(int a, int b)
        {
            string part = "01234567890123456789012345678901";
            for (int i = 0; i < 10; i++) part = string.Concat(part, part);
            var values = new string[65536]; for (int i = 0; i < values.Length; i++) values[i] = part;
            try { return Hash(string.Concat(values)); }
            catch (OutOfMemoryException e) { return Hash(e.Message); }
        }
        public static int UnsupportedObjects(int a, int b) => string.Concat(new object[] { "a", "b" }).Length;
    }
}
