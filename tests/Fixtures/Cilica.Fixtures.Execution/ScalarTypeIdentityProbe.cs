using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public enum IdentityEnum { Low = -1, Zero = 0, High = 7 }
    public enum OtherIdentityEnum { Zero = 0, High = 7 }
    public static class ScalarTypeIdentityProbe
    {
        public static int Primitives(int a, int b)
        {
            int result = typeof(int) == a.GetType() ? 1 : 0;
            if (typeof(uint) == ((uint)a).GetType()) result += 2;
            if (typeof(long) == ((long)a).GetType()) result += 4;
            if (typeof(ulong) == ((ulong)(long)a).GetType()) result += 8;
            if (typeof(bool) == (a < b).GetType()) result += 16;
            if (typeof(char) == ((char)a).GetType()) result += 32;
            if (typeof(byte) == ((byte)a).GetType()) result += 64;
            if (typeof(sbyte) == ((sbyte)a).GetType()) result += 128;
            if (typeof(short) == ((short)a).GetType()) result += 256;
            if (typeof(ushort) == ((ushort)a).GetType()) result += 512;
            return result;
        }
        public static int Enums(int a, int b)
        {
            object value = (IdentityEnum)a;
            int result = typeof(IdentityEnum) == value.GetType() ? 1 : 0;
            if (typeof(IdentityEnum) != typeof(int)) result += 2;
            if (typeof(IdentityEnum) != typeof(OtherIdentityEnum)) result += 4;
            if (typeof(OtherIdentityEnum) == ((OtherIdentityEnum)b).GetType()) result += 8;
            return result;
        }
        public static int LiteralOnly(int a, int b)
        {
            int result = typeof(IdentityEnum) != typeof(OtherIdentityEnum) ? a : b;
            if (Type.GetTypeFromHandle(typeof(IdentityEnum).TypeHandle) == typeof(IdentityEnum)) result = unchecked(result + 3);
            if (typeof(int) != typeof(uint) && typeof(long) != typeof(ulong)) result = unchecked(result + 7);
            return result;
        }
        private static Type Closed<T>() => typeof(T);
        public static int Generic(int a, int b)
        {
            int result = Closed<IdentityEnum>() == typeof(IdentityEnum) ? a : b;
            if (Closed<int>() != Closed<uint>()) result = unchecked(result + 5);
            return result;
        }
        public static int Dictionary(int a, int b)
        {
            var values = new Dictionary<Type, int>();
            values.TryAdd(typeof(IdentityEnum), a); values.TryAdd(typeof(OtherIdentityEnum), b);
            values.TryAdd(typeof(int), 11);
            values.TryGetValue(((IdentityEnum)a).GetType(), out var left);
            values.TryGetValue(((OtherIdentityEnum)b).GetType(), out var right);
            values.TryGetValue(a.GetType(), out var integer);
            return unchecked(left + right + integer);
        }
        public static int Null(int a, int b)
        {
            object value = a < 0 ? null : (object)(IdentityEnum)a;
            try { return value.GetType() == typeof(IdentityEnum) ? b : a; }
            catch (NullReferenceException) { return unchecked(b + 13); }
        }
        public static int UnsupportedValue(int a, int b) => typeof(TypeUncomparedValue) == typeof(object) ? a : b;
        public static int UnsupportedFloat(int a, int b) => typeof(float) == typeof(double) ? a : b;
        public static int UnsupportedOpen(int a, int b) => typeof(List<>) == typeof(object) ? a : b;
        public static int UnsupportedNullable(int a, int b) => typeof(int?) == typeof(object) ? a : b;
    }
}
