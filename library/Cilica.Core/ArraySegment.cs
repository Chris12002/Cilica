// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned ArraySegment/ThrowHelper: constructors and views.
namespace System
{
    public readonly struct ArraySegment<T> : Collections.Generic.IList<T>, Collections.Generic.IReadOnlyList<T>
    {
        private readonly T[] array;
        private readonly int offset;
        private readonly int count;
        public ArraySegment(T[] array)
        {
            if (array == null) throw new ArgumentNullException("array");
            this.array = array; offset = 0; count = array.Length;
        }
        public ArraySegment(T[] array, int offset, int count)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Non-negative number required.");
            if (count < 0) throw new ArgumentOutOfRangeException("count", "Non-negative number required.");
            if (offset > array.Length || count > array.Length - offset)
                throw new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            this.array = array; this.offset = offset; this.count = count;
        }
        public T[] Array => array;
        public int Offset => offset;
        public int Count => count;
        // Retain contract interface metadata (Count is an implicit virtual/final slot).
        // Unimplemented interface bodies fail discovery rather than supply fake behavior.
        T Collections.Generic.IList<T>.this[int index]
        { get => Cilica.Runtime.UnsupportedLibraryMember<T>.Invoke(); set { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); } }
        T Collections.Generic.IReadOnlyList<T>.this[int index] => Cilica.Runtime.UnsupportedLibraryMember<T>.Invoke();
        int Collections.Generic.IList<T>.IndexOf(T item) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        void Collections.Generic.IList<T>.Insert(int index,T item) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        void Collections.Generic.IList<T>.RemoveAt(int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool Collections.Generic.ICollection<T>.IsReadOnly => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        void Collections.Generic.ICollection<T>.Add(T item) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        void Collections.Generic.ICollection<T>.Clear() { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool Collections.Generic.ICollection<T>.Contains(T item) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        bool Collections.Generic.ICollection<T>.Remove(T item) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public void CopyTo(T[] destination,int destinationIndex) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        Collections.Generic.IEnumerator<T> Collections.Generic.IEnumerable<T>.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.Generic.IEnumerator<T>>.Invoke();
        Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IEnumerator>.Invoke();
        // Do not accidentally inherit different value equality/hash/text behavior.
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        public override string ToString() => Cilica.Runtime.UnsupportedLibraryMember<string>.Invoke();
    }
}
