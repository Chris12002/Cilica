// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Distinct/First algorithms; see provenance.
using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Runtime
{
    public interface IQueryFirst<T> { T TryFirst(out int found); }
    public static class QueryFirst<T>
    {
        internal static T Read(IEnumerable<T> source, out int found)
        {
            if (source is IQueryFirst<T> query) return query.TryFirst(out found);
            if (source is IList<T> list)
            {
                if (list.Count > 0) { found = 1; return list[0]; }
            }
            else
            {
                using (var e = source.GetEnumerator())
                    if (e.MoveNext()) { found = 1; return e.Current; }
            }
            found = 0; return default(T);
        }
    }
    public sealed class DistinctIterator<T> : IEnumerable<T>, IEnumerator<T>, IQueryFirst<T>, IQueryCardinality, IQueryMaterialization<T>
    {
        private readonly IEnumerable<T> source;
        private readonly int threadId;
        private SetStorage<T> set;
        private IEnumerator<T> enumerator;
        private int state;
        private T current;
        internal DistinctIterator(IEnumerable<T> source) { this.source = source; threadId = Environment.CurrentManagedThreadId; }
        internal static IEnumerable<T> FromArray(T[] source, int length)
        {
            if (length == 0) return Array.Empty<T>();
            return new DistinctIterator<T>(source);
        }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new DistinctIterator<T>(source);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public T Current => current;
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            if (enumerator != null) { enumerator.Dispose(); enumerator = null; set = null; }
            current = default(T); state = -1;
        }
        public bool MoveNext()
        {
            if (state == 1)
            {
                enumerator = source.GetEnumerator();
                if (!enumerator.MoveNext()) { Dispose(); return false; }
                T item = enumerator.Current;
                set = SetStorage<T>.ForQuery(7); set.Add(item); current = item; state = 2; return true;
            }
            if (state == 2)
            {
                while (enumerator.MoveNext())
                {
                    T item = enumerator.Current;
                    if (set.Add(item)) { current = item; return true; }
                }
            }
            Dispose(); return false;
        }
        // Iterator.TryGetFirst intentionally bypasses all set/hash work.
        public T TryFirst(out int found) => QueryFirst<T>.Read(source, out found);
        public bool Any() { TryFirst(out int found); return found != 0; }
        public int Count() => BuildSet().Count;
        public T[] ToArray() => BuildSet().QueryArray();
        private SetStorage<T> BuildSet()
        {
            int capacity = 0;
            if (source is ICollection<T> collection) capacity = collection.Count;
            var result = SetStorage<T>.ForQuery(capacity);
            foreach (T item in source) result.Add(item);
            return result;
        }
    }
}
