using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class LinqMaterializationProbe
    {
        private static int Sum(int[] values) { int result = values.Length; for (int i = 0; i < values.Length; i++) result = unchecked(result * 7 + values[i]); return result; }
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Arrays(int a, int b)
        {
            var source = new[] { a, b }; var copy = source.ToArray(); source[0] = 19;
            return unchecked(Sum(copy) + (copy == source ? -100 : 7));
        }
        public static int EmptyAndNull(int a, int b)
        {
            int[] source = a < 0 ? null : new int[0];
            try { return source.ToArray() == Array.Empty<int>() ? b : -100; }
            catch (ArgumentNullException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int Collections(int a, int b)
        {
            var source = new ConstructionCollection(); source.Mode = (int)((uint)a % 5); source.First = a; source.Second = b;
            int result;
            try { result = Sum(source.ToArray()); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            catch (OverflowException e) { result = Hash(e.Message); }
            return unchecked(result * 7 + source.Reads * 13 + source.Copies);
        }
        public static int Values(int a, int b)
        {
            ValueVectorCell item = default; item.Number = a; item.Wide = ((long)a << 32) + b; item.Reference = new ValueVectorBox(b);
            var source = new[] { item }; var copy = source.ToArray(); source[0] = default;
            return unchecked(copy[0].Number + (int)(copy[0].Wide >> 32) + copy[0].Reference.Number);
        }
        public static int EmptyValues(int a, int b)
        {
            var result = new ValueVectorEmpty[a & 3].ToArray(); return result.Length + b + (result == Array.Empty<ValueVectorEmpty>() ? 7 : 0);
        }
        public static int Covariant(int a, int b)
        {
            IEnumerable<object> source = new string[] { "first", null, "last" }; var copy = source.ToArray(); copy[0] = new ValueVectorBox(a);
            return unchecked(((ValueVectorBox)copy[0]).Number + ((string)copy[2]).Length + b);
        }
        public static int Growth(int a, int b)
        {
            var source = new int[19]; for (int i = 0; i < source.Length; i++) source[i] = unchecked(a + i);
            var state = new PredicateState(b, unchecked(a + 30)); var query = source.Where(state.Match); var copy = query.ToArray();
            return unchecked(Sum(copy) + state.Calls * 37);
        }
        public static int WhereCursor(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); var query = new[] { 1, b | 1, 3 }.Where(state.Match);
            var enumerator = query.GetEnumerator(); enumerator.MoveNext(); int current = enumerator.Current;
            var copy = query.ToArray(); if (enumerator.Current != current) return -100;
            int result = Sum(copy); if (enumerator.MoveNext()) result = unchecked(result + enumerator.Current);
            enumerator.Dispose(); return unchecked(result + state.Calls * 37);
        }
        public static int WhereIdentity(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); var query = new[] { 1, b | 1, 3 }.Where(state.Match);
            int result = Sum(query.ToArray()); var first = query.GetEnumerator();
            if ((object)first != query) return -100; first.Dispose();
            return unchecked(result + state.Calls * 37);
        }
        public static int Fallback(int a, int b)
        {
            var source = new PredicateSequence(b & 3); int result;
            try { result = Sum(source.ToArray()); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + source.Trace * 37 + a);
        }
        public static int WhereFaults(int a, int b)
        {
            var source = new PredicateSequence(b & 3); var state = new PredicateState(a, 2); int result;
            try { result = Sum(source.Where(state.Match).ToArray()); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + source.Trace * 37 + state.Calls * 53);
        }
        public static int References(int a, int b)
        {
            var state = new PredicateState(int.MinValue, 0); var item = new ValueVectorBox(a & int.MaxValue);
            var source = new[] { item, new ValueVectorBox(b & int.MaxValue) }; var copy = source.Where(state.Reference).ToArray();
            source[0] = null; item.Number = b;
            return unchecked(copy.Length + (copy[0] == item ? 7 : -100) + copy[0].Number + state.Calls);
        }
        public static int UnsupportedListEnumerable(int a, int b) => Enumerable.ToArray(new List<int>()).Length;
        public static int UnsupportedUnsigned(int a, int b) => new uint[1].ToArray().Length;
    }
}
