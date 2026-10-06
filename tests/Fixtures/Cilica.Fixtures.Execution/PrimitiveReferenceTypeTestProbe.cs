using System;
namespace Cilica.Fixtures.Execution
{
    public static class PrimitiveReferenceTypeTestProbe
    {
        public sealed class Marker { public int Number; public Marker(int number) { Number = number; } }
        private static T As<T>(object value) where T : class => value as T;
        private static T Cast<T>(object value) where T : class => (T)value;
        public static int String(int a, int b)
        {
            object value = a < 0 ? null : (a & 1) == 0 ? (object)string.Concat("a", "b") : new Marker(b);
            string text = As<string>(value);
            int result = text == null ? 11 : text.Length + ((object)text == value ? 17 : 19);
            try { string cast = Cast<string>(value); return result + ((object)cast == value ? 23 : 29); }
            catch (InvalidCastException) { return result + 31; }
        }
        public static int Object(int a, int b)
        {
            object value = a < 0 ? null : (a & 1) == 0 ? (object)string.Concat("a", "b") : new Marker(b);
            object result = As<object>(value); object cast = Cast<object>(value);
            return (result == value ? 37 : 41) + ((object)cast == value ? 43 : 47);
        }
    }
}
