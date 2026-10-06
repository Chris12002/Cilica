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
            // Clear every admitted element shape, including references inside values.
            for (int i = 0; i < oldSize; ++i) items[i] = default;
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
        public void CopyTo(T[] array, int arrayIndex) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
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
            items[size] = default;
            version++;
        }
        // Use ordinary same-assembly accessors until generic lexical private
        // field access has its own closed-owner proof.
        internal int EnumerationVersion => version;
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
            object Collections.IEnumerator.Current => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
            void Collections.IEnumerator.Reset()
            {
                if (version != list.EnumerationVersion)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                index = 0;
                current = default;
            }
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<IEnumerator<T>>.Invoke();
        Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IEnumerator>.Invoke();
        object Collections.IList.this[int index] { get => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke(); set { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); } }
        int Collections.IList.Add(object value) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        bool Collections.IList.Contains(object value) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        int Collections.IList.IndexOf(object value) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        void Collections.IList.Insert(int index, object value) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        void Collections.IList.Remove(object value) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        void Collections.ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
    }
}
