// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned ReadOnlyCollection<T> live views and mutation faults.
using System.Collections.Generic;
namespace System.Collections.ObjectModel
{
    public class ReadOnlyCollection<T> : IList<T>, IList, IReadOnlyList<T>
    {
        private readonly IList<T> list;
        // Pinned Array.AsReadOnly shares a per-closed-T empty view. Keep the
        // cache private: netstandard2.1 has no public ReadOnlyCollection.Empty.
        private static ReadOnlyCollection<T> emptyView;
        internal static ReadOnlyCollection<T> FromArray(T[] array, int length)
        {
            // The typed length parameter keeps ldlen/conv.i4 explicit in the
            // caller; general native-integer branch flow is outside this core.
            if (length != 0) return new ReadOnlyCollection<T>(array);
            if (emptyView == null) emptyView = new ReadOnlyCollection<T>(System.Array.Empty<T>());
            return emptyView;
        }
        public ReadOnlyCollection(IList<T> list) { this.list = Validate(list); }
        private static IList<T> Validate(IList<T> list)
        {
            if (list == null) throw new ArgumentNullException("list");
            return list;
        }
        public int Count => list.Count;
        public T this[int index] => list[index];
        protected IList<T> Items => list;
        bool ICollection<T>.IsReadOnly => true;
        T IList<T>.this[int index] { get => list[index]; set { throw new NotSupportedException("Collection is read-only."); } }
        void ICollection<T>.Add(T value) { throw new NotSupportedException("Collection is read-only."); }
        void ICollection<T>.Clear() { throw new NotSupportedException("Collection is read-only."); }
        void IList<T>.Insert(int index, T value) { throw new NotSupportedException("Collection is read-only."); }
        bool ICollection<T>.Remove(T value) { throw new NotSupportedException("Collection is read-only."); }
        void IList<T>.RemoveAt(int index) { throw new NotSupportedException("Collection is read-only."); }
        bool IList.IsReadOnly => true;
        bool IList.IsFixedSize => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => list is ICollection collection ? collection.SyncRoot : this;
        object IList.this[int index] { get => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke(); set { throw new NotSupportedException("Collection is read-only."); } }
        int IList.Add(object value) { throw new NotSupportedException("Collection is read-only."); }
        void IList.Clear() { throw new NotSupportedException("Collection is read-only."); }
        void IList.Insert(int index, object value) { throw new NotSupportedException("Collection is read-only."); }
        void IList.Remove(object value) { throw new NotSupportedException("Collection is read-only."); }
        void IList.RemoveAt(int index) { throw new NotSupportedException("Collection is read-only."); }
        public bool Contains(T value) => list.Contains(value);
        public int IndexOf(T value) => list.IndexOf(value);
        public void CopyTo(T[] array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        public IEnumerator<T> GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<IEnumerator<T>>.Invoke();
        IEnumerator IEnumerable.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<IEnumerator>.Invoke();
        bool IList.Contains(object value) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        int IList.IndexOf(object value) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        void ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
    }
}
