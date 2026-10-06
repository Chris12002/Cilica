// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Array.Enumerators.cs; see provenance.
using System.Collections.Generic;
namespace System
{
    internal sealed class SZGenericArrayEnumerator<T> : IEnumerator<T>
    {
        private readonly T[] array;
        private readonly int endIndex;
        private int index;
        private static SZGenericArrayEnumerator<T> empty;
        private SZGenericArrayEnumerator(T[] array, int length)
        {
            this.array = array;
            endIndex = length;
            index = -1;
        }
        internal static IEnumerator<T> Create(T[] array, int length)
        {
            if (length != 0) return new SZGenericArrayEnumerator<T>(array, length);
            if (empty == null) empty = new SZGenericArrayEnumerator<T>(null, 0);
            return empty;
        }
        public bool MoveNext()
        {
            int next = unchecked(index + 1);
            if ((uint)next < (uint)endIndex) { index = next; return true; }
            index = endIndex;
            return false;
        }
        public T Current
        {
            get
            {
                if ((uint)index >= (uint)endIndex)
                {
                    if (index < 0) throw new InvalidOperationException("Enumeration has not started. Call MoveNext.");
                    throw new InvalidOperationException("Enumeration already finished.");
                }
                return array[index];
            }
        }
        public void Reset() { index = -1; }
        public void Dispose() { }
        object Collections.IEnumerator.Current => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
    }
}
