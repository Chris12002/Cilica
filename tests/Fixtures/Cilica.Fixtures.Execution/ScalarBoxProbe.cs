using System;
namespace Cilica.Fixtures.Execution
{
    public enum BoxEnum { Zero, One }
    public enum OtherBoxEnum { Zero, One }
    public static class ScalarBoxProbe
    {
        private static object Box<T>(T value) => value;
        private static T Read<T>(object value) => (T)value;
        public static int Int(int a, int b)
        {
            int value = unchecked((int)a);
            object first = Box(value), second = Box(value);
            var copy = Read<int>(first);
            int? optional = a == 0 ? default(int?) : value;
            object boxed = Box(optional);
            var again = Read<int?>(boxed);
            var present = Read<int?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int UInt(int a, int b)
        {
            uint value = unchecked((uint)a);
            object first = Box(value), second = Box(value);
            var copy = Read<uint>(first);
            uint? optional = a == 0 ? default(uint?) : value;
            object boxed = Box(optional);
            var again = Read<uint?>(boxed);
            var present = Read<uint?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int Long(int a, int b)
        {
            long value = unchecked((long)(((long)a << 32) | (uint)b));
            object first = Box(value), second = Box(value);
            var copy = Read<long>(first);
            long? optional = a == 0 ? default(long?) : value;
            object boxed = Box(optional);
            var again = Read<long?>(boxed);
            var present = Read<long?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int ULong(int a, int b)
        {
            ulong value = unchecked((ulong)(((long)a << 32) | (uint)b));
            object first = Box(value), second = Box(value);
            var copy = Read<ulong>(first);
            ulong? optional = a == 0 ? default(ulong?) : value;
            object boxed = Box(optional);
            var again = Read<ulong?>(boxed);
            var present = Read<ulong?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int Byte(int a, int b)
        {
            byte value = unchecked((byte)a);
            object first = Box(value), second = Box(value);
            var copy = Read<byte>(first);
            byte? optional = a == 0 ? default(byte?) : value;
            object boxed = Box(optional);
            var again = Read<byte?>(boxed);
            var present = Read<byte?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int SByte(int a, int b)
        {
            sbyte value = unchecked((sbyte)a);
            object first = Box(value), second = Box(value);
            var copy = Read<sbyte>(first);
            sbyte? optional = a == 0 ? default(sbyte?) : value;
            object boxed = Box(optional);
            var again = Read<sbyte?>(boxed);
            var present = Read<sbyte?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int Short(int a, int b)
        {
            short value = unchecked((short)a);
            object first = Box(value), second = Box(value);
            var copy = Read<short>(first);
            short? optional = a == 0 ? default(short?) : value;
            object boxed = Box(optional);
            var again = Read<short?>(boxed);
            var present = Read<short?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int UShort(int a, int b)
        {
            ushort value = unchecked((ushort)a);
            object first = Box(value), second = Box(value);
            var copy = Read<ushort>(first);
            ushort? optional = a == 0 ? default(ushort?) : value;
            object boxed = Box(optional);
            var again = Read<ushort?>(boxed);
            var present = Read<ushort?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int Char(int a, int b)
        {
            char value = unchecked((char)a);
            object first = Box(value), second = Box(value);
            var copy = Read<char>(first);
            char? optional = a == 0 ? default(char?) : value;
            object boxed = Box(optional);
            var again = Read<char?>(boxed);
            var present = Read<char?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int Bool(int a, int b)
        {
            bool value = a < b;
            object first = Box(value), second = Box(value);
            var copy = Read<bool>(first);
            bool? optional = a == 0 ? default(bool?) : value;
            object boxed = Box(optional);
            var again = Read<bool?>(boxed);
            var present = Read<bool?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((copy ? 1 : 0) + b) : unchecked(a - b);
        }
        public static int Enum(int a, int b)
        {
            BoxEnum value = unchecked((BoxEnum)a);
            object first = Box(value), second = Box(value);
            var copy = Read<BoxEnum>(first);
            BoxEnum? optional = a == 0 ? default(BoxEnum?) : value;
            object boxed = Box(optional);
            var again = Read<BoxEnum?>(boxed);
            var present = Read<BoxEnum?>(first);
            return first != second && first.GetType() == second.GetType()
                && present.HasValue && present.Value == value && again.HasValue == optional.HasValue
                && again.GetValueOrDefault() == optional.GetValueOrDefault()
                ? unchecked((int)copy + b) : unchecked(a - b);
        }
        public static int EnumCompatibility(int a, int b)
        {
            object primitive = a, enumeration = (BoxEnum)b, other = (OtherBoxEnum)a;
            return unchecked((int)(BoxEnum)primitive + (int)enumeration + (int)(OtherBoxEnum)enumeration + (int)(BoxEnum)other);
        }
        public static int ExactNullableEnum(int a, int b)
        {
            object primitive = a, enumeration = (BoxEnum)b, other = (OtherBoxEnum)a;
            int errors = 0;
            try { _ = (BoxEnum?)primitive; } catch (InvalidCastException) { errors++; }
            try { _ = (int?)enumeration; } catch (InvalidCastException) { errors++; }
            try { _ = (OtherBoxEnum?)enumeration; } catch (InvalidCastException) { errors++; }
            try { _ = (BoxEnum?)other; } catch (InvalidCastException) { errors++; }
            return unchecked(errors + ((int?)primitive).Value + (int)((BoxEnum?)enumeration).Value);
        }
        public static int NominalIdentity(int a, int b)
        {
            object integer = a, unsigned = unchecked((uint)a), enumeration = (BoxEnum)a, other = (OtherBoxEnum)a;
            return integer.GetType() != unsigned.GetType() && integer.GetType() != enumeration.GetType()
                && enumeration.GetType() != other.GetType() ? a : b;
        }
        public static int WrongScalarTypes(int a, int b)
        {
            int errors = 0;
            object unsigned = unchecked((uint)a), signed = a, small = unchecked((byte)b), flag = true;
            try { _ = (int)unsigned; } catch (InvalidCastException e) { if (e.Message == "Unable to cast object of type 'System.UInt32' to type 'System.Int32'.") errors++; }
            try { _ = (uint)signed; } catch (InvalidCastException) { errors++; }
            try { _ = (sbyte)small; } catch (InvalidCastException) { errors++; }
            try { _ = (byte)flag; } catch (InvalidCastException) { errors++; }
            try { _ = (int)(object)null; } catch (NullReferenceException) { errors++; }
            return unchecked(errors + a + b);
        }
        public static int ArrayLifetime(int a, int b)
        {
            var values = new object[3];
            values[0] = a; values[1] = unchecked(((long)a << 32) | (uint)b); values[2] = (BoxEnum)b;
            var noise = new NullableNode(b);
            return unchecked((int)values[0] + (int)((long)values[1] >> 32) + (int)(BoxEnum)values[2] + noise.Value);
        }
        public static int UnsupportedScalarTypeTest(int a, int b) { object value = (long)a; return value is int ? a : b; }
        public static int UnsupportedNullableTypeTest(int a, int b) { object value = a; return (value as int?).HasValue ? a : b; }
        public static int UnsupportedInterfaceTest(int a, int b) { object value = a; return value is IComparable ? a : b; }
        public static int UnsupportedInterfaceCast(int a, int b) { object value = a; return (IComparable)value == null ? a : b; }
        public static int WideBits(int a, int b)
        {
            ulong expected = ((ulong)(uint)a << 32) | (uint)b;
            object boxed = expected;
            ulong copy = (ulong)boxed;
            ulong? optional = (ulong?)boxed;
            return copy == expected && optional.Value == expected ? a : b;
        }
    }
}
