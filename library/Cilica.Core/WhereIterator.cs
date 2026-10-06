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

    // One owned class carries the two selected source shapes. Private upstream
    // iterator inheritance/layout is not a compatibility contract.
    // Public metadata permits the existing cross-owner generic visibility rule;
    // this Cilica helper is not a mapped framework iterator contract.
    public sealed class WhereIterator<T> : IEnumerable<T>, IEnumerator<T>, IQueryCardinality
    {
        private readonly IEnumerable<T> source;
        private readonly T[] array;
        private readonly Func<T, bool> predicate;
        private readonly int threadId;
        private IEnumerator<T> enumerator;
        private int state;
        private T current;
        internal WhereIterator(IEnumerable<T> source, T[] array, Func<T, bool> predicate)
        {
            this.source = source; this.array = array; this.predicate = predicate;
            threadId = Environment.CurrentManagedThreadId;
        }
        internal static IEnumerable<T> FromArray(T[] array, Func<T, bool> predicate, int length)
        {
            if (length == 0) return Array.Empty<T>();
            return new WhereIterator<T>(array, array, predicate);
        }
        internal IEnumerable<T> Filter(Func<T, bool> next)
        {
            var combined = new CombinedPredicate<T>(predicate, next);
            return new WhereIterator<T>(source, array, combined.Match);
        }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new WhereIterator<T>(source, array, predicate);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public T Current => current;
        object IEnumerator.Current => UnsupportedLibraryMember<object>.Invoke();
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
                while ((uint)index < (uint)array.Length)
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
