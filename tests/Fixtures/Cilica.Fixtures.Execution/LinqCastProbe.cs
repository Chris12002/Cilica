using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cilica.Fixtures.Execution
{
    public sealed class CastCollection : ICollection
    {
        public readonly TypeSequence Source;
        public int CountValue;
        public CastCollection(TypeSequence source, int count) { Source = source; CountValue = count; }
        public int Count { get { Source.Trace = unchecked(Source.Trace * 7 + 5); return CountValue; } }
        public IEnumerator GetEnumerator() => Source.GetEnumerator();
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void CopyTo(Array array, int index) { throw new NotSupportedException(); }
    }
    public static class LinqCastProbe
    {
        public static int TypedShortcut(int a, int b)
        {
            var array = new[] { a, b }; var query = ((IEnumerable)array).Cast<int>();
            var references = new[] { new DerivedTypeItem(a) }; var wider = ((IEnumerable)references).Cast<TypeItem>();
            var objects = new DualTypeSequence(null); var same = objects.Cast<object>();
            return unchecked((ReferenceEquals(array, query) && ReferenceEquals(references, wider) && ReferenceEquals(objects, same) ? 17 : -100) + query.Count() + wider.First().Number + objects.Trace);
        }
        public static int References(int a, int b)
        {
            var source = new TypeSequence(new DerivedTypeItem(a), null, new TypeItem(b), 0); var query = source.Cast<TypeItem>();
            var array = query.ToArray(); var list = query.ToList();
            return unchecked(array[0].Number + array[2].Number * 3 + list.Count + (array[1] == null ? 7 : -100) + source.Trace * 37);
        }
        public static int Scalars(int a, int b)
        {
            var source = new TypeSequence(a, b, 7, 0); var values = source.Cast<int>().ToArray();
            return unchecked(values[0] + values[1] * 3 + values[2] * 5 + source.Trace * 37);
        }
        public static int Nullable(int a, int b)
        {
            var source = new TypeSequence(a, null, b, 0); var values = source.Cast<int?>().ToArray();
            return unchecked(values[0].Value + values[1].GetValueOrDefault(11) + values[2].Value * 3 + source.Trace * 37);
        }
        public static int Values(int a, int b)
        {
            TypeValue value = default; value.Number = a; value.Reference = new ValueVectorBox(b);
            var source = new TypeSequence(value, value, value, 0); var query = source.Cast<TypeValue>(); var values = query.ToArray();
            values[0].Number = b; values[0].Reference.Number = a;
            return unchecked(values[0].Number + values[1].Number * 3 + values[2].Reference.Number * 5 + source.Trace * 37);
        }
        public static int DeferredStates(int a, int b)
        {
            var source = new TypeSequence(a, b, 7, 0); var query = source.Cast<int>();
            var first = query.GetEnumerator(); var other = query.GetEnumerator();
            int result = ReferenceEquals(query, first) && !ReferenceEquals(first, other) && source.Trace == 0 ? 17 : -100;
            first.MoveNext(); result = unchecked(result + first.Current); first.Dispose();
            result = unchecked(result + first.Current * 3 + (first.MoveNext() ? -101 : 11));
            other.Dispose(); if (other.MoveNext()) result = unchecked(result + other.Current * 5); other.Dispose();
            try { ((IEnumerator)first).Reset(); return -102; } catch (NotSupportedException) { result = unchecked(result + 13); }
            return unchecked(result + source.Trace * 37);
        }
        public static int Faults(int a, int b)
        {
            var source = new TypeSequence(a, (long)b, null, (b & 7) % 6); var cursor = source.Cast<int>().GetEnumerator(); int result = 0;
            try { while (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidCastException) { result = unchecked(result + 11); }
            catch (NullReferenceException) { result = unchecked(result + 13); }
            catch (InvalidOperationException e) { result = unchecked(result + (e.Message == "dispose" ? 17 : 19)); }
            result = unchecked(result + source.Trace * 37);
            try { cursor.Dispose(); } catch (InvalidOperationException) { result = unchecked(result + 23); }
            return unchecked(result + source.Trace * 53);
        }
        public static int CollectionTerminals(int a, int b)
        {
            var source = new TypeSequence(a, b, 7, 0); var collection = new CastCollection(source, 3); var query = collection.Cast<int>();
            int result = query.Count() + (query.Any() ? 11 : 13) + query.First() + (query.Contains(b) ? 17 : 19);
            var array = query.ToArray(); var list = query.ToList(); var cursor = query.GetEnumerator();
            result = unchecked(result + (ReferenceEquals(query, cursor) ? 23 : -100)); cursor.Dispose();
            return unchecked(result + array[1] * 3 + list[0] * 5 + source.Trace * 37);
        }
        public static int CollectionStates(int a, int b)
        {
            var source = new TypeSequence(a, b, 7, 0); var query = new CastCollection(source, 3).Cast<int>(); var cursor = query.GetEnumerator();
            cursor.MoveNext(); int result = cursor.Current; cursor.Dispose();
            result = unchecked(result + cursor.Current * 3 + (cursor.MoveNext() ? -100 : 7));
            var other = query.GetEnumerator(); other.Dispose(); if (other.MoveNext()) return -101;
            return unchecked(result + source.Trace * 37);
        }
        public static int CollectionFaults(int a, int b)
        {
            var source = new TypeSequence(a, (long)b, null, (b & 7) % 6); var cursor = new CastCollection(source, 3).Cast<int>().GetEnumerator(); int result = 0;
            try { while (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidCastException) { result = unchecked(result + 11); }
            catch (NullReferenceException) { result = unchecked(result + 13); }
            catch (InvalidOperationException e) { result = unchecked(result + (e.Message == "dispose" ? 17 : 19)); }
            result = unchecked(result + source.Trace * 37);
            try { cursor.Dispose(); } catch (InvalidOperationException) { result = unchecked(result + 23); }
            return unchecked(result + source.Trace * 53);
        }
        public static int CollectionSizing(int a, int b)
        {
            var source = new TypeSequence(a, b, 7, 0); var query = new CastCollection(source, (b & 3) - 1).Cast<int>(); int result = 0;
            try { var values = query.ToArray(); result = values.Length; }
            catch (OverflowException) { result = 11; } catch (IndexOutOfRangeException) { result = 13; }
            try { result = unchecked(result + query.ToList().Count); }
            catch (ArgumentOutOfRangeException e) { result = unchecked(result + (e.ParamName == "capacity" ? 17 : -100)); }
            return unchecked(result + source.Trace * 37);
        }
        public static int Projection(int a, int b)
        {
            var source = new TypeSequence(new TypeItem(a), new TypeItem(b), new TypeItem(7), 0); var query = new CastCollection(source, 3).Cast<TypeItem>();
            var selector = new TypeSelector(); var projected = query.Select(selector.Item);
            int result = projected.Any() ? 11 : 13; result = unchecked(result + projected.First() + projected.Count());
            var values = projected.ToArray(); var cursor = query.GetEnumerator();
            result = unchecked(result + (ReferenceEquals(query, cursor) ? 17 : 19)); cursor.Dispose();
            return unchecked(result + values[1] * 3 + source.Trace * 37 + selector.Calls * 53);
        }
        public static int ReadOnlyDowncast(int a, int b)
        {
            var list = new List<TypeItem>(); list.Add(new DerivedTypeItem(a)); list.Add(new DerivedTypeItem(b));
            var view = new ReadOnlyCollection<TypeItem>(list); var values = view.Cast<DerivedTypeItem>().ToArray();
            return unchecked(values[0].Number + values[1].Number * 3);
        }
        public static int CollectionCardinality(int a, int b)
        {
            var source = new TypeSequence(a, null, (long)b, 0);
            if (a < 0) source.First = (long)a;
            var query = new CastCollection(source, (b & 7) - 2).Cast<int>(); int result = query.Count();
            try { result = unchecked(result + (query.Any() ? 11 : 13)); }
            catch (InvalidCastException) { result = unchecked(result + 17); }
            return unchecked(result + source.Trace * 37);
        }
        public static int ProjectionSizing(int a, int b)
        {
            var source = new TypeSequence(new TypeItem(a), new TypeItem(b), new TypeItem(7), 0);
            var query = new CastCollection(source, (b & 7) - 2).Cast<TypeItem>(); var selector = new TypeSelector(); int result = 0;
            try { var values = query.Select(selector.Item).ToArray(); result = values.Length; }
            catch (OverflowException) { result = 11; } catch (IndexOutOfRangeException) { result = 13; }
            var cursor = query.GetEnumerator(); result = unchecked(result + (ReferenceEquals(query, cursor) ? 17 : 19)); cursor.Dispose();
            return unchecked(result + source.Trace * 37 + selector.Calls * 53);
        }
        public static int EmptyArray(int a, int b)
        {
            var query = ((IEnumerable)new object[0]).Cast<int>(); var first = query.ToArray(); var second = query.ToArray();
            var cursor = query.GetEnumerator(); int result = ReferenceEquals(query, cursor) ? 7 : -100; cursor.Dispose();
            return unchecked(result + (ReferenceEquals(first, second) || ReferenceEquals(first, Array.Empty<int>()) ? -101 : 11) + a + b);
        }
        public static int NullAndMismatch(int a, int b)
        {
            int result = 0; try { ((IEnumerable)null).Cast<int>(); } catch (ArgumentNullException e) { result = e.ParamName == "source" ? 7 : -100; }
            var source = new TypeSequence(null, a, b, 0); var query = source.Cast<int>();
            try { query.First(); } catch (NullReferenceException) { result = unchecked(result + 11); }
            return unchecked(result + source.Trace * 37);
        }
    }
}
