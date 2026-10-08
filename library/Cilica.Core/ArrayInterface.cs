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
    // Safe ordinary managed implementation; public so mapped application calls
    // obey normal method/type access checks. No privileged storage operation.
    public static class ArraySections<T>
    {
        // Pinned Array.Clear range semantics for admitted zero-based vectors.
        public static void ClearSection(T[] array, int index, int length)
        {
            if (array == null) throw new System.ArgumentNullException("array");
            if (index < 0 || length < 0 || index > array.Length || length > array.Length - index)
                throw new System.IndexOutOfRangeException();
            for (int i = 0; i < length; i++) array[index + i] = default(T);
        }
        public static void CopyWhole(T[] sourceArray, T[] destinationArray, int length)
            => CopySection(sourceArray, 0, destinationArray, 0, length);
        public static void CopySection(T[] sourceArray, int sourceIndex, T[] destinationArray, int destinationIndex, int length)
        {
            if (sourceArray == null) throw new System.ArgumentNullException("sourceArray");
            if (destinationArray == null) throw new System.ArgumentNullException("destinationArray");
            if (length < 0)
                throw new System.ArgumentOutOfRangeException("length", length,
                    string.Concat(string.Concat("length ('", IntegerText.Int32(length)), "') must be a non-negative value."));
            if (sourceIndex < 0)
                throw new System.ArgumentOutOfRangeException("sourceIndex", sourceIndex,
                    string.Concat(string.Concat("sourceIndex ('", IntegerText.Int32(sourceIndex)), "') must be greater than or equal to '0'."));
            if (sourceIndex > sourceArray.Length || length > sourceArray.Length - sourceIndex)
                throw new System.ArgumentException("Source array was not long enough. Check the source index, length, and the array's lower bounds.", "sourceArray");
            if (destinationIndex < 0)
                throw new System.ArgumentOutOfRangeException("destinationIndex", destinationIndex,
                    string.Concat(string.Concat("destinationIndex ('", IntegerText.Int32(destinationIndex)), "') must be greater than or equal to '0'."));
            if (destinationIndex > destinationArray.Length || length > destinationArray.Length - destinationIndex)
                throw new System.ArgumentException("Destination array was not long enough. Check the destination index, length, and the array's lower bounds.", "destinationArray");
            if (!ArrayInterface<T>.CopyCompatible(sourceArray, destinationArray))
                throw new System.ArrayTypeMismatchException("Source array type cannot be assigned to destination array type.");
            if (sourceArray == destinationArray && destinationIndex > sourceIndex && destinationIndex - sourceIndex < length)
                for (int i = length - 1; i >= 0; --i) ArrayInterface<T>.CopyStore(destinationArray, destinationIndex + i, sourceArray[sourceIndex + i]);
            else
                for (int i = 0; i < length; ++i) ArrayInterface<T>.CopyStore(destinationArray, destinationIndex + i, sourceArray[sourceIndex + i]);
        }
    }
}
