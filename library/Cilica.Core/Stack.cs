// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Stack<T>; see library/provenance.json.
namespace System.Collections.Generic
{
    public class Stack<T> : IEnumerable<T>, Collections.ICollection, IReadOnlyCollection<T>
    {
        private T[] items;
        private int size;
        private int version;

        public Stack() { items = Array.Empty<T>(); }
        public int Count => size;

        public void Push(T item)
        {
            int oldSize = size;
            T[] array = items;
            if ((uint)oldSize < (uint)array.Length)
            {
                array[oldSize] = item;
                version = unchecked(version + 1);
                size = oldSize + 1;
            }
            else
            {
                Grow(oldSize + 1);
                items[size] = item;
                version = unchecked(version + 1);
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
            version = unchecked(version + 1);
            size = nextSize;
            T result = array[nextSize];
            // Clearing scalar slots too is unobservable through the admitted API.
            // Reference-bearing values must release all embedded references.
            array[nextSize] = default(T);
            return result;
        }

        bool Collections.ICollection.IsSynchronized => false;
        object Collections.ICollection.SyncRoot => this;
        internal int EnumerationVersion => version;
        internal T[] EnumerationStorage => items;
        public Enumerator GetEnumerator() => new Enumerator(this);
        public struct Enumerator : IEnumerator<T>, Collections.IEnumerator
        {
            private readonly Stack<T> stack;
            private readonly int version;
            private int index;
            private T current;
            internal Enumerator(Stack<T> stack)
            { this.stack = stack; version = stack.EnumerationVersion; index = stack.Count; current = default; }
            public void Dispose() { index = -1; }
            public bool MoveNext()
            {
                if (version != stack.EnumerationVersion)
                    throw new InvalidOperationException("Collection was modified after the enumerator was instantiated.");
                T[] array = stack.EnumerationStorage;
                int next = unchecked(index - 1);
                if ((uint)next < (uint)array.Length)
                { current = array[next]; index = next; return true; }
                current = default; index = -1; return false;
            }
            public T Current => current;
            object Collections.IEnumerator.Current => Current;
            void Collections.IEnumerator.Reset()
            {
                if (version != stack.EnumerationVersion)
                    throw new InvalidOperationException("Collection was modified after the enumerator was instantiated.");
                index = stack.Count; current = default;
            }
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => size == 0
            ? System.SZGenericArrayEnumerator<T>.Create(null, 0) : GetEnumerator();
        Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();
        void Collections.ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
    }
}
