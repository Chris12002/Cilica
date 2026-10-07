using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class WideVectorHolder { public long[] Values; }
    public static class WideVectorProbe
    {
        private static long Bits(int a, int b) => ((long)a << 32) | (uint)b;
        private static int Fold(long value) => unchecked((int)value + (int)(value >> 32) * 37);
        public static int Storage(int a, int b)
        {
            long bits = Bits(a, b); var values = new long[4]; var check = new int[1]; check[0] = a;
            if (values[0] != 0 || values[3] != 0) return -101;
            values[0] = bits; values[1] = ~bits; values[2] = long.MinValue; values[3] = long.MaxValue;
            if (values[0] != bits || values[1] != ~bits || values[2] != long.MinValue || values[3] != long.MaxValue) return -103;
            return unchecked(Fold(values[0]) + Fold(values[1]) * 7 + check[0]);
        }
        private static T Read<T>(T[] values, int index) => values[index];
        private static void Write<T>(T[] values, int index, T value) { values[index] = value; }
        public static int Generic(int a, int b)
        { var values = new long[3]; Write(values, 0, Bits(a,b)); Write(values, 1, ~Bits(a,b)); return unchecked(Fold(Read(values,0)) + Fold(Read(values,1)) * 11 + Fold(Read(values,2))); }
        public static int Heap(int a, int b)
        {
            var holder = new WideVectorHolder(); holder.Values = new long[3]; var alias = holder.Values; alias[0] = Bits(a,b);
            for (int i = 0; i < 5; i++) { var pressure = new long[17]; pressure[i] = ~Bits(a,b); holder.Values[1] = pressure[i]; }
            return unchecked(Fold(alias[0]) + Fold(holder.Values[1]) * 13);
        }
        public static int Empty(int a, int b)
        { var first = Array.Empty<long>(); var second = Array.Empty<int>(); return first.Length + ((object)first == Array.Empty<long>() ? 7 : -101) + ((object)first == second ? -103 : 11); }
        public static int Types(int a, int b)
        {
            object value = a < 0 ? (object)new long[2] : new int[2]; int result = value is long[] ? 1 : 0;
            result += value is int[] ? 2 : 0; result += value is IList<long> ? 4 : 0; result += value is IList<ulong> ? 8 : 0;
            result += value is IReadOnlyList<long> ? 16 : 0; result += value is IEnumerable<ulong> ? 32 : 0;
            return result + (value.GetType() == new long[0].GetType() ? 64 : 0);
        }
        public static int Cast(int a, int b)
        {
            object value = new long[1];
            try { return ((int[])value)[b]; }
            catch (InvalidCastException e) { int result = 0; for (int i = 0; i < e.Message.Length; i++) result = unchecked(result * 31 + e.Message[i]); return result; }
        }
        public static int Faults(int a, int b)
        {
            long[] values = a < 0 ? null : new long[2]; int result = 0;
            try { result = Fold(values[b]); } catch (NullReferenceException) { result = 7; } catch (IndexOutOfRangeException) { result = 11; }
            try { values[b] = Bits(a,b); result = unchecked(result + Fold(values[b])); } catch (NullReferenceException) { result += 13; } catch (IndexOutOfRangeException) { result += 17; }
            return result;
        }
        public static int Interfaces(int a, int b)
        {
            long bits = Bits(a,b); var source = new long[3]; IList<long> list = source; list[0] = bits; list[1] = ~bits;
            var target = new long[4]; ((ICollection<long>)source).CopyTo(target, 1); int result = Fold(((IReadOnlyList<long>)source)[0]);
            foreach (long value in (IEnumerable<long>)target) result = unchecked(result * 7 + Fold(value));
            return unchecked(result + list.IndexOf(bits) + (list.Contains(~bits) ? 11 : 13));
        }
        public static int InterfaceFaults(int a, int b)
        {
            IList<long> list = new long[2]; int result = 0;
            try { list[b] = Bits(a,b); result = Fold(list[b]); } catch (ArgumentOutOfRangeException e) { result = e.ParamName == "index" ? 7 : -101; }
            try { list.Add(Bits(a,b)); } catch (NotSupportedException) { result += 11; }
            try { list.Remove(Bits(a,b)); } catch (NotSupportedException) { result += 13; }
            try { list.Clear(); } catch (NotSupportedException) { result += 17; }
            ((IList)list).Clear(); return unchecked(result + Fold(list[0]));
        }
        public static int List(int a, int b)
        {
            var values = new List<long>(); for (int i = 0; i < 12; i++) values.Add(unchecked(Bits(a,b) + i));
            values.RemoveAt(2); values.Insert(1, long.MinValue); var copy = values.ToArray();
            int result = values.Contains(long.MinValue) ? 7 : -101;
            foreach (long value in values) result = unchecked(result * 7 + Fold(value));
            return unchecked(result + Fold(copy[1]) + copy.Length);
        }
        public static int Set(int a, int b) => HashSetConstructionProbe.Wide(a,b);
        public static int SetArray(int a, int b)
        {
            long bits = Bits(a,b); var set = new HashSet<long>(new[] { bits, ~bits, bits, 0L }); int result = set.Count;
            foreach (long value in set) result = unchecked(result * 7 + Fold(value));
            return result;
        }
        public static int UnsignedView(int a, int b)
        {
            var values = new long[2]; IList<ulong> view = (IList<ulong>)(object)values;
            ulong bits = ((ulong)(uint)a << 32) | (uint)b; view[0] = bits; view[1] = ~bits;
            var read = (IReadOnlyList<ulong>)(object)values;
            if (view[0] != bits || read[1] != ~bits || values[0] != (long)bits || values[1] != (long)~bits) return -101;
            return view.IndexOf(bits) + (view.Contains(~bits) ? 7 : -103);
        }
        public static int UnsupportedUnsignedEnumeration(int a, int b)
        {
            var cursor = ((IEnumerable<ulong>)(object)new long[1]).GetEnumerator();
            cursor.MoveNext(); int value = (int)cursor.Current; cursor.Dispose(); return value;
        }
        public static int UnsupportedUnsignedCopy(int a, int b)
        { ((ICollection<ulong>)(object)new long[1]).CopyTo(null, 0); return a; }
        public static int UnsupportedBorrow(int a, int b) { var values = new long[1]; ref long value = ref values[0]; value = Bits(a,b); return Fold(value); }
        public static int UnsupportedUnsigned(int a, int b) { var values = new ulong[1]; values[0] = (ulong)Bits(a,b); return (int)values[0]; }
    }
}
