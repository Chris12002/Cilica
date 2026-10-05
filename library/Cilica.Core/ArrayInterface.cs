// Adapted from pinned dotnet/runtime SZArrayHelper; see ../provenance.json.
// Runtime-selected vector targets retain ordinary managed IL bodies.
namespace Cilica.Runtime
{
    internal static class ArrayInterface<T>
    {
        internal static int Count(T[] array) => array.Length;
        internal static T Get(T[] array, int index)
        {
            if ((uint)index >= (uint)array.Length)
                throw new System.ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
            return array[index];
        }
    }
}
