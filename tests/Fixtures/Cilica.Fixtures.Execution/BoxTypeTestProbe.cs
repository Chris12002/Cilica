using System;
namespace Cilica.Fixtures.Execution
{
    public struct TestEnvelope<T> { public T Value; }
    public static class BoxTypeTestProbe
    {
        private static int Test<T>(T payload, int a, int b) where T : struct
        {
            object present = payload;
            object optional = a == 0 ? default(T?) : payload;
            object wrong = new NullableNode(b);
            var copy = present as T?;
            var absent = wrong as T?;
            return present is T && present is T? && copy.HasValue
                && (optional is T) == (a != 0) && (optional is T?) == (a != 0)
                && !(wrong is T) && !absent.HasValue ? a : b;
        }
        public static int Int(int a, int b) => Test(a, a, b);
        public static int UInt(int a, int b) => Test(unchecked((uint)a), a, b);
        public static int Long(int a, int b) => Test(((long)a << 32) | (uint)b, a, b);
        public static int ULong(int a, int b) => Test(((ulong)(uint)a << 32) | (uint)b, a, b);
        public static int Byte(int a, int b) => Test(unchecked((byte)a), a, b);
        public static int SByte(int a, int b) => Test(unchecked((sbyte)a), a, b);
        public static int Short(int a, int b) => Test(unchecked((short)a), a, b);
        public static int UShort(int a, int b) => Test(unchecked((ushort)a), a, b);
        public static int Char(int a, int b) => Test(unchecked((char)a), a, b);
        public static int Bool(int a, int b) => Test(a < b, a, b);
        public static int Enum(int a, int b) => Test((BoxEnum)a, a, b);

        public static int GuidValue(int a, int b) => Test(new Guid(a, unchecked((short)b), 17, 1, 2, 3, 4, 5, 6, 7, 8), a, b);
        public static int EnumIdentity(int a, int b)
        {
            object integer = a, enumeration = (BoxEnum)b, other = (OtherBoxEnum)a;
            return integer is int && !(integer is BoxEnum) && !(integer is BoxEnum?)
                && enumeration is BoxEnum && enumeration is BoxEnum? && !(enumeration is int) && !(enumeration is int?)
                && !(enumeration is OtherBoxEnum) && !(enumeration is OtherBoxEnum?)
                && !(other is BoxEnum) && !(other is BoxEnum?) ? a : b;
        }
        public static int ReferencePayload(int a, int b)
        {
            object box = new NullablePayload(new NullableNode(a), b);
            var copy = box as NullablePayload?;
            box = null;
            var noise = new NullableNode(b);
            return copy.HasValue ? unchecked(copy.Value.Node.Value + copy.Value.Value + noise.Value) : a;
        }
        public static int ClosedValue(int a, int b)
        {
            object box = new TestEnvelope<NullableId> { Value = new NullableId(a) };
            var copy = box as TestEnvelope<NullableId>?;
            return box is TestEnvelope<NullableId> && !(box is TestEnvelope<NullableWide>?) && copy.HasValue ? copy.Value.Value.Value : b;
        }
        public static int TestOnly(int a, int b)
        {
            object value = new NullableNode(a);
            return value is int || value is BoxEnum || value is NullableId || value is NullablePayload? ? a : b;
        }
        public static int NullOnly(int a, int b)
        {
            object value = null;
            return value is long || value is BoxEnum? || value is NullableEmpty? ? a : b;
        }
        public static int UnsupportedFilter(int a, int b)
        {
            object value = a == 0 ? null : (object)(BoxEnum)a;
            try { throw new InvalidOperationException(); }
            catch (InvalidOperationException) when (value is BoxEnum?) { return a; }
            catch (InvalidOperationException) { return b; }
        }
        public static int EmptyPayload(int a, int b) => Test(new NullableEmpty(), a, b);
        public static int NominalMismatch(int a, int b)
        {
            object unsigned = unchecked((uint)a), character = unchecked((char)a), flag = a < b;
            return !(unsigned is int) && !(unsigned is int?) && !(character is ushort) && !(character is ushort?)
                && !(flag is byte) && !(flag is byte?) ? a : b;
        }
    }
}
