using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class CandidateBooleanHolder { public bool[] Values; }
    public static class CandidateBooleanVectorProbe
    {
        public static int Values(int a, int b)
        {
            var values = new bool[5]; values[0] = a != 0; values[1] = b < 0;
            int result = 0;
            for (int i=0; i<5; ++i) result = result * 2 + (values[i] ? 1 : 0);
            return result;
        }
        private static T Read<T>(T[] values, int index) => values[index];
        private static void Write<T>(T[] values, int index, T value) { values[index] = value; }
        public static int Generic(int a, int b)
        {
            var values = new bool[2]; Write(values, 0, a < b); Write(values, 1, a == b);
            return (Read(values,0) ? 1 : 0) + (Read(values,1) ? 2 : 0);
        }
        public static int Heap(int a, int b)
        {
            var holder = new CandidateBooleanHolder { Values = new bool[2] }; var alias = holder.Values;
            alias[0] = a != 0;
            for (int i=0; i<6; ++i) { var pressure = new bool[3]; pressure[0] = i > 0; alias[1] = pressure[0]; }
            return (holder.Values[0] ? 1 : 0) + (holder.Values[1] ? 2 : 0) + (ReferenceEquals(alias,holder.Values) ? 4 : 0);
        }
        public static int Empty(int a, int b)
        {
            var first = Array.Empty<bool>(); var second = Array.Empty<bool>(); var fresh = new bool[0];
            return first.Length + (ReferenceEquals(first,second) ? 1 : 0) + (ReferenceEquals(first,fresh) ? 2 : 0)
                + (ReferenceEquals(first,Array.Empty<byte>()) ? 4 : 0) + (first.GetType() == fresh.GetType() ? 8 : 0);
        }
        public static int Types(int a, int b)
        {
            object value = a < 0 ? (object)new bool[1] : new byte[1];
            return (value is bool[] ? 1 : 0) + (value is byte[] ? 2 : 0) + (value is char[] ? 4 : 0)
                + (value is int[] ? 8 : 0) + (value is IList<bool> ? 16 : 0) + (value is IList<byte> ? 32 : 0)
                + (value is IList<sbyte> ? 64 : 0) + (value is IReadOnlyList<bool> ? 128 : 0)
                + (value is IEnumerable<bool> ? 256 : 0) + (value.GetType() == new bool[0].GetType() ? 512 : 0);
        }
        public static int CastMessages(int a, int query)
        {
            object value = a < 0 ? (object)new bool[1] : new byte[1];
            try { return a < 0 ? ((byte[])value).Length : ((bool[])value).Length; }
            catch (InvalidCastException e)
            {
                string expected = a < 0 ? "Unable to cast object of type 'System.Boolean[]' to type 'System.Byte[]'."
                    : "Unable to cast object of type 'System.Byte[]' to type 'System.Boolean[]'.";
                if (e.Message != expected) return -1;
                return e.Message.Length;
            }
        }
        private static bool[] Receiver(ref int order, int mode)
        { order = order * 10 + 1; return mode < 0 ? null : new bool[2]; }
        private static int Index(ref int order, int index) { order = order * 10 + 2; return index; }
        private static bool Value(ref int order, int mode)
        { order = order * 10 + 3; if (mode == 0) throw new ArgumentException(); return mode > 0; }
        public static int FaultOrder(int a, int b)
        {
            int order = 0;
            try { Receiver(ref order,a)[Index(ref order,b)] = Value(ref order,a); return order * 10; }
            catch (ArgumentException) { return order * 10 + 1; }
            catch (NullReferenceException) { return order * 10 + 2; }
            catch (IndexOutOfRangeException) { return order * 10 + 3; }
        }
        public static int Metadata(int a, int b)
        {
            var values = new bool[2]; ICollection<bool> generic = values; ICollection plain = values; IList list = values;
            return generic.Count + plain.Count * 10 + (generic.IsReadOnly ? 100 : 0) + (list.IsFixedSize ? 1000 : 0)
                + (list.IsReadOnly ? 10000 : 0) + (plain.IsSynchronized ? 100000 : 0)
                + (ReferenceEquals(plain.SyncRoot,values) ? 1000000 : 0);
        }
        public static int Static(int a, int b)
        { var values = new[] { true,false,true,true,false,false,true,false,true,false,true,true }; return values[(a & 7)] ? 1 : 0; }
        public static int AllocationFaults(int a, int b)
        {
            int length = a < 0 ? a : a & 3;
            try { var values = new bool[length]; return values.Length; }
            catch (OverflowException) { return -17; }
        }
        public static int UnsupportedBorrow(int a, int b) { var values = new bool[1]; ref bool value = ref values[0]; value = a != 0; return value ? 1 : 0; }
        public static int UnsupportedInterface(int a, int b) { IList<bool> values = new bool[1]; return values[0] ? 1 : 0; }
        public static int UnsupportedClear(int a, int b) { IList values = new bool[1]; values.Clear(); return values.Count; }
        public static int UnsupportedCopy(int a, int b) { ICollection<bool> values = new bool[1]; values.CopyTo(new bool[1],0); return 0; }
    }
}
