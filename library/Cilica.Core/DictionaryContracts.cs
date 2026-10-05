// Owned metadata contracts for the bounded Dictionary implementation. Unimplemented
// members have no executable native fallback; their reachable bodies are rejected.
using System.Runtime.CompilerServices;
namespace System.Collections
{
    public interface ICollection : IEnumerable
    {
        int Count { get; }
        bool IsSynchronized { get; }
        object SyncRoot { get; }
        void CopyTo(System.Array array, int index);
    }
    public interface IList : ICollection
    {
        object this[int index] { get; set; }
        bool IsReadOnly { get; }
        bool IsFixedSize { get; }
        int Add(object value);
        bool Contains(object value);
        void Clear();
        int IndexOf(object value);
        void Insert(int index, object value);
        void Remove(object value);
        void RemoveAt(int index);
    }
    public interface IDictionary : ICollection
    {
        object this[object key] { get; set; }
        ICollection Keys { get; }
        ICollection Values { get; }
        bool IsReadOnly { get; }
        bool IsFixedSize { get; }
        bool Contains(object key);
        void Add(object key, object value);
        void Clear();
        new IDictionaryEnumerator GetEnumerator();
        void Remove(object key);
    }
    public interface IDictionaryEnumerator : IEnumerator
    {
        object Key { get; }
        object Value { get; }
        DictionaryEntry Entry { get; }
    }
    public struct DictionaryEntry
    {
        private object key;
        private object value;
        public DictionaryEntry(object key, object value) { this.key = key; this.value = value; }
        public object Key { get => key; set => key = value; }
        public object Value { get => value; set => this.value = value; }
    }
}
namespace System.Collections.Generic
{
    public interface ICollection<T> : IEnumerable<T>
    {
        int Count { get; }
        bool IsReadOnly { get; }
        void Add(T item);
        void Clear();
        bool Contains(T item);
        void CopyTo(T[] array, int arrayIndex);
        bool Remove(T item);
    }
    public interface IList<T> : ICollection<T>
    {
        T this[int index] { get; set; }
        int IndexOf(T item);
        void Insert(int index, T item);
        void RemoveAt(int index);
    }
    public interface IDictionary<TKey, TValue> : ICollection<KeyValuePair<TKey, TValue>>
    {
        TValue this[TKey key] { get; set; }
        ICollection<TKey> Keys { get; }
        ICollection<TValue> Values { get; }
        bool ContainsKey(TKey key);
        void Add(TKey key, TValue value);
        bool Remove(TKey key);
        bool TryGetValue(TKey key, out TValue value);
    }
    public interface IReadOnlyDictionary<TKey, TValue> : IReadOnlyCollection<KeyValuePair<TKey, TValue>>
    {
        TValue this[TKey key] { get; }
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TValue> Values { get; }
        bool ContainsKey(TKey key);
        bool TryGetValue(TKey key, out TValue value);
    }
    public readonly struct KeyValuePair<TKey, TValue>
    {
        private readonly TKey key;
        private readonly TValue value;
        public KeyValuePair(TKey key, TValue value) { this.key = key; this.value = value; }
        public TKey Key => key;
        public TValue Value => value;
    }
}
namespace System.Runtime.Serialization
{
    public interface ISerializable { void GetObjectData(SerializationInfo info, StreamingContext context); }
    public interface IDeserializationCallback { void OnDeserialization(object sender); }
    public sealed class SerializationInfo { }
    public struct StreamingContext { }
}
namespace Cilica.Runtime
{
    internal static class UnsupportedLibraryMember<T>
    {
        [MethodImpl(MethodImplOptions.InternalCall)] internal static extern T Invoke();
    }
}
