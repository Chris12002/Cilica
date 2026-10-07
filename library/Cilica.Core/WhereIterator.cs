// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Where/Iterator algorithms; see provenance.
using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Runtime
{
    // Non-generic cardinality avoids introducing an unsupported Func<T,bool>
    // closure when Count/Any is used on an unrelated inline-value sequence.
    public interface IQueryCardinality
    {
        int Count();
        bool Any();
    }

    public interface IQueryMaterialization<T>
    {
        T[] ToArray();
    }

    // One owned class carries the two selected source shapes. Private upstream
    // iterator inheritance/layout is not a compatibility contract.
    // Public metadata permits the existing cross-owner generic visibility rule;
    // this Cilica helper is not a mapped framework iterator contract.
    public sealed class WhereIterator<T> : IEnumerable<T>, IEnumerator<T>, IQueryCardinality, IQueryMaterialization<T>, IQueryTraversal<T>, IQueryFirst<T>, IQueryContains<T>, IQueryContainsTraversal<T>
    {
        private readonly IEnumerable<T> source;
        private readonly T[] array;
        private readonly int length;
        private readonly Func<T, bool> predicate;
        private readonly int threadId;
        private IEnumerator<T> enumerator;
        private int state;
        private T current;
        internal WhereIterator(IEnumerable<T> source, T[] array, Func<T, bool> predicate, int length)
        {
            this.source = source; this.array = array; this.predicate = predicate; this.length = length;
            threadId = Environment.CurrentManagedThreadId;
        }
        internal static IEnumerable<T> FromArray(T[] array, Func<T, bool> predicate, int length)
        {
            if (length == 0) return Array.Empty<T>();
            return new WhereIterator<T>(array, array, predicate, length);
        }
        internal IEnumerable<T> Filter(Func<T, bool> next)
        {
            var combined = new CombinedPredicate<T>(predicate, next);
            return new WhereIterator<T>(source, array, combined.Match, length);
        }
        public IEnumerator<T> GetQueryEnumerator(int terminal, out int count)
        {
            count = -1;
            if (terminal == 3 && array == null && source is List<T> list)
            {
                var captured = list.QueryStorage(out int capturedCount);
                return new WhereIterator<T>(captured, captured, predicate, capturedCount).GetEnumerator();
            }
            return new WhereIterator<T>(source, array, predicate, length).GetEnumerator();
        }
        public IEnumerator<T> GetContainsEnumerator() => GetQueryEnumerator(0, out _);
        public bool Contains(T value)
        { using (IEnumerator<T> e = GetContainsEnumerator()) return QueryContains<T>.Read(e, value); }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new WhereIterator<T>(source, array, predicate, length);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public T Current => current;
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            if (enumerator != null) { enumerator.Dispose(); enumerator = null; }
            current = default(T); state = -1;
        }
        public bool MoveNext()
        {
            if (array != null)
            {
                int index = unchecked(state - 1);
                while ((uint)index < (uint)length)
                {
                    T item = array[index]; index = state; state = unchecked(state + 1);
                    if (predicate(item)) { current = item; return true; }
                }
                Dispose(); return false;
            }
            if (state == 1) { enumerator = source.GetEnumerator(); state = 2; }
            if (state == 2)
            {
                while (enumerator.MoveNext())
                {
                    T item = enumerator.Current;
                    if (predicate(item)) { current = item; return true; }
                }
                Dispose();
            }
            return false;
        }
        public T[] ToArray()
        {
            var builder = new MaterializationBuffer<T>();
            using (var cursor = GetQueryEnumerator(3, out _))
                while (cursor.MoveNext()) builder.Add(cursor.Current);
            return builder.ToArray();
        }
        public int Count()
        {
            int count = 0;
            if (array != null)
            {
                for (int index = 0; index < array.Length; index++) if (predicate(array[index])) count = checked(count + 1);
            }
            else
            {
                foreach (T item in source) if (predicate(item)) count = checked(count + 1);
            }
            return count;
        }
        public T TryFirst(out int found)
        {
            if (array != null)
            {
                for (int index = 0; index < array.Length; index++)
                {
                    T item = array[index]; if (predicate(item)) { found = 1; return item; }
                }
            }
            else
            {
                foreach (T item in source) if (predicate(item)) { found = 1; return item; }
            }
            found = 0; return default(T);
        }
        public bool Any()
        {
            if (array != null)
            {
                for (int index = 0; index < array.Length; index++) if (predicate(array[index])) return true;
            }
            else
            {
                foreach (T item in source) if (predicate(item)) return true;
            }
            return false;
        }
    }
    public sealed class CombinedPredicate<T>
    {
        private readonly Func<T, bool> first;
        private readonly Func<T, bool> second;
        internal CombinedPredicate(Func<T, bool> first, Func<T, bool> second) { this.first = first; this.second = second; }
        internal bool Match(T value) => first(value) && second(value);
    }
}
