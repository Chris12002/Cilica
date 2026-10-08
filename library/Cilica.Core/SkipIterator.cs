// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from pinned Skip.cs and SkipTake.SpeedOpt.cs; see provenance.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace System.Linq
{
    public static partial class Enumerable
    {
        public static IEnumerable<T> Skip<T>(IEnumerable<T> source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source is T[] array && NoElements(array.Length)) return Array.Empty<T>();
            if (count <= 0)
            {
                if (source is Cilica.Runtime.IQuerySkip<T>) return source;
                count = 0;
            }
            else if (source is Cilica.Runtime.IQuerySkip<T> query) return query.Skip(count);
            if (source is IList<T> list) return new Cilica.Runtime.ListSkipIterator<T>(list, count);
            return new Cilica.Runtime.EnumerableSkipIterator<T>(source, count);
        }
    }
}
namespace Cilica.Runtime
{
    // Optimized framework iterator sources must explicitly supply their contract.
    // A reached unimplemented specialization remains a build error.
    public interface IQuerySkip<T> { IEnumerable<T> Skip(int count); }

    public sealed class EnumerableSkipIterator<T> : IEnumerable<T>, IEnumerator<T>, IQuerySkip<T>,
        IQueryCardinality, IQueryCheapCount, IQueryMaterialization<T>, IQueryFirst<T>
    {
        private readonly IEnumerable<T> source;
        private readonly int minimum;
        private readonly int threadId;
        private IEnumerator<T> enumerator;
        private int state;
        private T current;
        internal EnumerableSkipIterator(IEnumerable<T> source, int minimum)
        { this.source = source; this.minimum = minimum; threadId = Environment.CurrentManagedThreadId; }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new EnumerableSkipIterator<T>(source, minimum);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private void SetCurrent(T value) { current = value; }
        public T Current => current;
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            if (enumerator != null) { enumerator.Dispose(); enumerator = null; }
            current = default(T); state = -1;
        }
        private bool SkipBefore(IEnumerator<T> cursor)
        {
            int remaining = minimum;
            while (remaining > 0 && cursor.MoveNext()) --remaining;
            return remaining == 0;
        }
        public bool MoveNext()
        {
            if (state < 1) { Dispose(); return false; }
            if (state == 1) { enumerator = source.GetEnumerator(); state = 2; }
            if (state == 2)
            {
                if (!SkipBefore(enumerator)) { Dispose(); return false; }
                state = 3;
            }
            if (enumerator.MoveNext()) { SetCurrent(enumerator.Current); return true; }
            Dispose(); return false;
        }
        public IEnumerable<T> Skip(int count)
        {
            int next = unchecked(minimum + count);
            return next < 0 ? new EnumerableSkipIterator<T>(this, count) : new EnumerableSkipIterator<T>(source, next);
        }
        public int Count() => Math.Max(Enumerable.Count(source) - minimum, 0);
        public int GetCheapCount() => -1;
        public T TryFirst(out int found)
        {
            using (var cursor = source.GetEnumerator())
                if (SkipBefore(cursor) && cursor.MoveNext()) { found = 1; return cursor.Current; }
            found = 0; return default(T);
        }
        public bool Any() { TryFirst(out int found); return found != 0; }
        public T[] ToArray()
        {
            var builder = new MaterializationBuffer<T>();
            using (var cursor = source.GetEnumerator())
                if (SkipBefore(cursor)) while (cursor.MoveNext()) builder.Add(cursor.Current);
            return builder.ToArray();
        }
        public List<T> ToList() => List<T>.CopyEnumerable(ToArray());
    }

    public sealed class ListSkipIterator<T> : IList<T>, IReadOnlyList<T>, IEnumerator<T>, IQuerySkip<T>,
        IQueryCheapCount, IQueryMaterialization<T>, IQueryFirst<T>
    {
        private readonly IList<T> source;
        private readonly int minimum;
        private readonly int threadId;
        private int state;
        private T current;
        internal ListSkipIterator(IList<T> source, int minimum)
        { this.source = source; this.minimum = minimum; threadId = Environment.CurrentManagedThreadId; }
        public IEnumerator<T> GetEnumerator()
        {
            var result = state == 0 && threadId == Environment.CurrentManagedThreadId ? this : new ListSkipIterator<T>(source, minimum);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private void SetCurrent(T value) { current = value; }
        public T Current => current;
        object IEnumerator.Current => current;
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose() { current = default(T); state = -1; }
        public bool MoveNext()
        {
            int index = unchecked(state - 1);
            if ((uint)index <= (uint)(0x7fffffff - minimum) && index < source.Count - minimum)
            { SetCurrent(source[minimum + index]); state = unchecked(state + 1); return true; }
            Dispose(); return false;
        }
        public IEnumerable<T> Skip(int count)
        {
            int next = unchecked(minimum + count);
            if (next < 0) return Array.Empty<T>();
            return new ListSkipIterator<T>(source, next);
        }
        public int Count { get { int count = source.Count; return count <= minimum ? 0 : count - minimum; } }
        public int GetCheapCount() => Count;
        public T TryFirst(out int found)
        {
            if (source.Count > minimum) { found = 1; return source[minimum]; }
            found = 0; return default(T);
        }
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException("index");
                return source[minimum + index];
            }
            set { throw new NotSupportedException(); }
        }
        public bool IsReadOnly => true;
        public void Add(T value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public void Insert(int index, T value) { throw new NotSupportedException(); }
        public bool Remove(T value) { throw new NotSupportedException(); }
        public void RemoveAt(int index) { throw new NotSupportedException(); }
        private T[] Storage(out int length)
        {
            if (source is T[] values && values.GetType() == typeof(T[]))
            { length = values.Length; return values; }
            if (source is List<T> list && list.GetType() == typeof(List<T>))
                return list.QueryStorage(out length);
            length = 0; return null;
        }
        private void Fill(T[] destination, int destinationIndex, int count)
        {
            T[] values = Storage(out int length);
            if (values != null)
            {
                // Span.Slice validates the source start even for an empty copy.
                if ((uint)minimum > (uint)length || (uint)count > (uint)(length - minimum))
                    throw new ArgumentOutOfRangeException();
                for (int i = 0; i < count; ++i) destination[destinationIndex + i] = values[minimum + i];
            }
            else for (int i = 0; i < count; ++i) destination[destinationIndex + i] = source[minimum + i];
        }
        public T[] ToArray()
        {
            int count = Count; if (count == 0) return Array.Empty<T>();
            var result = new T[count]; Fill(result, 0, count); return result;
        }
        public List<T> ToList()
        {
            int count = Count; var result = new List<T>(count == 0 ? 0 : Math.Max(count, 4));
            for (int i = 0; i < count; ++i) result.Add(source[minimum + i]);
            return result;
        }
        public void CopyTo(T[] destination, int arrayIndex)
        {
            int count = Count;
            // Match AsSpan(array, start, length), including null/empty allowance.
            if (destination == null)
            {
                if (arrayIndex != 0 || count != 0) throw new ArgumentOutOfRangeException();
                Fill(destination, 0, 0);
                return;
            }
            if (destination.GetType() != Array.Empty<T>().GetType()) throw new ArrayTypeMismatchException();
            if ((uint)arrayIndex > (uint)destination.Length || (uint)count > (uint)(destination.Length - arrayIndex))
                throw new ArgumentOutOfRangeException();
            Fill(destination, arrayIndex, count);
        }
        public int IndexOf(T value)
        {
            // The unconstrained span overload uses EqualityComparer<T>.Equals
            // on copied elements, unlike Array.IndexOf's borrowed value path.
            T[] values = Storage(out int length);
            if (values != null)
            {
                for (int i = minimum; i < length; ++i)
                {
                    T item = values[i];
                    if (KeyOperations<T>.Equal(ref item, value)) return i - minimum;
                }
            }
            else
            {
                int end = minimum + Count;
                for (int i = minimum; i < end; ++i)
                {
                    T item = source[i]; if (KeyOperations<T>.Equal(ref item, value)) return i - minimum;
                }
            }
            return -1;
        }
        public bool Contains(T value) => IndexOf(value) >= 0;
    }
}
