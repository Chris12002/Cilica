// Adapted from pinned dotnet/runtime SZArrayHelper; see ../provenance.json.
// Runtime-selected vector targets retain ordinary managed IL bodies.
namespace Cilica.Runtime
{
    internal static class ArrayInterface<T>
    {
        internal static bool False(T[] array) => false;
        internal static bool True(T[] array) => true;
        internal static T[] Identity(T[] array) => array;
        internal static void Add(T[] array, T value) { throw new System.NotSupportedException("Collection was of a fixed size."); }
        internal static void Insert(T[] array, int index, T value) { throw new System.NotSupportedException("Collection was of a fixed size."); }
        // Nongeneric IList.Insert must reject fixed-size arrays without casting value to T.
        internal static void InsertObject(T[] array, int index, object value) { throw new System.NotSupportedException("Collection was of a fixed size."); }
        internal static void RemoveAt(T[] array, int index) { throw new System.NotSupportedException("Collection was of a fixed size."); }
        // ICollection<T>.Clear throws; the nongeneric IList.Clear resets elements.
        internal static void ClearReadOnly(T[] array) { throw new System.NotSupportedException("Collection is read-only."); }
        internal static void Clear(T[] array)
        {
            for (int index = 0; index < array.Length; index++) array[index] = default(T);
        }
        internal static void Set(T[] array, int index, T value)
        {
            if ((uint)index >= (uint)array.Length)
                throw new System.ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
            array[index] = value;
        }
        internal static int Count(T[] array) => array.Length;
        internal static T Get(T[] array, int index)
        {
            if ((uint)index >= (uint)array.Length)
                throw new System.ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
            return array[index];
        }
    }
}
