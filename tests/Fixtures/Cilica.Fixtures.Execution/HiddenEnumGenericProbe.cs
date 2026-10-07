using System;
namespace Cilica.Fixtures.Execution
{
    public static class EnumGenericTransport<T> where T : struct
    {
        public static T Copy(T value) => value;
        public static object Box(T value) => value;
        public static T? RoundTrip(T? value) { object boxed = value; return (T?)boxed; }
    }
    internal struct HiddenGenericCell { public int Value; }
    public static class HiddenEnumGenericProbe
    {
        private enum Phase { None = 0, First = 1, Second = 2 }
        public static int StructCopy(int a, int b) => EnumGenericTransport<HiddenGenericCell>.Copy(new HiddenGenericCell { Value = a }).Value;
        public static int InternalHolder(int a, int b) => InternalEnumProbe.Generic(a, b);
        public static int NestedHolder(int a, int b) => NestedEnumOwner.Generic(a, b);
        public static int Nullable(int a, int b)
        {
            PreparationFlags? value = a < 0 ? (PreparationFlags?)null : (PreparationFlags)b;
            var copied = EnumGenericTransport<PreparationFlags>.RoundTrip(value);
            return copied.HasValue ? (int)copied.Value : a;
        }
        public static int PrivateNullable(int a, int b)
        {
            Phase? value = a < 0 ? (Phase?)null : (Phase)b;
            var copied = EnumGenericTransport<Phase>.RoundTrip(value);
            return copied.HasValue ? (int)copied.Value : a;
        }
        public static int PrivateBox(int a, int b)
        {
            object boxed = EnumGenericTransport<Phase>.Box((Phase)a);
            return unchecked((int)(Phase)boxed + (int)EnumGenericTransport<Phase>.Copy((Phase)b));
        }
    }
}
