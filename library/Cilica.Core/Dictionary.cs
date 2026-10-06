// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Cilica bounded adaptation: retain prime capacities, hash/bucket chains and
// entry-slot order and free-slot reuse; private reference entries carry typed fields.
// See ../provenance.json for the pinned originals and deliberately omitted APIs.
namespace Cilica.Runtime
{
    public sealed class DictionaryEntry<TKey, TValue>
    {
        public uint Hash;
        public int Next;
        public TKey Key;
        public TValue Value;
        public DictionaryEntry(uint hash, int next, TKey key, TValue value)
        { Hash = hash; Next = next; Key = key; Value = value; }
    }
    internal static class DictionaryPrimes
    {
        internal static int ExpandPrime(int oldSize)
        {
            int size = unchecked(2 * oldSize);
            if ((uint)size > 0x7FFFFFC3 && oldSize < 0x7FFFFFC3) return 0x7FFFFFC3;
            return GetPrime(size);
        }
        internal static int GetPrime(int min)
        {
            if (min < 0) throw new System.ArgumentException("Hashtable's capacity overflowed and went negative. Check load factor, capacity and the current size of the table.");
            if (min <= 3) return 3;
            if (min <= 7) return 7;
            if (min <= 11) return 11;
            if (min <= 17) return 17;
            if (min <= 23) return 23;
            if (min <= 29) return 29;
            if (min <= 37) return 37;
            if (min <= 47) return 47;
            if (min <= 59) return 59;
            if (min <= 71) return 71;
            if (min <= 89) return 89;
            if (min <= 107) return 107;
            if (min <= 131) return 131;
            if (min <= 163) return 163;
            if (min <= 197) return 197;
            if (min <= 239) return 239;
            if (min <= 293) return 293;
            if (min <= 353) return 353;
            if (min <= 431) return 431;
            if (min <= 521) return 521;
            if (min <= 631) return 631;
            if (min <= 761) return 761;
            if (min <= 919) return 919;
            if (min <= 1103) return 1103;
            if (min <= 1327) return 1327;
            if (min <= 1597) return 1597;
            if (min <= 1931) return 1931;
            if (min <= 2333) return 2333;
            if (min <= 2801) return 2801;
            if (min <= 3371) return 3371;
            if (min <= 4049) return 4049;
            if (min <= 4861) return 4861;
            if (min <= 5839) return 5839;
            if (min <= 7013) return 7013;
            if (min <= 8419) return 8419;
            if (min <= 10103) return 10103;
            if (min <= 12143) return 12143;
            if (min <= 14591) return 14591;
            if (min <= 17519) return 17519;
            if (min <= 21023) return 21023;
            if (min <= 25229) return 25229;
            if (min <= 30293) return 30293;
            if (min <= 36353) return 36353;
            if (min <= 43627) return 43627;
            if (min <= 52361) return 52361;
            if (min <= 62851) return 62851;
            if (min <= 75431) return 75431;
            if (min <= 90523) return 90523;
            if (min <= 108631) return 108631;
            if (min <= 130363) return 130363;
            if (min <= 156437) return 156437;
            if (min <= 187751) return 187751;
            if (min <= 225307) return 225307;
            if (min <= 270371) return 270371;
            if (min <= 324449) return 324449;
            if (min <= 389357) return 389357;
            if (min <= 467237) return 467237;
            if (min <= 560689) return 560689;
            if (min <= 672827) return 672827;
            if (min <= 807403) return 807403;
            if (min <= 968897) return 968897;
            if (min <= 1162687) return 1162687;
            if (min <= 1395263) return 1395263;
            if (min <= 1674319) return 1674319;
            if (min <= 2009191) return 2009191;
            if (min <= 2411033) return 2411033;
            if (min <= 2893249) return 2893249;
            if (min <= 3471899) return 3471899;
            if (min <= 4166287) return 4166287;
            if (min <= 4999559) return 4999559;
            if (min <= 5999471) return 5999471;
            if (min <= 7199369) return 7199369;
            for (int candidate = min | 1; candidate < 2147483647; candidate += 2)
                if (IsPrime(candidate) && (candidate - 1) % 101 != 0) return candidate;
            return min;
        }
        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) == 0) return candidate == 2;
            for (int divisor = 3; (long)divisor * divisor <= candidate; divisor += 2)
                if (candidate % divisor == 0) return false;
            return true;
        }
    }
}
namespace System.Collections.Generic
{
    using System;
    public class Dictionary<TKey, TValue> : IDictionary<TKey, TValue>, Collections.IDictionary, IReadOnlyDictionary<TKey, TValue>,
        Runtime.Serialization.ISerializable, Runtime.Serialization.IDeserializationCallback
    {
        private int[] buckets;
        private Cilica.Runtime.DictionaryEntry<TKey, TValue>[] entries;
        private int count;
        private int freeList;
        private int freeCount;
        private int version;
        public Dictionary() { freeList = -1; }
        public int Count => count - freeCount;
        private KeyCollection keys;
        private ValueCollection values;
        public KeyCollection Keys
        {
            get { if (keys == null) keys = new KeyCollection(this); return keys; }
        }
        public ValueCollection Values
        {
            get { if (values == null) values = new ValueCollection(this); return values; }
        }

        // FindValue and equality preserve EqualityComparer<T>'s by-value receiver
        // semantics. TryInsert deliberately hashes its own key parameter byref,
        // matching the pinned source even when a value key mutates its receiver.
        private static bool Equal(TKey left, TKey right) => Cilica.Runtime.KeyOperations<TKey>.Equal(ref left, right);
        private static uint Hash(TKey key) => unchecked((uint)Cilica.Runtime.KeyOperations<TKey>.Hash(ref key));
        private static void CheckKey(TKey key)
        { if (Cilica.Runtime.KeyOperations<TKey>.IsNull(key)) throw new ArgumentNullException("key"); }
        private Cilica.Runtime.DictionaryEntry<TKey, TValue> Find(TKey key)
        {
            CheckKey(key);
            if (buckets == null) return null;
            uint hash = Hash(key);
            int index = buckets[(int)(hash % (uint)buckets.Length)] - 1;
            int collisions = 0;
            while ((uint)index < (uint)entries.Length)
            {
                var entry = entries[index];
                if (entry.Hash == hash && Equal(entry.Key, key)) return entry;
                index = entry.Next;
                if (++collisions > entries.Length) throw new InvalidOperationException("Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.");
            }
            return null;
        }
        public bool TryGetValue(TKey key, out TValue value)
        {
            var entry = Find(key);
            if (entry != null) { value = entry.Value; return true; }
            value = default; return false;
        }
        public bool TryAdd(TKey key, TValue value) => Insert(key, value, false);
        public TValue this[TKey key] {
            get => Cilica.Runtime.UnsupportedLibraryMember<TValue>.Invoke();
            set { Insert(key, value, true); }
        }
        public bool ContainsKey(TKey key) => Find(key) != null;
        private static bool ValueEqual(TValue left, TValue right) => Cilica.Runtime.KeyOperations<TValue>.Equal(ref left, right);
        public bool ContainsValue(TValue value)
        {
            var capturedEntries = entries;
            if (Cilica.Runtime.KeyOperations<TValue>.IsNull(value))
            {
                for (int i = 0; i < count; ++i)
                    if (capturedEntries[i].Next >= -1 && Cilica.Runtime.KeyOperations<TValue>.IsNull(capturedEntries[i].Value)) return true;
            }
            else
            {
                for (int i = 0; i < count; ++i)
                    if (capturedEntries[i].Next >= -1 && ValueEqual(capturedEntries[i].Value, value)) return true;
            }
            return false;
        }

        public void Add(TKey key, TValue value) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        public bool Remove(TKey key) => Remove(key, out _);
        public bool Remove(TKey key, out TValue value)
        {
            CheckKey(key);
            if (buckets != null)
            {
                // Remove, like TryInsert, hashes its own key parameter before comparison.
                uint hash = unchecked((uint)Cilica.Runtime.KeyOperations<TKey>.Hash(ref key));
                int bucket = (int)(hash % (uint)buckets.Length);
                int previous = -1;
                int index = buckets[bucket] - 1;
                int collisions = 0;
                while ((uint)index < (uint)entries.Length)
                {
                    var entry = entries[index];
                    if (entry.Hash == hash && Equal(entry.Key, key))
                    {
                        if (previous < 0) buckets[bucket] = entry.Next + 1;
                        else entries[previous].Next = entry.Next;
                        value = entry.Value;
                        entry.Next = -3 - freeList;
                        entry.Key = default;
                        entry.Value = default;
                        freeList = index;
                        freeCount++;
                        return true;
                    }
                    previous = index;
                    index = entry.Next;
                    if (++collisions > entries.Length) throw new InvalidOperationException("Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.");
                }
            }
            value = default;
            return false;
        }
        public void Clear()
        {
            if (count == 0) return;
            for (int i = 0; i < buckets.Length; ++i) buckets[i] = 0;
            for (int i = 0; i < count; ++i) entries[i] = null;
            count = 0;
            freeList = -1;
            freeCount = 0;
        }
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public virtual extern void GetObjectData(Runtime.Serialization.SerializationInfo info, Runtime.Serialization.StreamingContext context);
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public virtual extern void OnDeserialization(object sender);
        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;
        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<IEnumerator<KeyValuePair<TKey, TValue>>>.Invoke();
        Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IEnumerator>.Invoke();
        bool Collections.ICollection.IsSynchronized => false;
        object Collections.ICollection.SyncRoot => this;
        void Collections.ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool Collections.IDictionary.IsFixedSize => false;
        bool Collections.IDictionary.IsReadOnly => false;
        Collections.ICollection Collections.IDictionary.Keys => Keys;
        Collections.ICollection Collections.IDictionary.Values => Values;
        object Collections.IDictionary.this[object key] {
            get => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
            set { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        }
        void Collections.IDictionary.Add(object key, object value) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool Collections.IDictionary.Contains(object key) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        Collections.IDictionaryEnumerator Collections.IDictionary.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IDictionaryEnumerator>.Invoke();
        void Collections.IDictionary.Remove(object key) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        internal int EnumerationVersion => version;
        internal int EnumerationCount => count;
        internal Cilica.Runtime.DictionaryEntry<TKey, TValue> EnumerationEntry(int index) => entries[index];
        public Enumerator GetEnumerator() => new Enumerator(this);
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, Collections.IDictionaryEnumerator
        {
            private readonly Dictionary<TKey, TValue> dictionary;
            private readonly int version;
            private int index;
            private KeyValuePair<TKey, TValue> current;
            internal Enumerator(Dictionary<TKey, TValue> dictionary)
            {
                this.dictionary = dictionary;
                version = dictionary.EnumerationVersion;
                index = 0;
                current = default;
            }
            public bool MoveNext()
            {
                if (version != dictionary.EnumerationVersion)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                while ((uint)index < (uint)dictionary.EnumerationCount)
                {
                    var entry = dictionary.EnumerationEntry(index++);
                    if (entry.Next >= -1)
                    {
                        var pair = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
                        current = pair;
                        return true;
                    }
                }
                index = unchecked(dictionary.EnumerationCount + 1);
                current = default;
                return false;
            }
            public KeyValuePair<TKey, TValue> Current => current;
            public void Dispose() { }
            void Collections.IEnumerator.Reset()
            {
                if (version != dictionary.EnumerationVersion)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                index = 0;
                current = default;
            }
            object Collections.IEnumerator.Current => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
            Collections.DictionaryEntry Collections.IDictionaryEnumerator.Entry => Cilica.Runtime.UnsupportedLibraryMember<Collections.DictionaryEntry>.Invoke();
            object Collections.IDictionaryEnumerator.Key => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
            object Collections.IDictionaryEnumerator.Value => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
        }
        public sealed class KeyCollection : ICollection<TKey>, Collections.ICollection, IReadOnlyCollection<TKey>
        {
            private readonly Dictionary<TKey, TValue> dictionary;
            public KeyCollection(Dictionary<TKey, TValue> dictionary) { this.dictionary = Validate(dictionary); }
            private static Dictionary<TKey, TValue> Validate(Dictionary<TKey, TValue> dictionary)
            {
                if (dictionary == null) throw new ArgumentNullException("dictionary");
                return dictionary;
            }
            public int Count => dictionary.Count;
            bool ICollection<TKey>.IsReadOnly => true;
            bool Collections.ICollection.IsSynchronized => false;
            object Collections.ICollection.SyncRoot => ((Collections.ICollection)dictionary).SyncRoot;
            void ICollection<TKey>.Add(TKey item) { throw new NotSupportedException("Mutating a key collection derived from a dictionary is not allowed."); }
            bool ICollection<TKey>.Remove(TKey item) { throw new NotSupportedException("Mutating a key collection derived from a dictionary is not allowed."); }
            void ICollection<TKey>.Clear() { throw new NotSupportedException("Mutating a key collection derived from a dictionary is not allowed."); }
            public bool Contains(TKey item) => dictionary.ContainsKey(item);
            public void CopyTo(TKey[] array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
            void Collections.ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<IEnumerator<TKey>>.Invoke();
            Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IEnumerator>.Invoke();
        }
        public sealed class ValueCollection : ICollection<TValue>, Collections.ICollection, IReadOnlyCollection<TValue>
        {
            private readonly Dictionary<TKey, TValue> dictionary;
            public ValueCollection(Dictionary<TKey, TValue> dictionary) { this.dictionary = Validate(dictionary); }
            private static Dictionary<TKey, TValue> Validate(Dictionary<TKey, TValue> dictionary)
            {
                if (dictionary == null) throw new ArgumentNullException("dictionary");
                return dictionary;
            }
            public int Count => dictionary.Count;
            bool ICollection<TValue>.IsReadOnly => true;
            bool Collections.ICollection.IsSynchronized => false;
            object Collections.ICollection.SyncRoot => ((Collections.ICollection)dictionary).SyncRoot;
            void ICollection<TValue>.Add(TValue item) { throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed."); }
            bool ICollection<TValue>.Remove(TValue item) { throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed."); }
            void ICollection<TValue>.Clear() { throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed."); }
            bool ICollection<TValue>.Contains(TValue item) => dictionary.ContainsValue(item);
            public void CopyTo(TValue[] array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
            void Collections.ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<IEnumerator<TValue>>.Invoke();
            Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IEnumerator>.Invoke();
        }
        private bool Insert(TKey key, TValue value, bool overwrite)
        {
            CheckKey(key);
            if (buckets == null)
            {
                var newBuckets = new int[3];
                var newEntries = new Cilica.Runtime.DictionaryEntry<TKey, TValue>[3];
                buckets = newBuckets; entries = newEntries;
            }
            uint hash = unchecked((uint)Cilica.Runtime.KeyOperations<TKey>.Hash(ref key));
            int bucket = (int)(hash % (uint)buckets.Length);
            int index = buckets[bucket] - 1;
            int collisions = 0;
            while ((uint)index < (uint)entries.Length)
            {
                var entry = entries[index];
                if (entry.Hash == hash && Equal(entry.Key, key))
                {
                    if (!overwrite) return false;
                    entry.Value = value; return true;
                }
                index = entry.Next;
                if (++collisions > entries.Length) throw new InvalidOperationException("Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.");
            }
            if (freeCount > 0)
            {
                int slot = freeList;
                var reused = entries[slot];
                freeList = -3 - reused.Next;
                freeCount--;
                reused.Hash = hash;
                reused.Next = buckets[bucket] - 1;
                reused.Key = key;
                reused.Value = value;
                buckets[bucket] = slot + 1;
                version++;
                return true;
            }
            if (count == entries.Length)
            {
                Resize();
                bucket = (int)(hash % (uint)buckets.Length);
            }
            var added = new Cilica.Runtime.DictionaryEntry<TKey, TValue>(hash, buckets[bucket] - 1, key, value);
            entries[count] = added;
            buckets[bucket] = count + 1;
            count++;
            version++;
            return true;
        }
        private void Resize()
        {
            int size = Cilica.Runtime.DictionaryPrimes.ExpandPrime(count);
            var newEntries = new Cilica.Runtime.DictionaryEntry<TKey, TValue>[size];
            var newBuckets = new int[size];
            for (int i = 0; i < count; ++i) newEntries[i] = entries[i];
            for (int i = 0; i < count; ++i)
            {
                var entry = newEntries[i];
                if (entry.Next < -1) continue;
                int bucket = (int)(entry.Hash % (uint)size);
                entry.Next = newBuckets[bucket] - 1;
                newBuckets[bucket] = i + 1;
            }
            entries = newEntries; buckets = newBuckets;
        }
    }
}
