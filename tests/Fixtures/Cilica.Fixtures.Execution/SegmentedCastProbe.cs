using System;
namespace Cilica.Fixtures.Execution
{
    public static class SegmentedCastProbe
    {
        public static Exception Create(int a, int b)
        {
            object source = a > b ? new ValueVectorBox(a) : (object)new CastNameOuter.NestedValue { Number = a };
            try { return (Exception)source; }
            catch (InvalidCastException error) { return error; }
        }
        public static int Message(int a, int b)
        {
            var error = Create(a, b); var first = error.Message;
            var pressure = new ValueVectorBox(b); var second = error.Message;
            return unchecked(first.Length + pressure.Number + (ReferenceEquals(first, second) ? 3 : 17));
        }
    }
}
