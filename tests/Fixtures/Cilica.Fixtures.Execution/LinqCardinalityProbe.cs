using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public class CardinalSequence : IEnumerable<int>
    {
        public int Limit;
        public int Mode;
        public int Trace;
        public CardinalSequence(int limit, int mode) { Limit = limit; Mode = mode; }
        public IEnumerator<int> GetEnumerator()
        {
            Trace = unchecked(Trace * 7 + 1);
            if (Mode == 1) throw new InvalidOperationException("factory");
            if (Mode == 2) return null;
            return new CardinalEnumerator(this);
        }
        IEnumerator IEnumerable.GetEnumerator() { throw new NotSupportedException(); }
    }
    public sealed class CardinalEnumerator : IEnumerator<int>
    {
        private CardinalSequence owner;
        private int index;
        public CardinalEnumerator(CardinalSequence owner) { this.owner = owner; }
        public bool MoveNext()
        {
            owner.Trace = unchecked(owner.Trace * 7 + 2);
            if (owner.Mode == 3 || owner.Mode == 5) throw new InvalidOperationException("move");
            return index++ < owner.Limit;
        }
        public int Current { get { throw new InvalidOperationException("current must not be read"); } }
        object IEnumerator.Current { get { throw new NotSupportedException(); } }
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            owner.Trace = unchecked(owner.Trace * 7 + 3);
            if (owner.Mode == 4 || owner.Mode == 5) throw new InvalidOperationException("dispose");
        }
    }
    public sealed class CardinalCollection : CardinalSequence, ICollection<int>, ICollection
    {
        public CardinalCollection(int count, int mode) : base(count, mode) { }
        public int Count { get { Trace = unchecked(Trace * 7 + 4); if (Mode == 6) throw new InvalidOperationException("count"); return Limit; } }
        int ICollection.Count { get { Trace = unchecked(Trace * 7 + 5); return -91; } }
        public bool IsReadOnly => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int value) { throw new NotSupportedException(); }
        public bool Remove(int value) { throw new NotSupportedException(); }
        public void CopyTo(int[] values, int index) { throw new NotSupportedException(); }
        public void CopyTo(Array values, int index) { throw new NotSupportedException(); }
    }
    public sealed class CardinalLegacyCollection : CardinalSequence, ICollection
    {
        public CardinalLegacyCollection(int count, int mode) : base(count, mode) { }
        public int Count { get { Trace = unchecked(Trace * 7 + 5); if (Mode == 6) throw new InvalidOperationException("count"); return Limit; } }
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void CopyTo(Array values, int index) { throw new NotSupportedException(); }
    }
    public static class LinqCardinalityProbe
    {
        public static int Arrays(int a, int b)
        {
            int[] values = a < 0 ? new int[0] : new[] { a, b };
            return values.Count() + (values.Any() ? 7 : 0);
        }
        public static int References(int a, int b)
        {
            ValueVectorBox[] values = a < 0 ? new ValueVectorBox[0] : new[] { new ValueVectorBox(a), null };
            return values.Count() + (values.Any() ? 7 : 0);
        }
        public static int Values(int a, int b)
        {
            var values = new ValueVectorCell[a & 3];
            return values.Count() + (values.Any() ? 7 : 0);
        }
        public static int GenericCollection(int a, int b) { return Run(new CardinalCollection(a, b & 7), b); }
        public static int LegacyCollection(int a, int b) { return Run(new CardinalLegacyCollection(a, b & 7), b); }
        public static int Fallback(int a, int b) { return Run(new CardinalSequence(a & 3, 0), b); }
        public static int Faults(int a, int b) { return Run(new CardinalSequence(a & 3, b & 7), a); }
        public static int Run(CardinalSequence sequence, int mode)
        {
            int result = 0;
            try { result = mode < 0 ? sequence.Count() : (sequence.Any() ? 11 : 13); }
            catch (InvalidOperationException e) { result = e.Message == "factory" ? 17 : e.Message == "move" ? 19 : e.Message == "dispose" ? 23 : e.Message == "count" ? 29 : -100; }
            catch (NullReferenceException) { result = 31; }
            return unchecked(result + sequence.Trace * 37);
        }
        public static int Null(int a, int b)
        {
            IEnumerable<int> missing = null; int result = 0;
            try { missing.Any(); } catch (ArgumentNullException e) { result += e.ParamName == "source" ? 3 : -100; }
            try { missing.Count(); } catch (ArgumentNullException e) { result += e.Message == "Value cannot be null. (Parameter 'source')" ? 7 : -100; }
            return result;
        }
        public static int UnsupportedList(int a, int b) { var values = new List<int>(); values.Add(a); return values.Count(); }
        public static int UnsupportedPredicate(int a, int b) { return new[] { a, b }.Count(x => x > 0); }
    }
}
