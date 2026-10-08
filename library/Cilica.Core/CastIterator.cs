// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Cast.cs and Cast.SpeedOpt.cs; see provenance.
using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Runtime
{
    // Preserve the compiler-generated foreach/yield state machine, including
    // retained Current and automatic fault cleanup. This is not Iterator<T>.
    public sealed class CastIterator<T> : IEnumerable<T>, IEnumerator<T>
    {
        private readonly IEnumerable source;
        private readonly int threadId;
        private IEnumerator enumerator;
        private int state;
        private T current;
        internal CastIterator(IEnumerable source)
        { this.source = source; threadId = Environment.CurrentManagedThreadId; state = -2; }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == -2 && threadId == Environment.CurrentManagedThreadId ? this : new CastIterator<T>(source);
            result.state = 0; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private void SetCurrent(T value) { current = value; }
        public T Current => current;
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            if (state == 1 || state == -3)
            {
                state = -1;
                if (enumerator is IDisposable disposable) disposable.Dispose();
            }
            state = -1;
        }
        public bool MoveNext()
        {
            try
            {
                if (state == 0) { state = -1; enumerator = source.GetEnumerator(); state = -3; }
                else if (state == 1) state = -3;
                else return false;
                if (enumerator.MoveNext()) { SetCurrent((T)enumerator.Current); state = 1; return true; }
                Dispose(); enumerator = null; return false;
            }
            catch (Exception) { Dispose(); throw; }
        }
    }

    public sealed class CastCollectionIterator<T> : IEnumerable<T>, IEnumerator<T>,
        IQueryCardinality, IQueryCheapCount, IQueryMaterialization<T>, IQueryFirst<T>,
        IQueryFirstProjection<T>, IQueryContains<T>, IQueryTraversal<T>, IQuerySkip<T>
    {
        public IEnumerable<T> Skip(int count) => UnsupportedLibraryMember<IEnumerable<T>>.Invoke();

        private readonly ICollection source;
        private readonly int threadId;
        private IEnumerator enumerator;
        private int state;
        private T current;
        internal CastCollectionIterator(ICollection source)
        { this.source = source; threadId = Environment.CurrentManagedThreadId; }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new CastCollectionIterator<T>(source);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private void SetCurrent(T value) { current = value; }
        public T Current => current;
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            if (enumerator is IDisposable disposable) disposable.Dispose();
            enumerator = null; current = default(T); state = -1;
        }
        public bool MoveNext()
        {
            if (state == 1) { enumerator = source.GetEnumerator(); state = 2; }
            if (state == 2)
            {
                if (enumerator.MoveNext()) { SetCurrent((T)enumerator.Current); return true; }
                Dispose();
            }
            return false;
        }
        public int Count() => source.Count;
        public int GetCheapCount() => source.Count;
        public bool Any()
        {
            int count = source.Count;
            if (count >= 0) return count != 0;
            TryFirst(out int found); return found != 0;
        }
        public T[] ToArray()
        {
            var result = new T[source.Count]; int index = 0;
            foreach (T item in source) result[index++] = item;
            return result;
        }
        public List<T> ToList()
        {
            var result = new List<T>(source.Count);
            foreach (T item in source) result.Add(item);
            return result;
        }
        public T TryFirst(out int found)
        {
            foreach (T item in source) { found = 1; return item; }
            found = 0; return default(T);
        }
        public bool Contains(T value)
        {
            foreach (T item in source)
            {
                T candidate = item;
                if (KeyOperations<T>.Equal(ref candidate, value)) return true;
            }
            return false;
        }
        public IEnumerator<T> GetQueryEnumerator(int terminal, out int count)
        {
            count = -1;
            if (terminal == 3 || terminal == 4) count = source.Count;
            // Select skips traversal for an empty presized result, leaving this
            // query available for its first public enumeration.
            return count == 0 || count < -1 ? new CastCollectionIterator<T>(source).GetEnumerator() : GetEnumerator();
        }
    }
}
