using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class ConcatTrace
    {
        public int Value;
        public void Add(int value) { Value = unchecked(Value * 31 + value); }
        public int Project(int value) { Add(9); return unchecked(value + 3); }
        public bool KeepValue(ValueVectorCell value) { var pressure = new ValueVectorBox(value.Number); Add(pressure.Number); return true; }
        public bool Keep(int value) { Add(10); return true; }
    }
    public class ConcatSequence : IEnumerable<int>
    {
        public ConcatTrace Trace;
        public int Id;
        public int Limit;
        public int Mode;
        public ConcatSequence(ConcatTrace trace, int id, int limit, int mode) { Trace = trace; Id = id; Limit = limit; Mode = mode; }
        public IEnumerator<int> GetEnumerator()
        {
            Trace.Add(Id * 100 + 1); if (Mode == 1) throw new InvalidOperationException("get");
            if (Mode == 2) return null; return new ConcatCursor(this);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public sealed class ConcatCursor : IEnumerator<int>
    {
        private ConcatSequence source;
        private int index;
        public ConcatCursor(ConcatSequence source) { this.source = source; }
        public bool MoveNext() { source.Trace.Add(source.Id * 100 + 2); if (source.Mode == 3) throw new InvalidOperationException("move"); return index++ < source.Limit; }
        public int Current { get { source.Trace.Add(source.Id * 100 + 3); if (source.Mode == 4) throw new InvalidOperationException("current"); return source.Id * 10 + index; } }
        object IEnumerator.Current => Current;
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose() { source.Trace.Add(source.Id * 100 + 4); if (source.Mode == 5) throw new InvalidOperationException("dispose"); }
    }
    public sealed class ConcatCollection : ConcatSequence, ICollection<int>
    {
        public ConcatCollection(ConcatTrace trace, int id, int limit, int mode) : base(trace, id, limit, mode) { }
        public int Count { get { Trace.Add(Id * 100 + 5); if (Mode == 6) throw new InvalidOperationException("count"); return Mode == 8 ? int.MaxValue : Limit; } }
        public bool IsReadOnly => true;
        public void CopyTo(int[] array, int index)
        {
            Trace.Add(Id * 100 + 6); Trace.Add(array.Length); Trace.Add(index);
            if (Mode == 7) throw new InvalidOperationException("copy");
            for (int i = 0; i < Limit; i++) array[index + i] = Id * 10 + i + 1;
        }
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int value) { throw new NotSupportedException(); }
        public bool Remove(int value) { throw new NotSupportedException(); }
    }
    public static class LinqConcatProbe
    {
        private static int Hash(string text) { int result = 17; for (int i = 0; i < text.Length; i++) result = unchecked(result * 7 + text[i]); return result; }
        private static int Sum(int[] values) { int result = values.Length; for (int i = 0; i < values.Length; i++) result = unchecked(result * 7 + values[i]); return result; }
        private static int[] Items(int count, int start) { var values = new int[count]; for (int i = 0; i < count; i++) values[i] = unchecked(start + i); return values; }
        private static int Read(IEnumerable<int> query)
        {
            int result = 0; try { foreach (int item in query) result = unchecked(result * 7 + item); }
            catch (InvalidOperationException e) { return Hash(e.Message); } catch (NullReferenceException) { return 991; }
            return result;
        }
        private static int Materialize(IEnumerable<int> query)
        {
            try { return Sum(query.ToArray()); } catch (InvalidOperationException e) { return Hash(e.Message); }
            catch (OverflowException) { return 997; } catch (NullReferenceException) { return 991; }
        }
        public static int Arrays(int a, int b) => Sum(Items(a & 3, a).Concat(Items(b & 3, b)).ToArray());
        public static int EmptyAndNull(int a, int b)
        {
            var values = Items(2, b); IEnumerable<int> left = a < 0 ? null : new int[0], right = b < 0 ? null : values;
            try
            {
                var query = left.Concat(right);
                return ((object)query == right ? 7 : 0) + ((object)values.Concat(new int[0]) == values ? 11 : 0);
            }
            catch (ArgumentNullException e) { return unchecked(Hash(e.ParamName) + Hash(e.Message)); }
        }
        public static int Cursor(int a, int b)
        {
            var trace = new ConcatTrace(); var query = new ConcatSequence(trace, 1, a & 3, 0).Concat(new ConcatSequence(trace, 2, b & 3, 0));
            if (trace.Value != 0) return -100;
            var unstarted = (IEnumerator<int>)query; int result = unstarted.Current;
            if (unstarted.MoveNext()) return -101;
            var first = query.GetEnumerator(); var second = query.GetEnumerator();
            if ((object)first != query || first == second || trace.Value != 0) return -102;
            if (first.MoveNext()) result = unchecked(result + first.Current);
            if (second.MoveNext()) result = unchecked(result * 7 + second.Current);
            try { first.Reset(); } catch (NotSupportedException) { result = unchecked(result + 19); }
            first.Dispose(); second.Dispose();
            return unchecked(result + first.Current + trace.Value * 37 + Read(query));
        }
        public static int Faults(int a, int b)
        {
            var trace = new ConcatTrace(); var query = new ConcatSequence(trace, 1, a & 1, (int)((uint)a % 6)).Concat(new ConcatSequence(trace, 2, 2, (int)((uint)b % 6)));
            int result = Read(query); return unchecked(result + trace.Value * 37);
        }
        private static IEnumerable<int> Build(ConcatTrace trace, int a, int b, int shape)
        {
            IEnumerable<int> first, second;
            if ((shape & 1) != 0) first = new ConcatCollection(trace, 1, a & 3, (int)((uint)a % 8)); else first = new ConcatSequence(trace, 1, a & 3, (int)((uint)a % 6));
            if ((shape & 2) != 0) second = new ConcatCollection(trace, 2, b & 3, (int)((uint)b % 8)); else second = new ConcatSequence(trace, 2, b & 3, (int)((uint)b % 6));
            return first.Concat(second);
        }
        private static int Copy(int a, int b, int shape)
        { var trace = new ConcatTrace(); int result = Materialize(Build(trace, a, b, shape)); return unchecked(result + trace.Value * 37); }
        public static int BothCollections(int a, int b) => Copy(a, b, 3);
        public static int FirstCollection(int a, int b) => Copy(a, b, 1);
        public static int SecondCollection(int a, int b) => Copy(a, b, 2);
        public static int NeitherCollection(int a, int b) => Copy(a, b, 0);
        public static int CollectionChain(int a, int b)
        {
            var trace = new ConcatTrace(); var left = new ConcatCollection(trace, 1, a & 3, (int)((uint)a % 9));
            var second = new ConcatCollection(trace, 2, b & 3, (int)((uint)b % 8));
            var query = left.Concat(second).Concat(new ConcatCollection(trace, 3, 2, 0)).Concat(new ConcatCollection(trace, 4, b & 1, 0));
            int result = Materialize(query); return unchecked(result + trace.Value * 37);
        }
        public static int MixedChain(int a, int b)
        {
            var trace = new ConcatTrace(); var first = new ConcatSequence(trace, 1, 7 + (a & 7), 0);
            var second = new ConcatCollection(trace, 2, 1 + (b & 31), 0);
            var query = first.Concat(Items(1, a)).Concat(second).Concat(Items(3, b));
            int result = Materialize(query); return unchecked(result + trace.Value * 37);
        }
        public static int Cardinality(int a, int b)
        {
            var trace = new ConcatTrace(); var query = Build(trace, a, b, a & 3); int result;
            try { result = b < 0 ? query.Count() : query.Any() ? 17 : 19; }
            catch (InvalidOperationException e) { result = Hash(e.Message); } catch (OverflowException) { result = 997; } catch (NullReferenceException) { result = 991; }
            return unchecked(result + trace.Value * 37);
        }
        public static int ChainCardinality(int a, int b)
        {
            var trace = new ConcatTrace(); var query = Build(trace, a, b, a & 3).Concat(new ConcatCollection(trace, 3, 2, 0)); int result;
            try { result = b < 0 ? query.Count() : query.Any() ? 17 : 19; }
            catch (InvalidOperationException e) { result = Hash(e.Message); } catch (OverflowException) { result = 997; } catch (NullReferenceException) { result = 991; }
            return unchecked(result + trace.Value * 37);
        }
        public static int Preservation(int a, int b)
        {
            var query = Items(2, a).Concat(Items(2, b)); int result = query.Count() + (query.Any() ? 7 : 0) + Sum(query.ToArray());
            var cursor = query.GetEnumerator(); if ((object)cursor != query) return -100;
            cursor.MoveNext(); int current = cursor.Current; var copy = query.ToArray();
            if (cursor.Current != current) return -101; cursor.Dispose(); return unchecked(result + Sum(copy));
        }
        public static int Projection(int a, int b)
        {
            var trace = new ConcatTrace(); var source = Build(trace, a, b, a & 3); var query = source.Select(trace.Project); int result;
            try { result = b < 0 ? query.Count() : b == 0 ? query.Any() ? 17 : 19 : Sum(query.ToArray()); }
            catch (InvalidOperationException e) { result = Hash(e.Message); } catch (OverflowException) { result = 997; } catch (NullReferenceException) { result = 991; }
            return unchecked(result + trace.Value * 37);
        }
        public static int References(int a, int b)
        {
            var left = new[] { new ValueVectorBox(a) }; var right = new[] { new ValueVectorBox(b), left[0] };
            var result = left.Concat(right).ToArray(); left[0] = null; right[0] = null;
            var pressure = new ValueVectorBox(b); result[0].Number = a;
            return unchecked(result[0].Number + result[1].Number + pressure.Number + (result[0] == result[2] ? 7 : -100));
        }
        public static int Values(int a, int b)
        {
            ValueVectorCell item = default; item.Number = a; item.Wide = ((long)a << 32) + b; item.Reference = new ValueVectorBox(b);
            var source = new[] { item }; var result = source.Concat(source).Concat(source).ToArray(); source[0] = default;
            var copy = result[1]; copy.Number = 99; var pressure = new ValueVectorBox(a);
            return unchecked(result[1].Number + (int)(result[2].Wide >> 32) + result[0].Reference.Number + pressure.Number);
        }
        public static int Nullable(int a, int b)
        {
            int?[] left = new int?[2]; left[0] = a; int?[] right = new int?[1]; right[0] = b;
            var query = left.Concat(right); int result = 0;
            foreach (int? item in query) result = unchecked(result * 7 + item.GetValueOrDefault(19));
            var copy = query.ToArray(); return unchecked(result + copy[2].Value);
        }
        public static int EarlyBreak(int a, int b)
        {
            var trace = new ConcatTrace(); var query = new ConcatSequence(trace, 1, 3, 0).Concat(new ConcatSequence(trace, 2, 3, 1)); int result = 0;
            foreach (int item in query) { result = item; break; }
            return unchecked(result + trace.Value * 37 + a + b);
        }

        public static int ZeroProjection(int a, int b)
        {
            var trace = new ConcatTrace(); var source = new ConcatCollection(trace, 1, 0, 0).Concat(new ConcatCollection(trace, 2, 0, 0));
            var projected = source.Select(trace.Project); var values = projected.ToArray();
            int result = values == Array.Empty<int>() ? 7 : -100;
            var cursor = source.GetEnumerator(); if ((object)cursor != source) return -101; cursor.Dispose();
            return unchecked(result + trace.Value * 37 + a + b);
        }
        public static int ZeroCollections(int a, int b)
        {
            var trace = new ConcatTrace(); var first = new ConcatCollection(trace, 1, 0, 0); var second = new ConcatCollection(trace, 2, 0, 0);
            var two = first.Concat(second).ToArray(); var many = first.Concat(second).Concat(new ConcatCollection(trace, 3, 0, 0)).ToArray();
            return unchecked((two == Array.Empty<int>() ? -100 : 7) + (many == Array.Empty<int>() ? 11 : -101) + trace.Value * 37 + a + b);
        }
        public static int MixedValues(int a, int b)
        {
            var trace = new ConcatTrace(); var values = new ValueVectorCell[9];
            for (int i = 0; i < values.Length; i++) { ValueVectorCell item = default; item.Number = unchecked(a + i); item.Reference = new ValueVectorBox(unchecked(b + i)); values[i] = item; }
            var result = values.Where(trace.KeepValue).Concat(values).Concat(values).ToArray();
            for (int i = 0; i < values.Length; i++) values[i] = default;
            var pressure = new ValueVectorBox(a); int sum = pressure.Number;
            for (int i = 0; i < result.Length; i++) sum = unchecked(sum * 7 + result[i].Number + result[i].Reference.Number);
            return unchecked(sum + trace.Value * 37);
        }
        public static int ListConcat(int a, int b) => new List<int>().Concat(Items(1, b)).ToArray().Length;
    }
}
