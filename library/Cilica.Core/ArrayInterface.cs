// Adapted from pinned dotnet/runtime SZArrayHelper; see ../provenance.json.
// Runtime-selected vector targets retain ordinary managed IL bodies.
namespace Cilica.Runtime
{
    internal static class ArrayInterface<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern void CopyStore(T[] array, int index, T value);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern bool CopyCompatible(T[] source, T[] destination);
        internal static void CopyTo(T[] source, T[] destination, int index) => CopyPrefix(source, source.Length, destination, index);
        internal static void CopyPrefix(T[] source, int count, T[] destination, int index)
        {
            if (destination == null) throw new System.ArgumentNullException("destinationArray");
            if (index < 0)
                throw new System.ArgumentOutOfRangeException("destinationIndex", index,
                    string.Concat(string.Concat("destinationIndex ('", IntegerText.Int32(index)), "') must be greater than or equal to '0'."));
            if (index > destination.Length || count > destination.Length - index)
                throw new System.ArgumentException("Destination array was not long enough. Check the destination index, length, and the array's lower bounds.", "destinationArray");
            if (!CopyCompatible(source, destination))
                throw new System.ArrayTypeMismatchException("Source array type cannot be assigned to destination array type.");
            for (int i = 0; i < count; i++) CopyStore(destination, index + i, source[i]);
        }
        internal static System.Collections.Generic.IEnumerator<T> Enumerate(T[] array) => System.SZGenericArrayEnumerator<T>.Create(array, array.Length);
        internal static System.Collections.IEnumerator EnumerateObjects(T[] array) => new ObjectArrayEnumerator<T>(array, array.Length);
        internal static bool False(T[] array) => false;
        internal static bool True(T[] array) => true;
        internal static T[] Identity(T[] array) => array;
        internal static int IndexOf(T[] array, T value) => KeyOperations<T>.IndexOf(array, value, array.Length);
        internal static bool Contains(T[] array, T value) => IndexOf(array, value) >= 0;
        internal static bool Remove(T[] array, T value) { throw new System.NotSupportedException("Collection was of a fixed size."); }
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
