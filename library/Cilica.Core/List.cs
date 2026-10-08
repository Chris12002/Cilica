// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned List<T> storage, growth, indexed mutation and snapshots.
namespace System.Collections.Generic
{
    public class List<T> : IList<T>, Collections.IList, IReadOnlyList<T>
    {
        private T[] items;
        private int size;
        private int version;
        // Public empty snapshots share per closed T, separately from Array.Empty<T>.
        private static T[] emptySnapshot;
        public List() { items = Array.Empty<T>(); }
        public List(int capacity) { items = CreateStorage(capacity); }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public extern List(IEnumerable<T> collection);
        private List(bool uninitialized) { }
        // Public metadata bridge for admitted enumerable sources. Allocating
        // first preserves newobj order without allocating empty storage before null.
        public static List<T> CopyEnumerable(IEnumerable<T> collection)
        {
            var result = new List<T>(true);
            if (collection == null) throw new ArgumentNullException("collection");
            if (collection is ICollection<T> source)
            {
                int count = source.Count;
                if (count == 0) result.items = Array.Empty<T>();
                else
                {
                    result.items = new T[count];
                    source.CopyTo(result.items, 0);
                    result.size = count;
                }
            }
            else
            {
                result.items = Array.Empty<T>();
                using (IEnumerator<T> cursor = collection.GetEnumerator())
                    while (cursor.MoveNext()) result.Add(cursor.Current);
            }
            return result;
        }
        private static T[] CreateStorage(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException("capacity", "Non-negative number required.");
            return capacity == 0 ? Array.Empty<T>() : new T[capacity];
        }
        public int Count => size;
        public int Capacity
        {
            get => items.Length;
            set
            {
                if (value < size) throw new ArgumentOutOfRangeException("value", "capacity was less than the current size.");
                if (value == items.Length) return;
                if (value == 0) { items = Array.Empty<T>(); return; }
                var replacement = new T[value];
                for (int i = 0; i < size; ++i) replacement[i] = items[i];
                items = replacement;
            }
        }
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)size) throw new ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
                return items[index];
            }
            set
            {
                if ((uint)index >= (uint)size) throw new ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
                items[index] = value;
                version++;
            }
        }
        public void Add(T item)
        {
            version++;
            int oldSize = size;
            if ((uint)oldSize >= (uint)items.Length) Grow(oldSize + 1);
            size = oldSize + 1;
            items[oldSize] = item;
        }
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException("collection");
            if (collection is ICollection<T> source)
            {
                int count = source.Count;
                if (count > 0)
                {
                    if (Capacity - size < count) Grow(checked(size + count));
                    source.CopyTo(items, size);
                    size = unchecked(size + count);
                    version++;
                }
            }
            else
            {
                using (IEnumerator<T> cursor = collection.GetEnumerator())
                    while (cursor.MoveNext()) Add(cursor.Current);
            }
        }
        private void Grow(int capacity)
        {
            int length = Capacity;
            int next = length == 0 ? 4 : unchecked(length * 2);
            if ((uint)next > 0x7fffffc7) next = 0x7fffffc7;
            if (next < capacity) next = capacity;
            Capacity = next;
        }
        public ObjectModel.ReadOnlyCollection<T> AsReadOnly() => new ObjectModel.ReadOnlyCollection<T>(this);
        bool ICollection<T>.IsReadOnly => false;
        bool Collections.IList.IsReadOnly => false;
        bool Collections.IList.IsFixedSize => false;
        bool Collections.ICollection.IsSynchronized => false;
        object Collections.ICollection.SyncRoot => this;
        // Keep the exact nominal interface contract; unimplemented reachable APIs
        // fail during compilation through the selected unsupported-member marker.
        public void Clear()
        {
            version++;
            int oldSize = size;
            size = 0;
            // Match the managed backing-store contract when callbacks still hold it.
            if (Cilica.Runtime.KeyOperations<T>.ContainsReferences())
                for (int i = 0; i < oldSize; ++i) items[i] = default;
        }
        public T Find(Predicate<T> match)
        {
            if (match == null) throw new ArgumentNullException("match");
            // Both size and storage are live: the callback may mutate the list.
            for (int i = 0; i < size; i++)
                if (match(items[i])) return items[i];
            return default;
        }
        public bool Exists(Predicate<T> match) => FindIndex(match) != -1;
        public int FindIndex(Predicate<T> match) => FindIndex(0, size, match);
        public int FindIndex(int startIndex, Predicate<T> match) => FindIndex(startIndex, unchecked(size - startIndex), match);
        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            if ((uint)startIndex > (uint)size)
                throw new ArgumentOutOfRangeException("startIndex", "Index was out of range. Must be non-negative and less than or equal to the size of the collection.");
            if (count < 0 || startIndex > size - count)
                throw new ArgumentOutOfRangeException("count", "Count must be positive and count must refer to a location within the string/array/collection.");
            if (match == null) throw new ArgumentNullException("match");
            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
                if (match(items[i])) return i;
            return -1;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public extern void Sort(IComparer<T> comparer);
        // Selected only for List<string>.Sort with a directly proven Ordinal getter.
        public void SortOrdinal()
        {
            if (size > 1) Cilica.Runtime.ComparisonSort<T>.Sort(items, size, new Comparison<T>(Cilica.Runtime.ComparisonSort<T>.CompareOrdinal));
            version++;
        }
        public void Sort()
        {
            if (size > 1) Cilica.Runtime.ComparisonSort<T>.Sort(items, size, new Comparison<T>(Cilica.Runtime.ComparisonSort<T>.CompareDefault));
            version++;
        }
        public void Sort(Comparison<T> comparison)
        {
            if (comparison == null) throw new ArgumentNullException("comparison");
            if (size > 1) Cilica.Runtime.ComparisonSort<T>.Sort(items, size, comparison);
            version++;
        }
        public void Reverse() => Reverse(0, size);
        public void Reverse(int index, int count)
        {
            if (index < 0) throw new ArgumentOutOfRangeException("index", "Non-negative number required.");
            if (count < 0) throw new ArgumentOutOfRangeException("count", "Non-negative number required.");
            if (size - index < count)
                throw new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            if (count > 1)
            {
                int left = index, right = index + count - 1;
                while (left < right)
                {
                    T temporary = items[left];
                    items[left++] = items[right];
                    items[right--] = temporary;
                }
            }
            version++;
        }
        public T[] ToArray()
        {
            if (size == 0)
            {
                var empty = emptySnapshot;
                if (empty == null) { empty = new T[0]; emptySnapshot = empty; }
                return empty;
            }
            var snapshot = new T[size];
            for (int i = 0; i < size; ++i) snapshot[i] = items[i];
            return snapshot;
        }
        public bool Contains(T value) => IndexOf(value) >= 0;
        public void CopyTo(T[] array) => CopyTo(array, 0);
        public void CopyTo(T[] array, int arrayIndex) => Cilica.Runtime.ArrayInterface<T>.CopyPrefix(items, size, array, arrayIndex);
        public bool Remove(T value)
        {
            int index = IndexOf(value);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }
        public int IndexOf(T value) => Cilica.Runtime.KeyOperations<T>.IndexOf(items, value, size);
        public void Insert(int index, T value)
        {
            if ((uint)index > (uint)size)
                throw new ArgumentOutOfRangeException("index", "Index must be within the bounds of the List.");
            if (size == items.Length) Grow(checked(size + 1));
            for (int i = size; i > index; --i) items[i] = items[i - 1];
            items[index] = value;
            size++;
            version++;
        }
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)size)
                throw new ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
            size--;
            for (int i = index; i < size; ++i) items[i] = items[i + 1];
            if (Cilica.Runtime.KeyOperations<T>.ContainsReferences()) items[size] = default;
            version++;
        }
        // Use ordinary same-assembly accessors until generic lexical private
        // field access has its own closed-owner proof.
        internal int EnumerationVersion => version;
        // Owned query materializers use the pinned AsSpan snapshot semantics.
        // This remains managed array storage; no public Span API is introduced.
        internal T[] QueryStorage(out int count)
        {
            count = size;
            var captured = items;
            int length = captured.Length;
            if ((uint)count > (uint)length)
                throw new InvalidOperationException("Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.");
            return captured;
        }
        public Enumerator GetEnumerator() => new Enumerator(this);
        public struct Enumerator : IEnumerator<T>, Collections.IEnumerator
        {
            private readonly List<T> list;
            private readonly int version;
            private int index;
            private T current;
            internal Enumerator(List<T> list)
            {
                this.list = list;
                version = list.EnumerationVersion;
                index = 0;
                current = default;
            }
            public void Dispose() { }
            public bool MoveNext()
            {
                if (version != list.EnumerationVersion)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                if ((uint)index < (uint)list.Count)
                {
                    current = list[index];
                    index++;
                    return true;
                }
                current = default;
                index = -1;
                return false;
            }
            public T Current => current;
            object Collections.IEnumerator.Current
            {
                get
                {
                    if (index <= 0) throw new InvalidOperationException("Enumeration has either not started or has already finished.");
                    return current;
                }
            }
            void Collections.IEnumerator.Reset()
            {
                if (version != list.EnumerationVersion)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                index = 0;
                current = default;
            }
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => size == 0
            ? System.SZGenericArrayEnumerator<T>.Create(null, 0) : GetEnumerator();
        Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();
        object Collections.IList.this[int index] { get => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke(); set { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); } }
        int Collections.IList.Add(object value) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        bool Collections.IList.Contains(object value) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        int Collections.IList.IndexOf(object value) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        void Collections.IList.Insert(int index, object value) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        void Collections.IList.Remove(object value) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        void Collections.ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
    }
}
