// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Stack<T>; see library/provenance.json.
namespace System.Collections.Generic
{
    public class Stack<T> : IEnumerable<T>, Collections.ICollection, IReadOnlyCollection<T>
    {
        private T[] items;
        private int size;

        public Stack() { items = Array.Empty<T>(); }
        public int Count => size;

        public void Push(T item)
        {
            int oldSize = size;
            T[] array = items;
            if ((uint)oldSize < (uint)array.Length)
            {
                array[oldSize] = item;
                size = oldSize + 1;
            }
            else
            {
                Grow(oldSize + 1);
                items[size] = item;
                size++;
            }
        }

        private void Grow(int capacity)
        {
            int length = items.Length == 0 ? 4 : unchecked(items.Length * 2);
            if ((uint)length > 0x7fffffc7) length = 0x7fffffc7;
            if (length < capacity) length = capacity;
            var grown = new T[length];
            for (int index = 0; index < size; index++) grown[index] = items[index];
            items = grown;
        }

        public T Pop()
        {
            int nextSize = size - 1;
            T[] array = items;
            if ((uint)nextSize >= (uint)array.Length)
                throw new InvalidOperationException("Stack empty.");
            size = nextSize;
            T result = array[nextSize];
            // Clearing scalar slots too is unobservable through the admitted API.
            // Reference-bearing values must release all embedded references.
            array[nextSize] = default(T);
            return result;
        }

        bool Collections.ICollection.IsSynchronized => false;
        object Collections.ICollection.SyncRoot => this;
        // Preserve nominal interface facts while rejecting unimplemented traversal.
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<IEnumerator<T>>.Invoke();
        Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IEnumerator>.Invoke();
        void Collections.ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
    }
}
