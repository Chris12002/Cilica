using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class ListQueryState
    {
        public List<int> Source; public int Mode, Calls, Trace;
        private void Change(int value)
        {
            Trace = unchecked(Trace * 31 + value); Calls++;
            if (Calls != 1) return;
            if (Mode == 1) Source.Add(73);
            if (Mode == 2) Source.Clear();
            if (Mode == 3) Source.RemoveAt(0);
            if (Mode == 4) Source.Insert(0, 79);
            if (Mode == 5) { Source.Capacity = 32; Source[1] = 83; }
            if (Mode == 6) Source[1] = 89;
            if (Mode == 7) { Source.Clear(); Source.Add(97); }
            if (Mode == 8) throw new InvalidOperationException("callback");
            var pressure = new ValueVectorBox(101); Trace = unchecked(Trace + pressure.Number);
        }
        public bool Match(int value) { Change(value); return (value & 1) != 0; }
        public bool Second(int value) { Trace = unchecked(Trace * 13 + value); return true; }
        public int Select(int value) { Change(value); return unchecked(value * 7 + 3); }
        public int Next(int value) { Trace = unchecked(Trace * 11 + value); return unchecked(value * 3 + 1); }
    }
    public sealed class ListQueryReferenceState
    {
        public List<ValueVectorBox> Source; public int Mode, Calls, Trace;
        private void Change(ValueVectorBox value)
        {
            Trace = unchecked(Trace * 31 + (value == null ? -99 : value.Number)); Calls++;
            if (Calls != 1) return;
            if (Mode == 1) { Source.Add(new ValueVectorBox(73)); Source[1] = new ValueVectorBox(79); }
            if (Mode == 2) Source.Clear();
            if (Mode == 3) Source[1] = new ValueVectorBox(83);
            if (Mode == 4) { Source.Capacity = 32; Source[1] = new ValueVectorBox(89); }
            if (Mode == 5) Source.RemoveAt(0);
            if (Mode == 6) throw new InvalidOperationException("callback");
            var pressure = new ValueVectorBox(101); Trace = unchecked(Trace + pressure.Number);
        }
        public bool Match(ValueVectorBox value) { Change(value); return true; }
        public ValueVectorBox Select(ValueVectorBox value) { Change(value); return value; }
        public ValueVectorBox Next(ValueVectorBox value) { var pressure = new ValueVectorBox(103); Trace = unchecked(Trace * 11 + pressure.Number); return value; }
    }
    public sealed class ListQueryValueState
    {
        public List<ValueVectorCell> Source; public int Mode, Calls, Trace;
        private void Change(ValueVectorCell value)
        {
            Trace = unchecked(Trace * 31 + value.Number + (value.Reference == null ? -99 : value.Reference.Number)); Calls++;
            if (Calls != 1) return;
            if (Mode == 1) { Source.Add(default); Source[1] = default; }
            if (Mode == 2) Source.Clear();
            if (Mode == 3) Source[1] = default;
            if (Mode == 4) { Source.Capacity = 32; Source[1] = default; }
            if (Mode == 5) Source.RemoveAt(0);
            if (Mode == 6) throw new InvalidOperationException("callback");
            var pressure = new ValueVectorBox(107); Trace = unchecked(Trace + pressure.Number);
        }
        public bool Match(ValueVectorCell value) { Change(value); return true; }
        public ValueVectorCell Select(ValueVectorCell value) { Change(value); value.Number = unchecked(value.Number + 7); return value; }
        public ValueVectorCell Next(ValueVectorCell value) { var pressure = new ValueVectorBox(109); Trace = unchecked(Trace * 11 + pressure.Number); return value; }
    }
    public sealed class CorruptRangeCollection : ICollection<int>
    {
        public List<int> Target;
        public int Count => 2; public bool IsReadOnly => true;
        public void CopyTo(int[] array, int index) { Target.Add(23); array[index] = 29; array[index + 1] = 31; }
        public IEnumerator<int> GetEnumerator() { throw new NotSupportedException(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(int item) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int item) { throw new NotSupportedException(); }
        public bool Remove(int item) { throw new NotSupportedException(); }
    }
    public static class ListQueryMaterializationProbe
    {
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        private static ListQueryState State(int mode)
        {
            var state = new ListQueryState(); state.Source = new List<int>();
            for (int i = 0; i < 4; i++) state.Source.Add(i * 2 + 1);
            state.Mode = mode; return state;
        }
        private static IEnumerable<int> Query(ListQueryState state, int kind)
        {
            if (kind == 0) return state.Source.Where(state.Match);
            if (kind == 1) return state.Source.Select(state.Select);
            if (kind == 2) return state.Source.Where(state.Match).Select(state.Next);
            if (kind == 3) return state.Source.Select(state.Select).Select(state.Next);
            if (kind == 4) return state.Source.Where(state.Match).Where(state.Second).Select(state.Next);
            return state.Source.Select(state.Select).Where(state.Second);
        }
        public static int Integers(int mode, int kind)
        {
            var state = State(mode); int result = 0;
            try { var values = Query(state, kind).ToArray(); result = values.Length; for (int i = 0; i < values.Length; i++) result = unchecked(result * 7 + values[i]); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            return unchecked(result + state.Trace + state.Calls * 13 + state.Source.Count);
        }
        public static int OriginalCursor(int mode, int kind)
        {
            var state = State(0); var query = Query(state, kind); var cursor = query.GetEnumerator(); cursor.MoveNext(); int current = cursor.Current;
            state.Mode = mode; state.Calls = 0; int result = 0;
            try { var values = query.ToArray(); result = values.Length; for (int i = 0; i < values.Length; i++) result = unchecked(result * 7 + values[i]); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            if (cursor.Current != current) return -100;
            state.Mode = 0;
            try { if (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidOperationException error) { result = unchecked(result + Hash(error.Message)); }
            cursor.Dispose(); return unchecked(result + state.Trace + state.Calls * 13);
        }
        public static int References(int mode, int kind)
        {
            var state = new ListQueryReferenceState(); state.Mode = mode; state.Source = new List<ValueVectorBox>();
            for (int i = 0; i < 4; i++) state.Source.Add(new ValueVectorBox(i * 2 + 1));
            IEnumerable<ValueVectorBox> query;
            if (kind == 0) query = state.Source.Where(state.Match);
            else if (kind == 1) query = state.Source.Select(state.Select);
            else if (kind == 2) query = state.Source.Where(state.Match).Select(state.Next);
            else query = state.Source.Select(state.Select).Select(state.Next);
            int result = 0;
            try { var values = query.ToArray(); result = values.Length; for (int i = 0; i < values.Length; i++) result = unchecked(result * 7 + (values[i] == null ? -99 : values[i].Number)); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            return unchecked(result + state.Trace + state.Calls * 13 + state.Source.Count);
        }
        public static int Values(int mode, int kind)
        {
            var state = new ListQueryValueState(); state.Mode = mode; state.Source = new List<ValueVectorCell>();
            for (int i = 0; i < 4; i++) { ValueVectorCell item = default; item.Number = i * 2 + 1; item.Wide = (long)i << 32; item.Reference = new ValueVectorBox(i + 5); state.Source.Add(item); }
            IEnumerable<ValueVectorCell> query;
            if (kind == 0) query = state.Source.Where(state.Match);
            else if (kind == 1) query = state.Source.Select(state.Select);
            else if (kind == 2) query = state.Source.Where(state.Match).Select(state.Next);
            else query = state.Source.Select(state.Select).Select(state.Next);
            int result = 0;
            try { var values = query.ToArray(); result = values.Length; for (int i = 0; i < values.Length; i++) result = unchecked(result * 7 + values[i].Number + (int)(values[i].Wide >> 32) + (values[i].Reference == null ? -99 : values[i].Reference.Number)); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            return unchecked(result + state.Trace + state.Calls * 13 + state.Source.Count);
        }
        public static int CorruptCount(int mode, int kind)
        {
            var state = State(0); state.Source.RemoveAt(0); state.Source.RemoveAt(0);
            var source = new CorruptRangeCollection(); source.Target = state.Source; state.Source.AddRange(source);
            try { return Query(state, kind).ToArray().Length; }
            catch (InvalidOperationException error) { return Hash(error.Message); }
        }
    }
}
