using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public enum ArrayInterfaceEnum { Zero, One }
    public static class ArrayInterfaceProbe
    {
        public static int Casts(int input, int mode)
        {
            object vector = mode < 0 ? (object)new int[2] : new string[3];
            int bits = vector is IEnumerable ? 1 : 0;
            bits += vector is IEnumerable<int> ? 2 : 0;
            bits += vector is IReadOnlyCollection<int> ? 4 : 0;
            bits += vector is IReadOnlyList<int> ? 8 : 0;
            bits += vector is IReadOnlyList<string> ? 16 : 0;
            bits += vector is IReadOnlyList<object> ? 32 : 0;
            bits += vector is IReadOnlyList<long> ? 64 : 0;
            bits += vector is IReadOnlyList<uint> ? 128 : 0;
            return unchecked(input + bits);
        }
        public static int Integers(int input, int mode)
        {
            IReadOnlyList<int> vector = new[] { input, mode };
            return unchecked(vector.Count + vector[0] + vector[1]);
        }
        public static int Covariance(int input, int mode)
        {
            var array = new[] { string.Concat("a", "b"), "XYZ" };
            IReadOnlyList<object> objects = array;
            IReadOnlyList<string> strings = array;
            object vector = objects;
            int bits = objects[0] == (object)strings[0] ? 1 : 0;
            try { ((object[])vector)[0] = new object(); }
            catch (ArrayTypeMismatchException) { bits += 7; }
            return unchecked(input + mode + bits + strings[0].Length + strings[1].Length + objects.Count);
        }
        public static int Faults(int input, int mode)
        {
            IReadOnlyList<int> vector = mode < 0 ? null : new[] { 9, 13 };
            int result = input;
            try { result = unchecked(result + vector.Count); }
            catch (NullReferenceException) { result = unchecked(result + 11); }
            try { result = unchecked(result + vector[input]); }
            catch (ArgumentOutOfRangeException error) { result = unchecked(result + (error.ParamName == "index" && error.Message == "Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')" ? 17 : 31)); }
            catch (NullReferenceException) { result = unchecked(result + 23); }
            return result;
        }
        public static int Mixed(int input, int mode)
        {
            IReadOnlyList<int> values = mode < 0 ? (IReadOnlyList<int>)new ContractPair<int>(input, mode) : new[] { input, mode };
            return unchecked(values.Count + values[0] + values[1]);
        }
        public static int SignedReads(int input, int mode)
        {
            object array = new[] { input, mode };
            var values = (IReadOnlyList<uint>)array;
            return unchecked((int)(values[0] >> 1) + (int)values[1] + values.Count);
        }
        public static int UnsupportedEnum(int input, int mode)
        {
            object array = new[] { input, mode };
            return array is IReadOnlyList<ArrayInterfaceEnum> ? input : mode;
        }
        public static int UnsupportedEnumeration(int input, int mode)
        {
            IEnumerable<int> array = new[] { input, mode };
            return array.GetEnumerator().MoveNext() ? input : mode;
        }
    }
}
