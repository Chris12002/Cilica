// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned LINQ sources; see provenance.
using System.Collections;
using System.Collections.Generic;
namespace System.Linq
{
    public static partial class Enumerable
    {
        private static bool NoElements(int length) => length == 0;
        public static IEnumerable<TSource> Concat<TSource>(IEnumerable<TSource> first, IEnumerable<TSource> second)
        {
            if (first == null) throw new ArgumentNullException("first");
            if (second == null) throw new ArgumentNullException("second");
            if (first is TSource[] left && NoElements(left.Length)) return second;
            if (second is TSource[] right && NoElements(right.Length)) return first;
            return first is Cilica.Runtime.ConcatIterator<TSource> chain ? chain.Append(second) : new Cilica.Runtime.ConcatIterator<TSource>(first, second);
        }
        public static IEnumerable<TResult> OfType<TResult>(IEnumerable source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (default(TResult) != null && source is IEnumerable<TResult> typed) return typed;
            return new Cilica.Runtime.OfTypeIterator<TResult>(source);
        }
        public static IEnumerable<TSource> Distinct<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source is TSource[] array) return Cilica.Runtime.DistinctIterator<TSource>.FromArray(array, array.Length);
            return new Cilica.Runtime.DistinctIterator<TSource>(source);
        }
        public static IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (selector == null) throw new ArgumentNullException("selector");
            if (source is TSource[] array)
                return Cilica.Runtime.SelectIterator<TSource, TResult>.FromArray(array, selector, array.Length);
            return new Cilica.Runtime.SelectIterator<TSource, TResult>(source, null, selector);
        }
        public static TSource SingleOrDefault<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate) =>
            TryGetSingle(source, predicate, out _);
        public static TSource Single<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            TSource value = TryGetSingle(source, predicate, out int found);
            if (found == 0) throw new InvalidOperationException("Sequence contains no matching element");
            return value;
        }
        private static TSource TryGetSingle<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate, out int found)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (source is TSource[] array)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    TSource result = array[i];
                    if (predicate(result))
                    {
                        for (i++; i < array.Length; i++)
                            if (predicate(array[i])) throw new InvalidOperationException("Sequence contains more than one matching element");
                        found = 1; return result;
                    }
                }
            }
            else
            {
                using (IEnumerator<TSource> e = source.GetEnumerator())
                {
                    while (e.MoveNext())
                    {
                        TSource result = e.Current;
                        if (predicate(result))
                        {
                            while (e.MoveNext())
                                if (predicate(e.Current)) throw new InvalidOperationException("Sequence contains more than one matching element");
                            found = 1; return result;
                        }
                    }
                }
            }
            found = 0; return default;
        }
        public static TSource FirstOrDefault<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return Cilica.Runtime.QueryFirst<TSource>.Read(source, out _);
        }
        public static TSource First<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            TSource value = Cilica.Runtime.QueryFirst<TSource>.Read(source, out int found);
            if (found == 0) throw new InvalidOperationException("Sequence contains no elements");
            return value;
        }
        public static TSource FirstOrDefault<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate) =>
            TryGetFirst(source, predicate, out _);
        public static TSource First<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            TSource value = TryGetFirst(source, predicate, out int found);
            if (found == 0) throw new InvalidOperationException("Sequence contains no matching element");
            return value;
        }
        // Int32 flag keeps this private helper within the admitted by-reference carriers.
        private static TSource TryGetFirst<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate, out int found)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            TSource[] array = source as TSource[];
            int length = array == null ? 0 : array.Length;
            if (array == null && source is List<TSource> list) array = list.QueryStorage(out length);
            if (array != null)
            {
                for (int i = 0; i < length; i++)
                {
                    TSource item = array[i];
                    if (predicate(item)) { found = 1; return item; }
                }
            }
            else
            {
                foreach (TSource item in source)
                    if (predicate(item)) { found = 1; return item; }
            }
            found = 0; return default;
        }
        public static List<TSource> ToList<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source is Cilica.Runtime.IQueryMaterialization<TSource> query) return query.ToList();
            return List<TSource>.CopyEnumerable(source);
        }
        public static TSource[] ToArray<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source is Cilica.Runtime.IQueryMaterialization<TSource> query) return query.ToArray();
            if (source is ICollection<TSource> collection)
            {
                int count = collection.Count;
                if (count == 0) return Array.Empty<TSource>();
                var result = new TSource[count];
                collection.CopyTo(result, 0);
                return result;
            }
            var builder = new Cilica.Runtime.MaterializationBuffer<TSource>();
            foreach (TSource item in source) builder.Add(item);
            return builder.ToArray();
        }
        public static IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (source is Cilica.Runtime.WhereIterator<TSource> iterator) return iterator.Filter(predicate);
            if (source is TSource[] array) return Cilica.Runtime.WhereIterator<TSource>.FromArray(array, predicate, array.Length);
            return new Cilica.Runtime.WhereIterator<TSource>(source, null, predicate, 0);
        }
        public static IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (source is TSource[] array) return Cilica.Runtime.IndexedWhereIterator<TSource>.FromArray(array, predicate, array.Length);
            return new Cilica.Runtime.IndexedWhereIterator<TSource>(source, predicate);
        }
        public static bool Any<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source is ICollection<TSource> generic) return generic.Count != 0;
            if (source is Cilica.Runtime.IQueryCardinality query) return query.Any();
            if (source is ICollection legacy) return legacy.Count != 0;
            using (IEnumerator<TSource> e = source.GetEnumerator()) return e.MoveNext();
        }
        public static bool Any<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (source is TSource[] array)
            {
                for (int index = 0; index < array.Length; index++) if (predicate(array[index])) return true;
            }
            else
            {
                foreach (TSource item in source) if (predicate(item)) return true;
            }
            return false;
        }
        public static bool All<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (source is TSource[] array)
            {
                for (int index = 0; index < array.Length; index++) if (!predicate(array[index])) return false;
            }
            else
            {
                foreach (TSource item in source) if (!predicate(item)) return false;
            }
            return true;
        }
        public static int Count<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            int count = 0;
            if (source is TSource[] array)
            {
                for (int index = 0; index < array.Length; index++) if (predicate(array[index])) count++;
            }
            else
            {
                foreach (TSource item in source) if (predicate(item)) count = checked(count + 1);
            }
            return count;
        }
        public static int Count<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source is ICollection<TSource> generic) return generic.Count;
            if (source is Cilica.Runtime.IQueryCardinality query) return query.Count();
            if (source is ICollection legacy) return legacy.Count;
            int count = 0;
            using (IEnumerator<TSource> e = source.GetEnumerator())
                while (e.MoveNext()) count = checked(count + 1);
            return count;
        }
    }
}

namespace Cilica.Runtime
{
    // A managed buffer replaces private pooled/stack scratch storage. It owns
    // ordinary traced arrays and never becomes an enumerable dispatch candidate.
    public sealed class MaterializationBuffer<T>
    {
        private T[] items;
        private int count;
        public MaterializationBuffer() { items = System.Array.Empty<T>(); }
        public void Add(T item)
        {
            int required = checked(count + 1);
            if (count == items.Length)
            {
                int capacity = count == 0 ? 4 : unchecked(count * 2);
                if ((uint)capacity > 0x7fffffc7) capacity = 0x7fffffc7;
                if (capacity < required) capacity = required;
                var replacement = new T[capacity];
                for (int i = 0; i < count; i++) replacement[i] = items[i];
                items = replacement;
            }
            items[count] = item; count = required;
        }
        public T[] ToArray()
        {
            if (count == 0) return System.Array.Empty<T>();
            if (count == items.Length) return items;
            var result = new T[count];
            for (int i = 0; i < count; i++) result[i] = items[i];
            return result;
        }
    }
}
