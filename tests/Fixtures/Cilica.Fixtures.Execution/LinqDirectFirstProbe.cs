using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class FirstSequence : IEnumerable<int>, IEnumerator<int>
    {
        public int Trace, Mode, Length, Value;
        private int position;
        public FirstSequence(int mode, int length, int value) { Mode = mode; Length = length; Value = value; }
        public IEnumerator<int> GetEnumerator() { Trace = unchecked(Trace * 11 + 1); if (Mode == 1) throw new InvalidOperationException("get"); position = 0; return this; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext() { Trace = unchecked(Trace * 11 + 2); if (Mode == 2 || Mode == 5) throw new InvalidOperationException("move"); return position++ < Length; }
        public int Current { get { Trace = unchecked(Trace * 11 + 3); if (Mode == 3 || Mode == 6) throw new InvalidOperationException("current"); return Value; } }
        object IEnumerator.Current { get { throw new NotSupportedException(); } }
        public void Dispose() { Trace = unchecked(Trace * 11 + 4); if (Mode >= 4) throw new InvalidOperationException("dispose"); }
        public void Reset() { throw new NotSupportedException(); }
    }
    public sealed class FirstList : IList<int>
    {
        public int Trace, Mode, Length, Value;
        public FirstList(int mode, int length, int value) { Mode = mode; Length = length; Value = value; }
        public int Count { get { Trace = unchecked(Trace * 11 + 1); if (Mode == 1) throw new InvalidOperationException("count"); return Length; } }
        public int this[int index] { get { Trace = unchecked(Trace * 11 + 2 + index); if (Mode == 2) throw new InvalidOperationException("index"); return Value; } set { throw new NotSupportedException(); } }
        public IEnumerator<int> GetEnumerator() { throw new InvalidOperationException("must index"); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool IsReadOnly => true;
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int value) { throw new NotSupportedException(); }
        public void CopyTo(int[] values, int index) { throw new NotSupportedException(); }
        public bool Remove(int value) { throw new NotSupportedException(); }
        public int IndexOf(int value) { throw new NotSupportedException(); }
        public void Insert(int index, int value) { throw new NotSupportedException(); }
        public void RemoveAt(int index) { throw new NotSupportedException(); }
    }
    public struct FirstKey : IEquatable<FirstKey>
    {
        public int Value;
        public bool Equals(FirstKey other) { throw new InvalidOperationException("equality must not run"); }
        public override int GetHashCode() { throw new InvalidOperationException("hash must not run"); }
    }
    public sealed class FirstReferenceState
    {
        public List<ValueVectorBox> Source;
        public int Calls, Trace, Mode;
        public bool Match(ValueVectorBox value)
        {
            Calls++; Trace = unchecked(Trace * 31 + (value == null ? -1 : value.Number));
            if (Calls == 1)
            {
                if (Mode == 1) { Source.Add(new ValueVectorBox(73)); Source[1] = new ValueVectorBox(79); }
                if (Mode == 2) Source.Clear();
                if (Mode == 3) Source.RemoveAt(0);
                if (Mode == 4) { Source.Capacity = 32; Source[1] = new ValueVectorBox(83); }
                if (Mode == 5) Source[1] = new ValueVectorBox(89);
                if (Mode == 6) throw new InvalidOperationException("callback");
            }
            var pressure = new ValueVectorBox(97); Trace = unchecked(Trace + pressure.Number);
            return Calls > 1;
        }
    }
    public static class LinqDirectFirstProbe
    {
        private static int Hash(string value) { int h = 17; for (int i = 0; i < value.Length; i++) h = unchecked(h * 31 + value[i]); return h; }
        private static int Read(IEnumerable<int> source, int mode)
        {
            try { return mode < 0 ? source.First() : source.FirstOrDefault(); }
            catch (ArgumentNullException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
            catch (InvalidOperationException e) { return Hash(e.Message); }
        }
        public static int Arrays(int a, int b) => Read(a == 0 ? new int[0] : new[] { a, b }, b);
        public static int Null(int a, int b) => Read(null, b);
        public static int Lists(int a, int b)
        {
            var values = new List<int>(); if (a != 0) { values.Add(a); values.Add(b); }
            return Read(values, b);
        }
        public static int Indexed(int a, int b)
        {
            var source = new FirstList(b & 3, a < 0 ? -1 : a == 0 ? 0 : 3, a);
            int value = Read(source, b); return unchecked(value + source.Trace * 37);
        }
        public static int Fallback(int a, int b)
        {
            var source = new FirstSequence(b & 7, a == 0 ? 0 : 3, a);
            int value = Read(source, b); return unchecked(value + source.Trace * 37);
        }
        public static int Collection(int a, int b)
        {
            var source = new PredicateCollection(); int value = Read(source, b);
            return unchecked(value + source.Trace * 37);
        }
        public static int References(int a, int b)
        {
            var box = new ValueVectorBox(a); var source = a == 0 ? new ValueVectorBox[0] : new[] { b == 0 ? null : box, new ValueVectorBox(b) };
            var value = source.FirstOrDefault();
            if (a != 0 && source.First() != value) return -101;
            return value == null ? 7 : value == box ? value.Number : -103;
        }
        public static int Values(int a, int b)
        {
            var source = a == 0 ? new SelectCell[0] : new[] { new SelectCell { Wide = (long)a * 4294967297L, Number = b, Reference = new ValueVectorBox(a) } };
            var value = source.FirstOrDefault();
            return unchecked((int)(value.Wide >> 32) + value.Number + (value.Reference == null ? 7 : value.Reference.Number));
        }
        public static int Distinct(int a, int b)
        {
            var source = a == 0 ? new FirstKey[0] : new[] { new FirstKey { Value = a }, new FirstKey { Value = b } };
            return source.Distinct().FirstOrDefault().Value;
        }
        public static int WhereSelect(int a, int b)
        {
            var predicate = new PredicateState(a, int.MinValue); var selector = new SelectorState(b, int.MinValue);
            var query = new[] { a, b, 3 }.Where(predicate.Match).Select(selector.Apply);
            int value = Read(query, b); var e = query.GetEnumerator();
            if ((object)e != query) return -101; e.Dispose();
            return unchecked(value + predicate.Calls * 37 + selector.Trace * 53);
        }
        public static int ExistingCursor(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var query = new[] { a, b, 3 }.Select(state.Apply);
            var e = query.GetEnumerator(); e.MoveNext(); int value = Read(query, b);
            e.MoveNext(); value = unchecked(value + e.Current); e.Dispose();
            return unchecked(value + state.Trace * 37);
        }
        public static int Concat(int a, int b)
        {
            var first = new FirstSequence(0, a == 0 ? 0 : 2, a); var second = new FirstSequence(b & 7, 2, b);
            int value = Read(first.Concat(second), b);
            return unchecked(value + first.Trace * 37 + second.Trace * 53);
        }
        public static int Ordered(int a, int b)
        {
            var state = new SelectorState(a, int.MinValue); var query = new[] { a, b, 3 }.OrderBy(state.Apply).ThenBy(state.Second);
            int value = Read(query, b); return unchecked(value + state.Trace * 37 + state.Calls * 53);
        }
        public static int ListTerminals(int mode, int kind)
        {
            var state = new ListQueryState(); state.Mode = mode; state.Source = new List<int>();
            state.Source.Add(2); state.Source.Add(3); state.Source.Add(5); state.Source.Add(7);
            int shape = kind < 0 ? -kind : kind; int value;
            try
            {
                if (shape == 0 || shape == 4) value = kind < 0 ? state.Source.First(state.Match) : state.Source.FirstOrDefault(state.Match);
                else
                {
                    IEnumerable<int> query = state.Source.Where(state.Match);
                    if (shape == 2) query = query.Select(state.Next);
                    if (shape == 3) query = query.Where(state.Second).Select(state.Next).Select(state.Next);
                    value = kind < 0 ? query.First() : query.FirstOrDefault();
                }
            }
            catch (InvalidOperationException e) { value = Hash(e.Message); }
            return unchecked(value + state.Trace * 37 + state.Calls * 53 + state.Source.Count);
        }
        public static int ListReferences(int mode, int kind)
        {
            var state = new FirstReferenceState(); state.Mode = mode; state.Source = new List<ValueVectorBox>();
            state.Source.Add(new ValueVectorBox(2)); state.Source.Add(new ValueVectorBox(3));
            state.Source.Add(new ValueVectorBox(5)); state.Source.Add(new ValueVectorBox(7));
            int result;
            try
            {
                ValueVectorBox value;
                if (kind < 0) value = state.Source.First(state.Match);
                else value = state.Source.Where(state.Match).FirstOrDefault();
                result = value == null ? -1 : value.Number;
            }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + state.Trace * 37 + state.Calls * 53 + state.Source.Count);
        }
        public static int OfType(int a, int b)
        {
            IEnumerable source = new object[] { null, "abc", new ValueVectorBox(a), new ValueVectorBox(b) };
            return source.OfType<ValueVectorBox>().First().Number;
        }
    }
}
