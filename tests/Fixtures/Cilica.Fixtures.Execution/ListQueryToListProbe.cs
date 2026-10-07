using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class ListQueryToListProbe
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
            try { var values = Query(state, kind).ToList(); result = values.Count + values.Capacity; for (int i = 0; i < values.Count; i++) result = unchecked(result * 7 + values[i]); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            return unchecked(result + state.Trace + state.Calls * 13 + state.Source.Count);
        }
        public static int OriginalCursor(int mode, int kind)
        {
            var state = State(0); var query = Query(state, kind); var cursor = query.GetEnumerator(); cursor.MoveNext(); int current = cursor.Current;
            state.Mode = mode; state.Calls = 0; int result = 0;
            try { var values = query.ToList(); result = values.Count + values.Capacity; for (int i = 0; i < values.Count; i++) result = unchecked(result * 7 + values[i]); }
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
            try { var values = query.ToList(); result = values.Count + values.Capacity; for (int i = 0; i < values.Count; i++) result = unchecked(result * 7 + (values[i] == null ? -99 : values[i].Number)); }
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
            try { var values = query.ToList(); result = values.Count + values.Capacity; for (int i = 0; i < values.Count; i++) result = unchecked(result * 7 + values[i].Number + (int)(values[i].Wide >> 32) + (values[i].Reference == null ? -99 : values[i].Reference.Number)); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            return unchecked(result + state.Trace + state.Calls * 13 + state.Source.Count);
        }
        public static int CorruptCount(int mode, int kind)
        {
            var state = State(0); state.Source.RemoveAt(0); state.Source.RemoveAt(0);
            var source = new CorruptRangeCollection(); source.Target = state.Source; state.Source.AddRange(source);
            try { return Query(state, kind).ToList().Count; }
            catch (InvalidOperationException error) { return Hash(error.Message); }
        }
    }
}
