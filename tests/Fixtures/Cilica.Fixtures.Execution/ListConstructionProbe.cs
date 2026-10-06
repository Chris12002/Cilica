using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class ConstructionCollection : ICollection<int>
    {
        public int Mode, First, Second, Reads, Copies;
        public int Count { get { Reads++; if (Mode == 1) throw new InvalidOperationException("count"); return Mode == 2 ? 0 : Mode == 3 ? -1 : 2; } }
        public bool IsReadOnly => true;
        public void CopyTo(int[] array, int index)
        {
            Copies++; array[index] = First;
            if (Mode == 4) throw new InvalidOperationException("copy");
            array[index + 1] = Second; Mode = 2;
        }
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int value) { throw new NotSupportedException(); }
        public bool Remove(int value) { throw new NotSupportedException(); }
        public IEnumerator<int> GetEnumerator() { throw new NotSupportedException("enumerated"); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public static class ListConstructionProbe
    {
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Integers(int a, int b)
        {
            var source = new[] { a, b }; var copy = new List<int>(source); source[0] = 7;
            var e = copy.GetEnumerator(); e.MoveNext(); int result = unchecked(e.Current * 3 + copy[1] + copy.Capacity);
            copy.Add(11); try { e.MoveNext(); } catch (InvalidOperationException) { result = unchecked(result + 19); }
            return result;
        }
        public static int Lists(int a, int b)
        {
            var source = new List<int>(); source.Add(a); source.Add(b);
            var copy = new List<int>(source); source.Clear();
            return unchecked(copy[0] * 3 + copy[1] + copy.Count + copy.Capacity);
        }
        public static int Null(int a, int b)
        {
            ICollection<int> source = a < 0 ? null : new int[b & 3];
            try { var copy = new List<int>(source); return copy.Count + copy.Capacity; }
            catch (ArgumentNullException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int Custom(int a, int b)
        {
            var source = new ConstructionCollection(); source.Mode = (int)((uint)a % 5); source.First = a; source.Second = b;
            int result;
            try { var copy = new List<int>(source); result = copy.Count + copy.Capacity; if (copy.Count != 0) result = unchecked(result + copy[0] * 3 + copy[1]); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            catch (OverflowException e) { result = Hash(e.Message); }
            return unchecked(result * 7 + source.Reads * 13 + source.Copies);
        }
        public static int References(int a, int b)
        {
            var item = new ValueVectorBox(a); var source = new[] { item, null }; var copy = new List<ValueVectorBox>(source);
            source[0] = null; item.Number = b;
            return unchecked(copy[0].Number + (copy[0] == item && copy[1] == null ? 17 : -100));
        }
        public static int Values(int a, int b)
        {
            ValueVectorCell item = default; item.Number = a; item.Wide = ((long)a << 32) + b; item.Reference = new ValueVectorBox(b);
            var source = new[] { item }; var copy = new List<ValueVectorCell>(source); source[0] = default;
            return unchecked(copy[0].Number + (int)(copy[0].Wide >> 32) + copy[0].Reference.Number);
        }
        public static int EmptyValues(int a, int b)
        {
            var copy = new List<ValueVectorEmpty>(new ValueVectorEmpty[a & 3]); return copy.Count + copy.Capacity + b;
        }
        public static int Covariant(int a, int b)
        {
            string[] source = a < 0 ? null : new[] { "first", null, "last" };
            try { var copy = new List<object>(source); return copy.Count + copy.Capacity + ((string)copy[0]).Length + b; }
            catch (ArgumentNullException e) { return Hash(e.Message); }
        }
        public static int UnsupportedAlias(int a, int b)
        {
            IEnumerable<int> source = new[] { a, b }; return new List<int>(source).Count;
        }
        public static int UnsupportedIterator(int a, int b) => new List<int>(Iterate(a, b)).Count;
        private static IEnumerable<int> Iterate(int a, int b) { yield return a; yield return b; }
        public static int UnsupportedUnsigned(int a, int b) => new List<uint>(new uint[1]).Count;
    }
}
