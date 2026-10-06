// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded default-comparer adaptation of the pinned HashSet algorithms.
namespace Cilica.Runtime
{
    public struct HashSetEntry<T>
    {
        public int Hash;
        public int Next;
        public T Value;
    }
}
namespace System.Collections.Generic
{
    public interface ISet<T> : ICollection<T>
    {
        new bool Add(T item);
        void UnionWith(IEnumerable<T> other);
        void IntersectWith(IEnumerable<T> other);
        void ExceptWith(IEnumerable<T> other);
        void SymmetricExceptWith(IEnumerable<T> other);
        bool IsSubsetOf(IEnumerable<T> other);
        bool IsSupersetOf(IEnumerable<T> other);
        bool IsProperSupersetOf(IEnumerable<T> other);
        bool IsProperSubsetOf(IEnumerable<T> other);
        bool Overlaps(IEnumerable<T> other);
        bool SetEquals(IEnumerable<T> other);
    }
    public class HashSet<T> : ISet<T>, IReadOnlyCollection<T>, Runtime.Serialization.ISerializable, Runtime.Serialization.IDeserializationCallback
    {
        private int[] buckets;
        private Cilica.Runtime.HashSetEntry<T>[] entries;
        private int count;
        private int freeList;
        private int freeCount;
        private int version;
        public HashSet() { }
        public int Count => count - freeCount;
        bool ICollection<T>.IsReadOnly => false;
        private static int Hash(ref T item) => Cilica.Runtime.KeyOperations<T>.IsNull(item) ? 0 : Cilica.Runtime.KeyOperations<T>.Hash(ref item);
        private static bool Equal(T left, T right) => Cilica.Runtime.KeyOperations<T>.Equal(ref left, right);
        private static void Concurrent()
        {
            throw new InvalidOperationException("Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.");
        }
        private int Find(T item)
        {
            if (buckets == null) return -1;
            var captured = entries;
            int hash = Hash(ref item);
            int index = buckets[(int)((uint)hash % (uint)buckets.Length)] - 1;
            int collisions = 0;
            while (index >= 0)
            {
                if (captured[index].Hash == hash && Equal(captured[index].Value, item)) return index;
                index = captured[index].Next;
                if (++collisions > captured.Length) Concurrent();
            }
            return -1;
        }
        public bool Contains(T item) => Find(item) >= 0;
        public bool TryGetValue(T equalValue, out T actualValue)
        {
            int index = Find(equalValue);
            if (index >= 0) { actualValue = entries[index].Value; return true; }
            actualValue = default; return false;
        }
        public bool Add(T item) => Insert(item);
        void ICollection<T>.Add(T item) { Insert(item); }
        private bool Insert(T item)
        {
            if (buckets == null) Initialize();
            var captured = entries;
            int hash = Hash(ref item);
            // Capture the bucket array as well as its index: comparer callbacks may
            // resize the collection after upstream has acquired its bucket byref.
            var bucketArray = buckets;
            int bucket = (int)((uint)hash % (uint)bucketArray.Length);
            int index = bucketArray[bucket] - 1;
            int collisions = 0;
            while (index >= 0)
            {
                if (captured[index].Hash == hash && Equal(captured[index].Value, item)) return false;
                index = captured[index].Next;
                if (++collisions > captured.Length) Concurrent();
            }
            if (freeCount > 0)
            {
                index = freeList; freeCount--;
                freeList = -3 - captured[index].Next;
            }
            else
            {
                int oldCount = count;
                if (oldCount == captured.Length)
                {
                    Resize(); bucketArray = buckets;
                    bucket = (int)((uint)hash % (uint)bucketArray.Length);
                }
                index = oldCount; count = oldCount + 1; captured = entries;
            }
            captured[index].Hash = hash;
            captured[index].Next = bucketArray[bucket] - 1;
            captured[index].Value = item;
            bucketArray[bucket] = index + 1;
            version++;
            return true;
        }
        public bool Remove(T item)
        {
            if (buckets == null) return false;
            var captured = entries;
            int hash = Hash(ref item);
            var bucketArray = buckets;
            int bucket = (int)((uint)hash % (uint)bucketArray.Length);
            int previous = -1, index = bucketArray[bucket] - 1, collisions = 0;
            while (index >= 0)
            {
                if (captured[index].Hash == hash && Equal(captured[index].Value, item))
                {
                    if (previous < 0) bucketArray[bucket] = captured[index].Next + 1;
                    else captured[previous].Next = captured[index].Next;
                    captured[index].Next = -3 - freeList;
                    if (Cilica.Runtime.KeyOperations<T>.ContainsReferences()) captured[index].Value = default;
                    freeList = index; freeCount++;
                    return true;
                }
                previous = index; index = captured[index].Next;
                if (++collisions > captured.Length) Concurrent();
            }
            return false;
        }
        public void Clear()
        {
            int oldCount = count;
            if (oldCount == 0) return;
            for (int i = 0; i < buckets.Length; i++) buckets[i] = 0;
            count = 0; freeList = -1; freeCount = 0;
            for (int i = 0; i < oldCount; i++) entries[i] = default;
        }
        private void Initialize()
        {
            var newBuckets = new int[3]; var newEntries = new Cilica.Runtime.HashSetEntry<T>[3];
            freeList = -1; buckets = newBuckets; entries = newEntries;
        }
        private void Resize()
        {
            int size = Cilica.Runtime.DictionaryPrimes.ExpandPrime(count);
            var replacement = new Cilica.Runtime.HashSetEntry<T>[size];
            int oldCount = count;
            for (int i = 0; i < oldCount; i++) replacement[i] = entries[i];
            var newBuckets = new int[size];
            for (int i = 0; i < oldCount; i++)
            {
                if (replacement[i].Next < -1) continue;
                int bucket = (int)((uint)replacement[i].Hash % (uint)size);
                replacement[i].Next = newBuckets[bucket] - 1; newBuckets[bucket] = i + 1;
            }
            buckets = newBuckets; entries = replacement;
        }
        public void CopyTo(T[] array, int arrayIndex) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<IEnumerator<T>>.Invoke();
        Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IEnumerator>.Invoke();
        public void UnionWith(IEnumerable<T> other) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        public void IntersectWith(IEnumerable<T> other) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        public void ExceptWith(IEnumerable<T> other) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        public void SymmetricExceptWith(IEnumerable<T> other) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        public bool IsSubsetOf(IEnumerable<T> other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public bool IsSupersetOf(IEnumerable<T> other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public bool IsProperSupersetOf(IEnumerable<T> other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public bool IsProperSubsetOf(IEnumerable<T> other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public bool Overlaps(IEnumerable<T> other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public bool SetEquals(IEnumerable<T> other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public virtual extern void GetObjectData(Runtime.Serialization.SerializationInfo info, Runtime.Serialization.StreamingContext context);
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public virtual extern void OnDeserialization(object sender);
    }
}
