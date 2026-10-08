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
    // Terminal 1 may request a cheap materialization count; terminal 2 counts by
    // evaluating selectors and must not add a count-getter callback. Terminal 3
    // materializes to an array; terminal 4 materializes to a list. Both capture
    // List storage and guard preallocated destinations before invoking selectors.
    // Terminal 5 reads a first element; filtered lists capture storage without
    // changing ordinary iteration or materialization-count callbacks.
    public interface IQueryTraversal<T>
    {
        IEnumerator<T> GetQueryEnumerator(int terminal, out int count);
    }
    public interface IQueryCheapCount { int GetCheapCount(); }

    public sealed class SelectIterator<TSource, TResult> : IEnumerable<TResult>, IEnumerator<TResult>,
        IQueryTraversal<TResult>, IQueryFirst<TResult>, IQueryCheapCount, IQueryCardinality, IQueryMaterialization<TResult>, IQueryContains<TResult>, IQueryContainsTraversal<TResult>
    {
        private readonly IEnumerable<TSource> source;
        private readonly TSource[] array;
        private readonly Func<TSource, TResult> selector;
        private readonly int threadId;
        private IEnumerator<TSource> enumerator;
        private int state;
        private int materializationLimit = -1;
        private int materialized;
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
        object IEnumerator.Current => current;
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
                if (enumerator.MoveNext())
                {
                    TSource item = enumerator.Current;
                    if (materializationLimit >= 0)
                    {
                        if (materialized >= materializationLimit) throw new IndexOutOfRangeException();
                        materialized++;
                    }
                    SetCurrent(selector(item)); return true;
                }
                Dispose();
            }
            return false;
        }
        public IEnumerator<TResult> GetContainsEnumerator()
        {
            if (array != null) return GetQueryEnumerator(0, out _);
            IEnumerator<TSource> traversal = null;
            if (source is IQueryContainsTraversal<TSource> fused) traversal = fused.GetContainsEnumerator();
            else if (!(source is IQueryTraversal<TSource>) && source is IList<TSource> list)
                traversal = new IndexedQueryEnumerator<TSource>(list, list.Count);
            if (traversal == null) return null;
            var result = new SelectIterator<TSource, TResult>(source, null, selector);
            result.enumerator = traversal; result.state = 2; return result;
        }
        public bool Contains(TResult value)
        {
            IEnumerator<TResult> traversal = GetContainsEnumerator();
            if (traversal == null) return QueryContains<TResult>.Iterate(this, value);
            using (traversal) return QueryContains<TResult>.Read(traversal, value);
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
                    result.enumerator = query.GetQueryEnumerator(terminal, out count); result.state = 2;
                }
                else if ((terminal == 3 || terminal == 4) && source is List<TSource> capturedList)
                {
                    var captured = capturedList.QueryStorage(out count);
                    result.enumerator = new PrefixQueryEnumerator<TSource>(captured, count); result.state = 2;
                }
                else if (source is IList<TSource> list)
                {
                    count = list.Count;
                    if (count < 0 && terminal == 3) throw new OverflowException();
                    if (count < 0 && terminal == 4) throw new ArgumentOutOfRangeException("capacity", "Non-negative number required.");
                    result.enumerator = new IndexedQueryEnumerator<TSource>(list, count); result.state = 2;
                }
            }
            if (terminal == 3 || terminal == 4) result.materializationLimit = count;
            return result;
        }
        public TResult TryFirst(out int found)
        {
            if (source is IQueryFirstProjection<TSource> query)
            {
                TSource item = query.TryFirst(out found); return found != 0 ? selector(item) : default(TResult);
            }
            using (var e = GetQueryEnumerator(5, out _))
            {
                if (e.MoveNext()) { found = 1; return e.Current; }
            }
            found = 0; return default(TResult);
        }
        public bool Any()
        {
            int count = GetCheapCount();
            if (count >= 0) return count != 0;
            if (source is IQueryFirstProjection<TSource>) { TryFirst(out int found); return found != 0; }
            using (var e = GetQueryEnumerator(1, out _)) return e.MoveNext();
        }
        public int Count()
        {
            int count = 0;
            using (var e = GetQueryEnumerator(2, out _)) while (e.MoveNext()) count = checked(count + 1);
            return count;
        }
        public TResult[] ToArray() => Materialize(false);
        public List<TResult> ToList() => List<TResult>.CopyEnumerable(Materialize(true));
        private TResult[] Materialize(bool list)
        {
            using (var e = GetQueryEnumerator(list ? 4 : 3, out int count))
            {
                if (count == 0) return Array.Empty<TResult>();
                if (count != -1)
                {
                    if (list && count < 0) throw new ArgumentOutOfRangeException("capacity", "Non-negative number required.");
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
    // A private materialization view holds the old array across callback growth.
    public sealed class PrefixQueryEnumerator<T> : IEnumerator<T>
    {
        private readonly T[] array;
        private readonly int count;
        private int index;
        private T current;
        internal PrefixQueryEnumerator(T[] array, int count) { this.array = array; this.count = count; }
        public bool MoveNext()
        {
            if (index < count) { SetCurrent(array[index++]); return true; }
            return false;
        }
        private void SetCurrent(T value) { current = value; }
        public T Current => current;
        object IEnumerator.Current => current;
        public void Dispose() { current = default(T); }
        public void Reset() { throw new NotSupportedException(); }
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
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose() { current = default(T); }
    }
    // The indexed overload uses the pinned yield lifecycle, with no terminal
    // shortcuts: selectors run in source order and faults unwind the cursor.
    public sealed class IndexedSelectIterator<TSource, TResult> : IEnumerable<TResult>, IEnumerator<TResult>
    {
        private readonly IEnumerable<TSource> source;
        private readonly Func<TSource, int, TResult> selector;
        private readonly int threadId;
        private IEnumerator<TSource> enumerator;
        private int state;
        private int index;
        private TResult current;
        internal IndexedSelectIterator(IEnumerable<TSource> source, Func<TSource, int, TResult> selector)
        {
            this.source = source; this.selector = selector;
            threadId = Environment.CurrentManagedThreadId; state = -2;
        }
        internal static IEnumerable<TResult> FromArray(TSource[] source, Func<TSource, int, TResult> selector, int length)
        {
            if (length == 0) return Array.Empty<TResult>();
            return new IndexedSelectIterator<TSource, TResult>(source, selector);
        }
        public IEnumerator<TResult> GetEnumerator()
        {
            var result = state == -2 && threadId == Environment.CurrentManagedThreadId
                ? this : new IndexedSelectIterator<TSource, TResult>(source, selector);
            result.state = 0; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public TResult Current => current;
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        private void Finish()
        {
            // Terminate before user disposal so a throwing Dispose is not retried.
            state = -1;
            if (enumerator != null) enumerator.Dispose();
        }
        public void Dispose()
        {
            if (state == -3 || state == 1) Finish();
            enumerator = null; state = -2;
        }
        public bool MoveNext()
        {
            bool completed = false;
            try
            {
                bool result = Step(); completed = true; return result;
            }
            // Match the generated fault handler: cleanup happens during unwind,
            // after outer filters, and a disposal fault replaces the original.
            finally { if (!completed) Dispose(); }
        }
        private bool Step()
        {
            if (state == 0)
            {
                state = -1; index = -1;
                enumerator = source.GetEnumerator(); state = -3;
            }
            else if (state == 1) state = -3;
            else return false;
            if (enumerator.MoveNext())
            {
                TSource item = enumerator.Current;
                index = checked(index + 1);
                current = selector(item, index); state = 1; return true;
            }
            Finish(); enumerator = null; return false;
        }
    }

}
