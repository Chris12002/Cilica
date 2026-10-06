using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class LinqFirstProbe
    {
        private static int[] Three() { var values = new int[3]; values[0] = 1; values[1] = 2; values[2] = 3; return values; }
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Arrays(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); var values = Three();
            return unchecked(values.FirstOrDefault(state.Match) + state.Calls * 37 + b);
        }
        public static int Required(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); var values = Three(); int result;
            try { result = values.First(state.Match); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + state.Calls * 37 + b);
        }
        public static int EmptyAndNull(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue);
            IEnumerable<int> source = a < 0 ? null : new int[0]; Func<int,bool> predicate = b < 0 ? null : state.Match; int result;
            try { result = b == 0 ? source.First(predicate) : source.FirstOrDefault(predicate); }
            catch (ArgumentException e) { result = unchecked(Hash(e.Message) + Hash(e.ParamName)); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + state.Calls * 37);
        }
        public static int Mutations(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); state.Values = Three();
            return unchecked(state.Values.FirstOrDefault(state.Match) + state.Calls * 37 + state.Values[2] + b);
        }
        public static int References(int a, int b)
        {
            var state = new PredicateState(a, 0); var first = new ValueVectorBox(b); var second = new ValueVectorBox(a);
            var values = new[] { null, first, second }; var result = values.FirstOrDefault(state.Reference);
            return unchecked((result == null ? 3 : result.Number) + (result == first ? 7 : 0) + state.Calls * 37);
        }
        public static int Covariant(int a, int b)
        {
            var state = new PredicateState(a, 0); IEnumerable<object> values = new string[] { "a", "abcd", null };
            object result = values.FirstOrDefault(state.Wide);
            return unchecked((result == null ? 3 : ((string)result).Length) + state.Calls * 37 + b);
        }
        private static int Run(PredicateSequence source, PredicateState state, int mode)
        {
            int result;
            try { result = mode < 0 ? source.First(state.Match) : source.FirstOrDefault(state.Match); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + source.Trace * 37 + state.Calls * 53);
        }
        public static int Fallback(int a, int b) => Run(new PredicateSequence(0), new PredicateState(a, int.MinValue), b);
        public static int Faults(int a, int b) => Run(new PredicateSequence(b & 3), new PredicateState(a, 2), b);
        public static int IgnoreCount(int a, int b) => Run(new PredicateCollection(), new PredicateState(a, int.MinValue), b);
        public static int Where(int a, int b)
        {
            var first = new PredicateState(a, int.MinValue); var second = new PredicateState(b, int.MinValue);
            var query = Three().Where(first.Match);
            int value = query.FirstOrDefault(second.Match); var enumerator = query.GetEnumerator();
            if ((object)enumerator == query) return -100; enumerator.Dispose();
            return unchecked(value + first.Calls * 37 + second.Calls * 53);
        }
        public static int MatchedDefaults(int a, int b)
        {
            var values = new int[2]; values[1] = a;
            var references = new ValueVectorBox[2]; references[1] = new ValueVectorBox(b);
            return values.First(IsZero) + (references.First(IsNull) == null ? 11 : -100);
        }
        private static bool IsZero(int value) => value == 0;
        private static bool IsNull(ValueVectorBox value) => value == null;
        public static int UnsupportedNoPredicate(int a, int b) => new[] { a, b }.FirstOrDefault();
        public static int ValuePredicate(int a, int b) => new ValueVectorCell[1].FirstOrDefault(Match).Number;
        private static bool Match(ValueVectorCell value) => true;
    }
}
