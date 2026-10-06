// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned OrderBy/OrderedEnumerable; see provenance.
using System;
using System.Collections;
using System.Collections.Generic;
namespace System.Linq
{
    public interface IOrderedEnumerable<out TElement> : IEnumerable<TElement>
    {
        IOrderedEnumerable<TElement> CreateOrderedEnumerable<TKey>(Func<TElement, TKey> keySelector, IComparer<TKey> comparer, bool descending);
    }
    public static partial class Enumerable
    {
        public static IOrderedEnumerable<T> OrderBy<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector) => new Cilica.Runtime.OrderedIterator<T, TKey>(source, keySelector, null, false, null);
        public static IOrderedEnumerable<T> OrderBy<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector, IComparer<TKey> comparer) => new Cilica.Runtime.OrderedIterator<T, TKey>(source, keySelector, comparer, false, null);
        public static IOrderedEnumerable<T> OrderByDescending<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector) => new Cilica.Runtime.OrderedIterator<T, TKey>(source, keySelector, null, true, null);
        public static IOrderedEnumerable<T> OrderByDescending<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector, IComparer<TKey> comparer) => new Cilica.Runtime.OrderedIterator<T, TKey>(source, keySelector, comparer, true, null);
        public static IOrderedEnumerable<T> ThenBy<T, TKey>(IOrderedEnumerable<T> source, Func<T, TKey> keySelector)
        { if (source == null) throw new ArgumentNullException("source"); return source.CreateOrderedEnumerable(keySelector, null, false); }
        public static IOrderedEnumerable<T> ThenBy<T, TKey>(IOrderedEnumerable<T> source, Func<T, TKey> keySelector, IComparer<TKey> comparer)
        { if (source == null) throw new ArgumentNullException("source"); return source.CreateOrderedEnumerable(keySelector, comparer, false); }
        public static IOrderedEnumerable<T> ThenByDescending<T, TKey>(IOrderedEnumerable<T> source, Func<T, TKey> keySelector)
        { if (source == null) throw new ArgumentNullException("source"); return source.CreateOrderedEnumerable(keySelector, null, true); }
        public static IOrderedEnumerable<T> ThenByDescending<T, TKey>(IOrderedEnumerable<T> source, Func<T, TKey> keySelector, IComparer<TKey> comparer)
        { if (source == null) throw new ArgumentNullException("source"); return source.CreateOrderedEnumerable(keySelector, comparer, true); }
    }
}
namespace Cilica.Runtime
{
    // Exact owned interfaces preserve inline element signatures without widening
    // the compiler's abstract-class call profile. Public metadata is not a mapped API.
    public interface IOrdering<T>
    {
        IOrderSorter<T> GetSorter(IOrderSorter<T> next);
        IOrderCache<T> GetCache(IOrderCache<T> child);
    }
    public interface IOrderSorter<T> : IMapComparison { void ComputeKeys(T[] elements); }
    public interface IOrderCache<T> { void SetElement(T item); int Compare(T item); }
    public sealed class OrderedIterator<T, TKey> : System.Linq.IOrderedEnumerable<T>, IOrdering<T>, IEnumerator<T>,
        IQueryMaterialization<T>, IQueryCardinality, IQueryCheapCount, IQueryTraversal<T>, IQueryFirst<T>, IQueryFirstProjection<T>
    {
        private readonly IEnumerable<T> source;
        private readonly Func<T, TKey> selector;
        private readonly IComparer<TKey> comparer;
        private readonly bool descending;
        private readonly IOrdering<T> parent;
        private readonly int threadId;
        private T[] buffer;
        private int[] map;
        private T current;
        private int state;
        internal OrderedIterator(IEnumerable<T> source, Func<T, TKey> keySelector, IComparer<TKey> comparer, bool descending, IOrdering<T> parent)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");
            this.source = source; selector = keySelector; this.comparer = comparer ?? Comparer<TKey>.Default;
            this.descending = descending; this.parent = parent; threadId = Environment.CurrentManagedThreadId;
        }
        public System.Linq.IOrderedEnumerable<T> CreateOrderedEnumerable<TNext>(Func<T, TNext> keySelector, IComparer<TNext> comparer, bool descending) =>
            new OrderedIterator<T, TNext>(source, keySelector, comparer, descending, this);
        public IOrderSorter<T> GetSorter(IOrderSorter<T> next)
        {
            IOrderSorter<T> result = new OrderSorter<T, TKey>(selector, comparer, descending, next);
            return parent == null ? result : parent.GetSorter(result);
        }
        public IOrderCache<T> GetCache(IOrderCache<T> child)
        {
            IOrderCache<T> result = new OrderCache<T, TKey>(selector, comparer, descending, child);
            return parent == null ? result : parent.GetCache(result);
        }
        private int[] SortedMap(T[] items)
        {
            IOrderSorter<T> sorter = GetSorter(null); sorter.ComputeKeys(items);
            int[] result = new int[items.Length];
            for (int i = 0; i < result.Length; i++) result[i] = i;
            MapSort.Sort(result, sorter); return result;
        }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new OrderedIterator<T, TKey>(source, selector, comparer, descending, parent);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public T Current => current;
        object IEnumerator.Current => current;
        private void SetCurrent(T value) { current = value; }
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose() { buffer = null; map = null; current = default; state = -1; }
        private static bool HasElements(int length) => length != 0;
        public bool MoveNext()
        {
            if (state == 1)
            {
                T[] items = System.Linq.Enumerable.ToArray(source);
                if (HasElements(items.Length)) { map = SortedMap(items); buffer = items; state = 2; }
            }
            if (state > 1)
            {
                int index = state - 2;
                if ((uint)index < (uint)map.Length) { SetCurrent(buffer[map[index]]); state++; return true; }
            }
            Dispose(); return false;
        }
        public T[] ToArray()
        {
            T[] items = System.Linq.Enumerable.ToArray(source);
            if (items.Length <= 1) return items;
            T[] result = new T[items.Length]; int[] order = SortedMap(items);
            for (int i = 0; i < result.Length; i++) result[i] = items[order[i]];
            return result;
        }
        public int GetCheapCount()
        {
            if (source is IQueryCheapCount query) return query.GetCheapCount();
            if (source is ICollection<T> collection) return collection.Count;
            if (source is ICollection legacy) return legacy.Count;
            return -1;
        }
        public int Count() => System.Linq.Enumerable.Count(source);
        public bool Any() { int count = GetCheapCount(); if (count >= 0) return count != 0; TryFirst(out int found); return found != 0; }
        public IEnumerator<T> GetQueryEnumerator(int terminal, out int count)
        { count = -1; if (terminal == 1) count = GetCheapCount(); if (count == 0) return null; return GetEnumerator(); }
        public T TryFirst(out int found)
        {
            if (parent == null)
            {
                using (IEnumerator<T> e = source.GetEnumerator())
                {
                    if (e.MoveNext())
                    {
                        T result = e.Current; TKey key = selector(result);
                        while (e.MoveNext())
                        {
                            T item = e.Current; TKey next = selector(item); int c = comparer.Compare(next, key);
                            if (descending ? c > 0 : c < 0) { key = next; result = item; }
                        }
                        found = 1; return result;
                    }
                }
            }
            else
            {
                IOrderCache<T> cache = GetCache(null);
                using (IEnumerator<T> e = source.GetEnumerator())
                {
                    if (e.MoveNext())
                    {
                        T result = e.Current; cache.SetElement(result);
                        while (e.MoveNext()) { T item = e.Current; if (cache.Compare(item) < 0) result = item; }
                        found = 1; return result;
                    }
                }
            }
            found = 0; return default;
        }
    }
    public sealed class OrderSorter<T, TKey> : IOrderSorter<T>
    {
        private readonly Func<T, TKey> selector;
        private readonly IComparer<TKey> comparer;
        private readonly bool descending;
        private readonly IOrderSorter<T> next;
        private TKey[] keys;
        internal OrderSorter(Func<T, TKey> selector, IComparer<TKey> comparer, bool descending, IOrderSorter<T> next)
        { this.selector = selector; this.comparer = comparer; this.descending = descending; this.next = next; }
        public void ComputeKeys(T[] elements)
        {
            TKey[] result = new TKey[elements.Length];
            for (int i = 0; i < result.Length; i++) result[i] = selector(elements[i]);
            keys = result; if (next != null) next.ComputeKeys(elements);
        }
        public int Compare(int first, int second)
        {
            int c = comparer.Compare(keys[first], keys[second]);
            if (c == 0) return next == null ? first - second : next.Compare(first, second);
            return descending != (c > 0) ? 1 : -1;
        }
    }
    public sealed class OrderCache<T, TKey> : IOrderCache<T>
    {
        private readonly Func<T, TKey> selector;
        private readonly IComparer<TKey> comparer;
        private readonly bool descending;
        private readonly IOrderCache<T> child;
        private TKey lastKey;
        internal OrderCache(Func<T, TKey> selector, IComparer<TKey> comparer, bool descending, IOrderCache<T> child)
        { this.selector = selector; this.comparer = comparer; this.descending = descending; this.child = child; }
        private void SetKey(TKey key) { lastKey = key; }
        public void SetElement(T item) { TKey key = selector(item); SetKey(key); if (child != null) child.SetElement(item); }
        public int Compare(T item)
        {
            TKey key = selector(item); int c = descending ? comparer.Compare(lastKey, key) : comparer.Compare(key, lastKey);
            if (child != null && c == 0) return child.Compare(item);
            if (c < 0) { SetKey(key); if (child != null) child.SetElement(item); }
            return c;
        }
    }
}
