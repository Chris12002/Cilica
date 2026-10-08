using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class IndexedSelectState
    {
        public int Trace, Cutoff, Mode;
        public int[] Array;
        public List<int> List;
        public IndexedSelectState(int cutoff, int mode) { Cutoff = cutoff; Mode = mode; }
        public int Match(int value, int index)
        {
            Trace = unchecked(Trace * 31 + value * 7 + index);
            if ((Mode == 6 || Mode == 7) && index == 1) throw new InvalidOperationException("selector");
            if (Array != null && index == 0) Array[1] = Cutoff;
            if (List != null && index == 0) List.Add(Cutoff);
            var allocation = new ValueVectorBox(value);
            return unchecked(allocation.Number * 17 + index + Cutoff);
        }
        public ValueVectorBox Reference(ValueVectorBox value, int index)
        {
            Trace = unchecked(Trace * 31 + index);
            var allocation = new ValueVectorBox(index);
            return value;
        }
        public ValueVectorCell Value(ValueVectorCell value, int index)
        {
            Trace = unchecked(Trace * 31 + index);
            var allocation = new ValueVectorBox(index);
            value.Number = unchecked(value.Number + index); return value;
        }
        public bool Positive(int value) => value > Cutoff;
    }
    public static class LinqIndexedSelectProbe
    {
        public static int State(int a, int b)
        {
            var state = new IndexedSelectState(a, 0);
            var source = new IndexedWhereSource(0);
            var query = source.Select(state.Match);
            int result = source.Trace + state.Trace;
            var first = query.GetEnumerator(); var second = query.GetEnumerator();
            result += ReferenceEquals(query, first) ? 1 : 10;
            result += ReferenceEquals(first, second) ? 100 : 2;
            result += first.Current * 17 + source.Trace * 29;
            if (first.MoveNext()) result += first.Current * 31;
            first.Dispose(); first.Dispose();
            result += first.Current * 37 + (first.MoveNext() ? 1000 : 3);
            try { first.Reset(); } catch (NotSupportedException) { result += 5; }
            second.Dispose();
            return unchecked(result + source.Trace * 41 + state.Trace * 43);
        }
        public static int Clones(int a, int b)
        {
            var state = new IndexedSelectState(a, 0);
            var query = new[] { a, b, 3 }.Select(state.Match);
            // Disposing the unstarted enumerable does not consume first reuse.
            ((IDisposable)query).Dispose();
            var first = query.GetEnumerator();
            int result = ReferenceEquals(first, query) ? 1 : 10;
            first.Dispose(); // Cancels this cursor; the next GetEnumerator can reuse it.
            if (first.MoveNext()) result += first.Current * 3;
            var second = query.GetEnumerator();
            if (second.MoveNext()) result += second.Current * 5;
            while (first.MoveNext()) result = unchecked(result * 7 + first.Current);
            result = unchecked(result + first.Current * 11);
            first.Dispose(); second.Dispose();
            return unchecked(result + second.Current * 13 + state.Trace * 17);
        }
        public static int Faults(int a, int b)
        {
            int mode = a & 7;
            var source = new IndexedWhereSource(mode);
            var state = new IndexedSelectState(-1, mode);
            var cursor = source.Select(state.Match).GetEnumerator();
            int result = 0;
            try
            {
                if (cursor.MoveNext()) result = cursor.Current;
                if (b < 0) cursor.Dispose();
                else while (cursor.MoveNext()) result = unchecked(result * 7 + cursor.Current);
            }
            catch (InvalidOperationException error)
            {
                result += error.Message == "get" ? 10 : error.Message == "move" ? 20 : error.Message == "current" ? 30 : error.Message == "dispose" ? 40 : error.Message == "selector" ? 50 : -100;
            }
            catch (NullReferenceException) { result += 60; }
            cursor.Dispose(); cursor.Dispose();
            result += cursor.Current * 11 + (cursor.MoveNext() ? 1000 : 3);
            return unchecked(result + source.Trace * 17 + state.Trace * 19);
        }
        public static int Terminals(int a, int b)
        {
            var state = new IndexedSelectState(a, 0);
            var query = new[] { a, b, 1, 2, 3 }.Select(state.Match);
            var cursor = query.GetEnumerator(); cursor.MoveNext();
            int result = query.Count(state.Positive) + (query.Any() ? 7 : 11);
            var array = query.ToArray(); var list = query.ToList();
            result = unchecked(result + array.Length * 13 + list.Count * 17 + cursor.Current * 19);
            while (cursor.MoveNext()) result = unchecked(result * 23 + cursor.Current);
            cursor.Dispose();
            return unchecked(result + state.Trace * 29);
        }
        public static int Mutation(int a, int b)
        {
            var state = new IndexedSelectState(a, 0);
            var array = new[] { b, 1, 2 }; state.Array = array;
            int result = array.Select(state.Match).Count();
            state.Array = null; var list = new List<int>(); list.Add(b); list.Add(1); state.List = list;
            var cursor = list.Select(state.Match).GetEnumerator();
            try { while (cursor.MoveNext()) result = unchecked(result * 7 + cursor.Current); }
            catch (InvalidOperationException) { result += 13; }
            cursor.Dispose();
            return unchecked(result + cursor.Current * 17 + state.Trace * 19);
        }
        public static int References(int a, int b)
        {
            var state = new IndexedSelectState(a, 0);
            var values = new[] { new ValueVectorBox(b), null, new ValueVectorBox(a) };
            var cursor = values.Select(state.Reference).GetEnumerator(); int result = 0;
            while (cursor.MoveNext()) result = unchecked(result * 7 + (cursor.Current == null ? 3 : cursor.Current.Number));
            cursor.Dispose(); var allocation = new ValueVectorBox(b);
            result = unchecked(result + (cursor.Current == null ? 5 : cursor.Current.Number) * 11 + allocation.Number);
            return unchecked(result + state.Trace * 13);
        }
        public static int Values(int a, int b)
        {
            var state = new IndexedSelectState(a, 0);
            var values = new ValueVectorCell[2];
            values[0].Number = a; values[0].Reference = new ValueVectorBox(b);
            values[1].Number = b; values[1].Reference = new ValueVectorBox(a);
            var cursor = values.Select(state.Value).GetEnumerator(); int result = 0;
            while (cursor.MoveNext()) result = unchecked(result * 7 + cursor.Current.Number + cursor.Current.Reference.Number);
            cursor.Dispose(); var allocation = new ValueVectorBox(b);
            return unchecked(result + cursor.Current.Number * 11 + (cursor.Current.Reference == null ? 3 : cursor.Current.Reference.Number) * 13 + allocation.Number + state.Trace * 17);
        }
        public static int FaultReuse(int a, int b)
        {
            var source = new IndexedWhereSource(0);
            var state = new IndexedSelectState(-1, 6);
            var query = source.Select(state.Match); var cursor = query.GetEnumerator();
            cursor.MoveNext(); int result = 0;
            try { cursor.MoveNext(); }
            catch (InvalidOperationException) { result = 1; }
            // Fault cleanup and explicit disposal permit reuse without clearing Current.
            result += ReferenceEquals(query, query.GetEnumerator()) ? 3 : 30;
            cursor.Dispose();
            return unchecked(result + cursor.Current * 11 + source.Trace * 13);
        }
        public static IEnumerator<ValueVectorBox> MakeTracked()
        {
            var state = new IndexedSelectState(int.MinValue, 0);
            var values = new[] { new ValueVectorBox(17) };
            var cursor = values.Select(state.Reference).GetEnumerator(); cursor.MoveNext();
            values[0] = new ValueVectorBox(23); return cursor;
        }
        public static void StopTracked(IEnumerator<ValueVectorBox> cursor) { cursor.Dispose(); }
        public static int Tracked(int a, int b) { var cursor = MakeTracked(); StopTracked(cursor); return a; }
        public static int DifferentResult(int a,int b)
        {
            var query=new[]{a,b,3}.Select((value,index)=>new ValueVectorCell {Number=unchecked(value+index),Reference=new ValueVectorBox(index)});
            var array=query.ToArray();int result=0;
            for(int i=0;i<array.Length;i++)result=unchecked(result*31+array[i].Number+array[i].Reference.Number);
            return result;
        }
        public static int Legacy(int a,int b)
        {
            var state=new IndexedSelectState(a,0);
            IEnumerator cursor=((IEnumerable)new[]{a,b}.Select(state.Match)).GetEnumerator();
            int result=(int)cursor.Current;
            while(cursor.MoveNext())result=unchecked(result*31+(int)cursor.Current);
            ((IDisposable)cursor).Dispose();return unchecked(result+(int)cursor.Current*17);
        }
        public static int EmptyAndNull(int a, int b)
        {
            var state = new IndexedSelectState(a, 0); var empty = new int[0];
            var query = empty.Select(state.Match); int result = ReferenceEquals(query, Array.Empty<int>()) ? 1 : 10;
            IEnumerable<int> source = a < 0 ? null : empty; Func<int, int, int> selector = null;
            try { source.Select(selector); } catch (ArgumentNullException error) { result += error.ParamName == "source" ? 3 : error.ParamName == "selector" ? 5 : -100; }
            return result + state.Trace + query.Count();
        }
    }
}
