// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Select/Iterator algorithms; see provenance.
using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Runtime
{
    // Fresh traversal preserves the cursor of a preceding Where/Select. Terminal
    // traversal additionally preserves indexed IList behavior through Select chains.
    // These owned helpers avoid generic virtual methods and are not framework APIs.
    public interface IQueryTraversal<T>
    {
        IEnumerator<T> GetQueryEnumerator(int terminal, out int count);
    }
    public interface IQueryCheapCount { int GetCheapCount(); }

    public sealed class SelectIterator<TSource, TResult> : IEnumerable<TResult>, IEnumerator<TResult>,
        IQueryTraversal<TResult>, IQueryFirst<TResult>, IQueryCheapCount, IQueryCardinality, IQueryMaterialization<TResult>
    {
        private readonly IEnumerable<TSource> source;
        private readonly TSource[] array;
        private readonly Func<TSource, TResult> selector;
        private readonly int threadId;
        private IEnumerator<TSource> enumerator;
        private int state;
        private TResult current;
        internal SelectIterator(IEnumerable<TSource> source, TSource[] array, Func<TSource, TResult> selector)
        {
            this.source = source; this.array = array; this.selector = selector;
            threadId = Environment.CurrentManagedThreadId;
        }
        internal static IEnumerable<TResult> FromArray(TSource[] array, Func<TSource, TResult> selector, int length)
        {
            if (length == 0) return Array.Empty<TResult>();
            return new SelectIterator<TSource, TResult>(array, array, selector);
        }
        public IEnumerator<TResult> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new SelectIterator<TSource, TResult>(source, array, selector);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private void SetCurrent(TResult value) { current = value; }
        public TResult Current => current;
        object IEnumerator.Current => UnsupportedLibraryMember<object>.Invoke();
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            if (enumerator != null) { enumerator.Dispose(); enumerator = null; }
            current = default(TResult); state = -1;
        }
        public bool MoveNext()
        {
            if (array != null)
            {
                int index = unchecked(state - 1);
                if ((uint)index < (uint)array.Length)
                {
                    state = unchecked(state + 1); SetCurrent(selector(array[index])); return true;
                }
                Dispose(); return false;
            }
            if (state == 1)
            {
                enumerator = source is IQueryTraversal<TSource> query ? query.GetQueryEnumerator(0, out _) : source.GetEnumerator();
                state = 2;
            }
            if (state == 2)
            {
                if (enumerator.MoveNext()) { SetCurrent(selector(enumerator.Current)); return true; }
                Dispose();
            }
            return false;
        }
        public int GetCheapCount()
        {
            if (array != null) return array.Length;
            if (source is IQueryCheapCount query) return query.GetCheapCount();
            if (source is IList<TSource> list) return list.Count;
            return -1;
        }
        public IEnumerator<TResult> GetQueryEnumerator(int terminal, out int count)
        {
            var result = new SelectIterator<TSource, TResult>(source, array, selector);
            result.state = 1; count = -1;
            if (terminal != 0)
            {
                if (array != null) count = array.Length;
                else if (source is IQueryTraversal<TSource> query)
                {
                    result.enumerator = query.GetQueryEnumerator(1, out count); result.state = 2;
                }
                else if (source is IList<TSource> list)
                {
                    count = list.Count;
                    result.enumerator = new IndexedQueryEnumerator<TSource>(list, count); result.state = 2;
                }
            }
            return result;
        }
        public TResult TryFirst(out int found)
        {
            using (var e = GetQueryEnumerator(1, out _))
            {
                if (e.MoveNext()) { found = 1; return e.Current; }
            }
            found = 0; return default(TResult);
        }
        public bool Any()
        {
            int count = GetCheapCount();
            if (count >= 0) return count != 0;
            using (var e = GetQueryEnumerator(1, out _)) return e.MoveNext();
        }
        public int Count()
        {
            int count = 0;
            using (var e = GetQueryEnumerator(1, out _)) while (e.MoveNext()) count = checked(count + 1);
            return count;
        }
        public TResult[] ToArray()
        {
            using (var e = GetQueryEnumerator(1, out int count))
            {
                if (count == 0) return Array.Empty<TResult>();
                if (count > 0)
                {
                    var result = new TResult[count]; int index = 0;
                    while (e.MoveNext()) result[index++] = e.Current;
                    return result;
                }
                var builder = new MaterializationBuffer<TResult>();
                while (e.MoveNext()) builder.Add(e.Current);
                return builder.ToArray();
            }
        }
    }
    public sealed class IndexedQueryEnumerator<T> : IEnumerator<T>
    {
        private readonly IList<T> source;
        private readonly int count;
        private int index;
        private T current;
        internal IndexedQueryEnumerator(IList<T> source, int count) { this.source = source; this.count = count; }
        public bool MoveNext()
        {
            if (index < count) { SetCurrent(source[index++]); return true; }
            return false;
        }
        private void SetCurrent(T value) { current = value; }
        public T Current => current;
        object IEnumerator.Current => UnsupportedLibraryMember<object>.Invoke();
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose() { current = default(T); }
    }
}
