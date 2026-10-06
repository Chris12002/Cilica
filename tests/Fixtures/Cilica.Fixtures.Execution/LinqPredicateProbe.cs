using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class PredicateState
    {
        public int Calls;
        public int Cutoff;
        public int ThrowAt;
        public int[] Values;
        public PredicateState(int cutoff, int throwAt) { Cutoff = cutoff; ThrowAt = throwAt; }
        public bool Match(int value)
        {
            Calls = unchecked(Calls * 7 + value);
            if (ThrowAt > 0 && value == ThrowAt) throw new InvalidOperationException("predicate");
            if (Values != null) Values[1] = Cutoff;
            var allocation = new ValueVectorBox(value);
            return allocation.Number > Cutoff;
        }
        public bool Reference(ValueVectorBox value) { Calls++; return value != null && value.Number > Cutoff; }
        public bool Wide(object value) { Calls++; return value == null || ((string)value).Length > Cutoff; }
    }
    public class PredicateSequence : IEnumerable<int>
    {
        public int Trace;
        public int Mode;
        public PredicateSequence(int mode) { Mode = mode; }
        public IEnumerator<int> GetEnumerator() { Trace = unchecked(Trace * 7 + 1); return new PredicateEnumerator(this); }
        IEnumerator IEnumerable.GetEnumerator() { throw new NotSupportedException(); }
    }
    public sealed class PredicateCollection : PredicateSequence, ICollection<int>
    {
        public PredicateCollection() : base(0) { }
        public int Count { get { throw new InvalidOperationException("count must not be read"); } }
        public bool IsReadOnly => true;
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int value) { throw new NotSupportedException(); }
        public bool Remove(int value) { throw new NotSupportedException(); }
        public void CopyTo(int[] values, int index) { throw new NotSupportedException(); }
    }
    public sealed class PredicateEnumerator : IEnumerator<int>
    {
        private PredicateSequence owner;
        private int index;
        public PredicateEnumerator(PredicateSequence owner) { this.owner = owner; }
        public bool MoveNext() { owner.Trace = unchecked(owner.Trace * 7 + 2); return index++ < 3; }
        public int Current { get { owner.Trace = unchecked(owner.Trace * 7 + 3); if (owner.Mode == 1) throw new InvalidOperationException("current"); return index; } }
        object IEnumerator.Current { get { throw new NotSupportedException(); } }
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose() { owner.Trace = unchecked(owner.Trace * 7 + 4); if (owner.Mode == 2) throw new InvalidOperationException("dispose"); }
    }
    public static class LinqPredicateProbe
    {
        private static int[] Three() { var values = new int[3]; values[0] = 1; values[1] = 2; values[2] = 3; return values; }
        public static int ArraysAny(int a, int b) { var state = new PredicateState(a, int.MinValue); return unchecked((Three().Any(state.Match) ? 5 : 11) + state.Calls * 37); }
        public static int ArraysAll(int a, int b) { var state = new PredicateState(a, int.MinValue); return unchecked((Three().All(state.Match) ? 5 : 11) + state.Calls * 37); }
        public static int ArraysCount(int a, int b) { var state = new PredicateState(a, int.MinValue); return unchecked(Three().Count(state.Match) + state.Calls * 37); }
        public static int Empty(int a, int b) { var state = new PredicateState(a, int.MinValue); var empty = new int[0]; return (empty.Any(state.Match) ? 1 : 0) + (empty.All(state.Match) ? 2 : 0) + empty.Count(state.Match) + state.Calls; }
        public static int Mutations(int a, int b)
        {
            var state = new PredicateState(a, int.MinValue); state.Values = Three();
            return unchecked(state.Values.Count(state.Match) + state.Calls * 37);
        }
        public static int References(int a, int b)
        {
            var state = new PredicateState(a, 0); var values = new[] { new ValueVectorBox(b), null, new ValueVectorBox(a) };
            return unchecked(values.Count(state.Reference) + (values.Any(state.Reference) ? 7 : 0) + (values.All(state.Reference) ? 11 : 0) + state.Calls * 37);
        }
        public static int Covariance(int a, int b)
        {
            var state = new PredicateState(a, 0); IEnumerable<object> values = new string[] { "a", null, "abc" };
            return unchecked(values.Count(state.Wide) + (values.Any(state.Wide) ? 7 : 0) + (values.All(state.Wide) ? 11 : 0) + state.Calls * 37);
        }
        public static int Fallback(int a, int b) { return Run(new PredicateSequence(0), new PredicateState(a, int.MinValue), b); }
        public static int Faults(int a, int b) { return Run(new PredicateSequence(b & 3), new PredicateState(a, 2), b); }
        private static int Run(PredicateSequence source, PredicateState state, int mode)
        {
            int result = 0;
            try { result = mode < 0 ? source.Count(state.Match) : mode == 0 ? (source.Any(state.Match) ? 5 : 7) : (source.All(state.Match) ? 11 : 13); }
            catch (InvalidOperationException e) { result = e.Message == "predicate" ? 17 : e.Message == "current" ? 19 : e.Message == "dispose" ? 23 : -100; }
            return unchecked(result + source.Trace * 37 + state.Calls * 53);
        }
        public static int IgnoreCounts(int a, int b) { return Run(new PredicateCollection(), new PredicateState(a, int.MinValue), b); }
        public static int ArrayFaults(int a, int b)
        {
            var state = new PredicateState(a, 2); var values = Three(); int result = 0;
            try { result = b < 0 ? values.Count(state.Match) : b == 0 ? (values.Any(state.Match) ? 5 : 7) : (values.All(state.Match) ? 11 : 13); }
            catch (InvalidOperationException e) { result = e.Message == "predicate" ? 17 : -100; }
            return unchecked(result + state.Calls * 37);
        }
        public static int ValuePredicate(int a, int b) { var values = new ValueVectorCell[1]; return values.Count(cell => cell.Number > 0); }
        public static int Null(int a, int b)
        {
            IEnumerable<int> source = a < 0 ? null : new int[0]; Func<int, bool> predicate = null;
            int result = 0;
            try { source.Any(predicate); } catch (ArgumentNullException e) { result += e.ParamName == "source" ? 1 : e.ParamName == "predicate" ? 2 : -100; }
            try { source.All(predicate); } catch (ArgumentNullException e) { result += e.ParamName == "source" ? 3 : e.ParamName == "predicate" ? 5 : -100; }
            try { source.Count(predicate); } catch (ArgumentNullException e) { result += e.ParamName == "source" ? 7 : e.ParamName == "predicate" ? 11 : -100; }
            return result;
        }
    }
}
