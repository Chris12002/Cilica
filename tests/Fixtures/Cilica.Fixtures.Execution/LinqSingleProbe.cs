using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class SingleState
    {
        public int Calls;
        public int Trace;
        public int[] Integers;
        public ValueVectorBox[] References;
        public ValueVectorCell[] Values;
        public bool Mutate(int value)
        {
            Calls++; Trace = unchecked(Trace * 7 + value);
            Integers[0] = 99;
            if (Calls == 2) Integers[2] = -5;
            return Calls == 1;
        }
        public bool KeepReference(ValueVectorBox value)
        {
            Calls++;
            References[0] = null;
            var allocation = new ValueVectorBox(Calls);
            Trace = unchecked(Trace * 7 + allocation.Number);
            return Calls == 1;
        }
        public bool KeepValue(ValueVectorCell value)
        {
            Calls++; Values[0] = default;
            var allocation = new ValueVectorBox(Calls);
            Trace = unchecked(Trace * 7 + allocation.Number);
            value.Number = 99;
            return Calls == 1;
        }
        public bool KeepNullable(int? value) { Calls++; return Calls == 1; }
    }
    public sealed class SingleFaultSequence : IEnumerable<int>, IEnumerator<int>
    {
        public int Mode;
        public int Trace;
        public int Index;
        public int Cutoff;
        public SingleFaultSequence(int mode, int cutoff) { Mode = mode; Cutoff = cutoff; }
        public IEnumerator<int> GetEnumerator() { Trace = 1; if (Mode == 1) throw new InvalidOperationException("get"); return this; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext() { Trace = unchecked(Trace * 7 + 2); Index++; if (Mode == 2 && Index == 2) throw new InvalidOperationException("late move"); return Index <= 3; }
        public int Current { get { Trace = unchecked(Trace * 7 + 3); if (Mode == 3 && Index == 2) throw new InvalidOperationException("late current"); return Index; } }
        object System.Collections.IEnumerator.Current => Current;
        public bool Match(int value) { Trace = unchecked(Trace * 7 + 4); if (Mode == 4 && value == 2) throw new InvalidOperationException("late predicate"); return Cutoff < 0 ? false : (Cutoff == 0 || value == 1); }
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose() { Trace = unchecked(Trace * 7 + 5); if (Mode == 5) throw new InvalidOperationException("late dispose"); }
    }
    public static class LinqSingleProbe
    {
        private static int[] Three() { var values = new int[3]; values[0] = 1; values[1] = 2; values[2] = 3; return values; }
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Arrays(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); int result;
            try { result = Three().SingleOrDefault(state.Match); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + state.Calls * 37 + b);
        }
        public static int Required(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); int result;
            try { result = Three().Single(state.Match); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + state.Calls * 37 + b);
        }
        public static int EmptyAndNull(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue);
            IEnumerable<int> source = a < 0 ? null : new int[0]; Func<int,bool> predicate = b < 0 ? null : state.Match; int result;
            try { result = b == 0 ? source.Single(predicate) : source.SingleOrDefault(predicate); }
            catch (ArgumentException e) { result = unchecked(Hash(e.Message) + Hash(e.ParamName)); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + state.Calls * 37);
        }
        public static int Mutations(int a, int b)
        {
            var state = new SingleState(); state.Integers = Three(); state.Integers[0] = a;
            int value = state.Integers.Single(state.Mutate);
            return unchecked(value + state.Calls * 37 + state.Trace * 53 + state.Integers[0] + b);
        }
        public static int References(int a, int b)
        {
            var state = new SingleState(); state.References = new ValueVectorBox[3]; state.References[0] = new ValueVectorBox(a);
            var result = state.References.Single(state.KeepReference);
            return unchecked(result.Number + state.Calls * 37 + state.Trace * 53 + b);
        }
        public static int Covariant(int a, int b)
        {
            var state = new PredicateState(a, 0); IEnumerable<object> values = new string[] { "a", "abcd", null }; int result;
            try { object value = values.SingleOrDefault(state.Wide); result = value == null ? 3 : ((string)value).Length; }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + state.Calls * 37 + b);
        }
        private static int Run(PredicateSequence source, PredicateState state, int mode)
        {
            int result;
            try { result = mode < 0 ? source.Single(state.Match) : source.SingleOrDefault(state.Match); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + source.Trace * 37 + state.Calls * 53);
        }
        public static int Fallback(int a, int b) => Run(new PredicateSequence(0), new PredicateState(a, int.MinValue), b);
        public static int Faults(int a, int b) => Run(new PredicateSequence(b & 3), new PredicateState(a, (b & 4) == 0 ? 0 : 2), b);
        public static int IgnoreCount(int a, int b) => Run(new PredicateCollection(), new PredicateState(a, int.MinValue), b);
        public static int Where(int a, int b)
        {
            var first = new PredicateState(a, int.MinValue); var second = new PredicateState(b, int.MinValue);
            var query = Three().Where(first.Match); int value;
            try { value = query.SingleOrDefault(second.Match); }
            catch (InvalidOperationException e) { value = Hash(e.Message); }
            var enumerator = query.GetEnumerator(); if ((object)enumerator == query) return -100; enumerator.Dispose();
            return unchecked(value + first.Calls * 37 + second.Calls * 53);
        }
        public static int MatchedDefaults(int a, int b)
        {
            var values = new int[1]; var references = new ValueVectorBox[1]; var cells = new ValueVectorCell[1];
            return values.Single(IsZero) + (references.Single(IsNull) == null ? 11 : -100) + cells.Single(IsDefault).Number;
        }
        private static bool IsZero(int value) => value == 0;
        private static bool IsNull(ValueVectorBox value) => value == null;
        private static bool IsDefault(ValueVectorCell value) => value.Reference == null;
        private static ValueVectorCell[] Cells(int a, int b)
        {
            var values = new ValueVectorCell[3]; ValueVectorCell cell = default;
            cell.Number = a; cell.Wide = ((long)a << 32) | (uint)b; cell.Reference = new ValueVectorBox(b); values[0] = cell;
            return values;
        }
        public static int Values(int a, int b)
        {
            var state = new SingleState(); state.Values = Cells(a, b); var value = state.Values.Single(state.KeepValue);
            return unchecked(value.Number + (int)(value.Wide >> 32) + (int)value.Wide + value.Reference.Number + state.Calls * 37 + state.Trace);
        }
        public static int Nullable(int a, int b)
        {
            var state = new SingleState(); var values = new int?[3]; if (b >= 0) values[0] = a;
            var value = values.Single(state.KeepNullable);
            return unchecked((value.HasValue ? value.Value : 17) + state.Calls * 37);
        }
        private static IEnumerable<ValueVectorCell> YieldCells(SingleState state)
        {
            for (int i = 0; i < state.Values.Length; i++) yield return state.Values[i];
        }
        public static int ValueFallback(int a, int b)
        {
            var state = new SingleState(); state.Values = Cells(a, b); var value = YieldCells(state).Single(state.KeepValue);
            return unchecked(value.Number + value.Reference.Number + (int)(value.Wide >> 32) + state.Calls * 37);
        }
        public static int OfType(int a, int b)
        {
            var source = new object[] { new ValueVectorBox(a), null, new ValueVectorBox(b) };
            var state = new PredicateState(a, 0); var query = source.OfType<ValueVectorBox>(); int result;
            try { var value = query.SingleOrDefault(state.Reference); result = value == null ? 7 : value.Number; }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            var cursor = query.GetEnumerator(); if ((object)cursor == query) return -100; cursor.Dispose();
            return unchecked(result + state.Calls * 37);
        }
        public static int LateFaults(int a, int b)
        {
            int result = 0;
            // Exercise every fault stage for every input, including disposal overriding no/multiple matches.
            for (int mode = 0; mode <= 5; mode++)
            {
                var source = new SingleFaultSequence(mode, a); int value;
                try { value = b < 0 ? source.Single(source.Match) : source.SingleOrDefault(source.Match); }
                catch (InvalidOperationException e) { value = Hash(e.Message); }
                result = unchecked(result * 31 + value + source.Trace * 37);
            }
            return result;
        }
        public static int UnsupportedNoPredicate(int a, int b) => new[] { a, b }.Single();
        public static int UnsupportedNoPredicateDefault(int a, int b) => new[] { a, b }.SingleOrDefault();
        public static int ListSingle(int a, int b)
        {
            var source = new List<int>();
            for (int i = 0; i < (a & 3); i++) source.Add(i == 0 ? 0 : b);
            try { return source.Single(IsZero); }
            catch (InvalidOperationException e) { return e.Message == "Sequence contains no matching element" ? 17 : e.Message == "Sequence contains more than one matching element" ? 19 : -100; }
        }
    }
}
