// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Concat/Concat.SpeedOpt; see provenance.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Runtime
{
    // IteratorSelect obtains this first result before invoking its selector.
    public interface IQueryFirstProjection<T> { T TryFirst(out int found); }
    public sealed class ConcatIterator<T> : IEnumerable<T>, IEnumerator<T>, IQueryFirst<T>, IQueryFirstProjection<T>,
        IQueryCardinality, IQueryCheapCount, IQueryMaterialization<T>, IQueryTraversal<T>, IQueryContains<T>
    {
        private readonly IEnumerable<T> first;
        private readonly IEnumerable<T> second;
        private readonly ConcatIterator<T> tail;
        private readonly IEnumerable<T> head;
        private readonly int headIndex;
        private readonly bool onlyCollections;
        private readonly int threadId;
        private IEnumerator<T> enumerator;
        private int state;
        private T current;
        internal ConcatIterator(IEnumerable<T> first, IEnumerable<T> second)
        { this.first = first; this.second = second; headIndex = 1; threadId = Environment.CurrentManagedThreadId; }
        private ConcatIterator(ConcatIterator<T> tail, IEnumerable<T> head, int index, bool collections)
        { this.tail = tail; this.head = head; headIndex = index; onlyCollections = collections; threadId = Environment.CurrentManagedThreadId; }
        internal ConcatIterator<T> Append(IEnumerable<T> next)
        {
            if (headIndex == 0x7ffffffd) return new ConcatIterator<T>(this, next);
            bool collections = tail == null ? next is ICollection<T> && first is ICollection<T> && second is ICollection<T>
                : onlyCollections && next is ICollection<T>;
            return new ConcatIterator<T>(this, next, headIndex + 1, collections);
        }
        private IEnumerable<T> Source(int index)
        {
            if (index > headIndex) return null;
            var node = this;
            while (node.tail != null) { if (index == node.headIndex) return node.head; node = node.tail; }
            return index == 0 ? node.first : index == 1 ? node.second : null;
        }
        public IEnumerator<T> GetEnumerator()
        {
            var result = this;
            if (state != 0 || threadId != Environment.CurrentManagedThreadId)
                result = tail == null ? new ConcatIterator<T>(first, second) : new ConcatIterator<T>(tail, head, headIndex, onlyCollections);
            result.state = 1; return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<T> GetQueryEnumerator(int terminal, out int count)
        { count = -1; if (terminal == 1 || terminal == 3 || terminal == 4) count = GetCheapCount(); if (count == 0) return null; return GetEnumerator(); }
        public T Current => current;
        object IEnumerator.Current => current;
        private void SetCurrent(T value) { current = value; }
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            if (enumerator != null) { enumerator.Dispose(); enumerator = null; }
            current = default(T); state = -1;
        }
        public bool MoveNext()
        {
            if (state == 1) { enumerator = Source(0).GetEnumerator(); state = 2; }
            if (state > 1)
            {
                while (true)
                {
                    if (enumerator.MoveNext()) { SetCurrent(enumerator.Current); return true; }
                    var next = Source(unchecked(state++ - 1));
                    if (next != null) { enumerator.Dispose(); enumerator = next.GetEnumerator(); continue; }
                    Dispose(); break;
                }
            }
            return false;
        }
        private static bool Cheap(IEnumerable<T> source, out int count)
        {
            if (source is ICollection<T> collection) { count = collection.Count; return true; }
            if (source is IQueryCheapCount query) { count = query.GetCheapCount(); if (count >= 0) return true; }
            if (source is ICollection legacy) { count = legacy.Count; return true; }
            count = 0; return false;
        }
        private int GetCount(bool cheap)
        {
            if (tail == null)
            {
                if (!Cheap(first, out int left)) { if (cheap) return -1; left = Enumerable.Count(first); }
                if (!Cheap(second, out int right)) { if (cheap) return -1; right = Enumerable.Count(second); }
                return checked(left + right);
            }
            if (cheap && !onlyCollections) return -1;
            int count = 0; var node = this;
            while (node.tail != null)
            {
                var source = node.head;
                int next = source is ICollection<T> collection ? collection.Count : Enumerable.Count(source);
                count = checked(count + next); node = node.tail;
            }
            return checked(count + node.GetCount(cheap));
        }
        public bool Contains(T value)
        {
            var node = this;
            while (node.tail != null)
            { if (Enumerable.Contains(node.head, value)) return true; node = node.tail; }
            return Enumerable.Contains(node.first, value) || Enumerable.Contains(node.second, value);
        }
        public int Count() => GetCount(false);
        public int GetCheapCount() => GetCount(true);
        public T TryFirst(out int found)
        {
            for (int i = 0; ; i++)
            {
                var source = Source(i); if (source == null) break;
                T item = QueryFirst<T>.Read(source, out found); if (found != 0) return item;
            }
            found = 0; return default(T);
        }
        public bool Any()
        {
            int count = GetCheapCount(); if (count >= 0) return count != 0;
            TryFirst(out int found); return found != 0;
        }
        private static void Copy(T[] source, T[] target, int index)
        { for (int i = 0; i < source.Length; i++) target[index + i] = source[i]; }
        public List<T> ToList()
        {
            int count = GetCheapCount();
            var result = count == -1 ? new List<T>() : new List<T>(count);
            for (int i = 0; ; i++)
            {
                var source = Source(i); if (source == null) break;
                result.AddRange(source);
            }
            return result;
        }
        public T[] ToArray()
        {
            if (tail != null) return onlyCollections ? Preallocated() : Buffered();
            var left = first as ICollection<T>; var right = second as ICollection<T>;
            if (left != null && right != null)
            {
                int count = left.Count; var result = new T[checked(count + right.Count)];
                left.CopyTo(result, 0); right.CopyTo(result, count); return result;
            }
            var builder = new MaterializationBuffer<T>();
            if (left != null)
            {
                int count = left.Count; foreach (T item in second) builder.Add(item);
                var items = builder.ToArray(); var result = new T[checked(count + items.Length)];
                left.CopyTo(result, 0); Copy(items, result, count); return result;
            }
            if (right != null)
            {
                int count = right.Count; foreach (T item in first) builder.Add(item);
                var items = builder.ToArray(); var result = new T[checked(items.Length + count)];
                Copy(items, result, 0); right.CopyTo(result, items.Length); return result;
            }
            foreach (T item in first) builder.Add(item);
            foreach (T item in second) builder.Add(item);
            return builder.ToArray();
        }
        private T[] Preallocated()
        {
            int count = GetCheapCount(); if (count == 0) return Array.Empty<T>();
            var result = new T[count]; int index = result.Length; var node = this;
            while (node.tail != null)
            {
                var collection = (ICollection<T>)node.head; int length = collection.Count;
                if (length > 0) { index = checked(index - length); collection.CopyTo(result, index); }
                node = node.tail;
            }
            var right = (ICollection<T>)node.second; int rightCount = right.Count;
            if (rightCount > 0) right.CopyTo(result, checked(index - rightCount));
            if (index > rightCount) ((ICollection<T>)node.first).CopyTo(result, 0);
            return result;
        }
        private T[] Buffered()
        {
            var buffer = new ConcatBuffer<T>();
            for (int i = 0; ; i++) { var source = Source(i); if (source == null) break; buffer.AddRange(source); }
            return buffer.ToArray();
        }
    }
    // Ordinary traced arrays replace spans and rented arrays. Keep the scratch /
    // segment thresholds because they determine observable Count/CopyTo callbacks.
    public sealed class ConcatSegment<T>
    {
        public T[] Items;
        public ConcatSegment<T> Previous;
        public ConcatSegment(T[] items, ConcatSegment<T> previous) { Items = items; Previous = previous; }
    }
    public sealed class ConcatBuffer<T>
    {
        private T[] current;
        private ConcatSegment<T> finished;
        private int count;
        private int previousCount;
        internal ConcatBuffer() { current = new T[8]; }
        private void Expand(int minimum)
        {
            previousCount = checked(previousCount + current.Length);
            if (previousCount > 0x7fffffc7) throw new OutOfMemoryException();
            long desired = current.Length * 2L;
            if (minimum > desired) desired = minimum;
            if (desired > 0x7fffffc7) desired = 0x7fffffc7;
            // ArrayPool.Shared uses power-of-two buckets starting at sixteen.
            int capacity = 16;
            while (capacity < desired && capacity < 1073741824) capacity *= 2;
            if (capacity < desired) capacity = (int)desired;
            finished = new ConcatSegment<T>(current, finished); current = new T[capacity]; count = 0;
        }
        private void Add(T item) { if (count == current.Length) Expand(16); current[count++] = item; }
        public void AddRange(IEnumerable<T> source)
        {
            if (source is ICollection<T> collection)
            {
                int length = collection.Count; if (length == 0) return;
                if (source is T[] array) { AddArray(array); return; }
                if (finished != null || count == current.Length)
                {
                    int remaining = current.Length - count;
                    if (remaining == 0) { Expand(length); collection.CopyTo(current, 0); count = length; return; }
                    if (length <= remaining) { collection.CopyTo(current, count); count += length; return; }
                }
            }
            foreach (T item in source) Add(item);
        }
        private void AddArray(T[] source)
        {
            int take = Math.Min(current.Length - count, source.Length);
            for (int i = 0; i < take; i++) current[count++] = source[i];
            if (take < source.Length) { Expand(source.Length - take); for (int i = take; i < source.Length; i++) current[count++] = source[i]; }
        }
        public T[] ToArray()
        {
            int total = checked(previousCount + count); if (total == 0) return Array.Empty<T>();
            var result = new T[total]; int index = previousCount;
            for (int i = 0; i < count; i++) result[index + i] = current[i];
            var segment = finished;
            while (segment != null) { index -= segment.Items.Length; for (int i = 0; i < segment.Items.Length; i++) result[index + i] = segment.Items[i]; segment = segment.Previous; }
            return result;
        }
    }
}
