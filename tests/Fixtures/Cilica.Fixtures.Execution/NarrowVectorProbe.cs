using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class NarrowVectorHolder { public byte[] Bytes; public char[] Chars; }
    public static class NarrowVectorProbe
    {
        public static int Bytes(int a, int b)
        { var values = new byte[3]; values[0] = unchecked((byte)a); values[1] = unchecked((byte)b); return values[0] + values[1] * 257 + values[2]; }
        public static int Chars(int a, int b)
        { var values = new char[3]; values[0] = unchecked((char)a); values[1] = unchecked((char)b); return unchecked(values[0] + values[1] * 65537 + values[2]); }
        public static int Loops(int a, int b)
        {
            var bytes = new byte[8]; var chars = new char[8];
            for (int i = 0; i < 8; i++) { bytes[i] = unchecked((byte)(a + i)); chars[i] = unchecked((char)(b - i)); }
            int result = 0;
            foreach (byte value in bytes) result = unchecked(result * 31 + value);
            foreach (char value in chars) result = unchecked(result * 37 + value);
            return result;
        }
        private static T Read<T>(T[] values, int index) => values[index];
        private static void Write<T>(T[] values, int index, T value) { values[index] = value; }
        public static int Generic(int a, int b)
        { var bytes = new byte[1]; var chars = new char[1]; Write(bytes, 0, unchecked((byte)a)); Write(chars, 0, unchecked((char)b)); return Read(bytes, 0) + Read(chars, 0) * 257; }
        public static int Heap(int a, int b)
        {
            var h = new NarrowVectorHolder(); h.Bytes = new byte[1]; h.Chars = new char[1];
            var alias = h.Bytes; alias[0] = unchecked((byte)a); h.Chars[0] = unchecked((char)b);
            for (int i = 0; i < 8; i++) { var garbage = new byte[7]; garbage[0] = (byte)i; if (garbage[0] != i) return -1; }
            return h.Bytes[0] + h.Chars[0] * 257;
        }
        public static int Empty(int a, int b)
        {
            var first = Array.Empty<byte>(); var second = Array.Empty<char>();
            return (first.Length + second.Length) * a + b + ((object)first == (object)Array.Empty<byte>() ? 3 : 5)
                + ((object)second == (object)Array.Empty<char>() ? 7 : 11) + ((object)first == (object)second ? 13 : 17);
        }
        public static int Types(int a, int b)
        {
            object value = a < 0 ? (object)new byte[1] : new char[1];
            int result = value is byte[] ? 1 : 0;
            result += value is char[] ? 2 : 0;
            result += value is int[] ? 4 : 0;
            result += value is IList<byte> ? 8 : 0;
            result += value is IList<sbyte> ? 16 : 0;
            result += value is IList<bool> ? 32 : 0;
            result += value is IList<char> ? 64 : 0;
            result += value is IList<ushort> ? 128 : 0;
            return unchecked(result + b);
        }
        public static int RuntimeTypes(int a, int b)
        { var bytes = new byte[1]; var chars = new char[1]; return (bytes.GetType() == new byte[0].GetType() ? a : b) + (bytes.GetType() == chars.GetType() ? 7 : 13); }
        public static int CastMessages(int a, int b)
        {
            object value = a < 0 ? (object)new byte[1] : new char[1];
            try { return ((int[])value)[b]; }
            catch (InvalidCastException e) { int result = 0; for (int i=0;i<e.Message.Length;i++) result=unchecked(result*31+e.Message[i]); return result; }
        }
        public static int LoadFaults(int a, int b)
        {
            byte[] bytes = a < 0 ? null : new byte[2];
            try { return bytes[b]; } catch (NullReferenceException) { return -11; } catch (IndexOutOfRangeException) { return -13; }
        }
        public static int StoreFaults(int a, int b)
        {
            char[] chars = a < 0 ? null : new char[2];
            try { chars[b] = unchecked((char)a); return chars[b]; } catch (NullReferenceException) { return -17; } catch (IndexOutOfRangeException) { return -19; }
        }
        public static int UnsupportedBorrow(int a, int b) { var values = new byte[1]; ref byte value = ref values[0]; value = unchecked((byte)a); return value + b; }
        public static int UnsupportedInterface(int a, int b) { IList<byte> values = new byte[1]; return values[0] + a + b; }
        public static int UnsupportedSignedArray(int a, int b) { var values = new sbyte[1]; values[0] = unchecked((sbyte)a); return values[0] + b; }
    }
}
