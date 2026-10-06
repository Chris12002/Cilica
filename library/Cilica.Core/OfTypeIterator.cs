// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned OfType and terminal algorithms; see provenance.
using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Runtime
{
    public sealed class OfTypeIterator<T> : IEnumerable<T>, IEnumerator<T>, IQueryCardinality,
        IQueryMaterialization<T>, IQueryFirst<T>, IQueryTraversal<T>
    {
        private readonly IEnumerable source;
        private readonly int threadId;
        private IEnumerator enumerator;
        private int state;
        private T current;
        internal OfTypeIterator(IEnumerable source) { this.source = source; threadId = Environment.CurrentManagedThreadId; }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new OfTypeIterator<T>(source);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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
                while (enumerator.MoveNext())
                    if (enumerator.Current is T item) { current = item; return true; }
                Dispose();
            }
            return false;
        }
        public int Count()
        {
            int count = 0;
            foreach (object item in source) if (item is T) count = checked(count + 1);
            return count;
        }
        public T[] ToArray()
        {
            var builder = new MaterializationBuffer<T>();
            foreach (object item in source) if (item is T value) builder.Add(value);
            return builder.ToArray();
        }
        public T TryFirst(out int found)
        {
            foreach (object item in source) if (item is T value) { found = 1; return value; }
            found = 0; return default(T);
        }
        public bool Any() { TryFirst(out int found); return found != 0; }
        public IEnumerator<T> GetQueryEnumerator(int terminal, out int count)
        {
            count = -1;
            // Upstream fuses reference-result OfType.Select over generic object
            // sources, reading generic Current and leaving this query untouched.
            // Value/nullable results and purely nongeneric sources use this cursor.
            if (!KeyOperations<T>.IsValueType() && source is IEnumerable<object> objects)
                return new ObjectTypeQueryEnumerator<T>(objects);
            return GetEnumerator();
        }
    }
    public sealed class ObjectTypeQueryEnumerator<T> : IEnumerator<T>
    {
        private readonly IEnumerable<object> source;
        private IEnumerator<object> enumerator;
        private int state;
        private T current;
        internal ObjectTypeQueryEnumerator(IEnumerable<object> source) { this.source = source; }
        public T Current => current;
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        public bool MoveNext()
        {
            if (state == 0) { enumerator = source.GetEnumerator(); state = 1; }
            if (state == 1)
            {
                while (enumerator.MoveNext())
                    if (enumerator.Current is T item) { current = item; return true; }
                Dispose();
            }
            return false;
        }
        public void Dispose()
        {
            if (enumerator != null) { enumerator.Dispose(); enumerator = null; }
            current = default(T); state = -1;
        }
    }
}
