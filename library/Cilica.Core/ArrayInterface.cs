// Adapted from pinned dotnet/runtime SZArrayHelper; see ../provenance.json.
// Runtime-selected vector targets retain ordinary managed IL bodies.
namespace Cilica.Runtime
{
    internal static class ArrayInterface<T>
    {
        internal static bool False(T[] array) => false;
        internal static bool True(T[] array) => true;
        internal static T[] Identity(T[] array) => array;
        internal static int Count(T[] array) => array.Length;
        internal static T Get(T[] array, int index)
        {
            if ((uint)index >= (uint)array.Length)
                throw new System.ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
            return array[index];
        }
    }
}
