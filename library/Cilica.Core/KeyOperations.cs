// Cilica runtime-coupled default key operations. Closed-world selection replaces
// reflection; each reached operation needs its exact independently approved grant.
namespace Cilica.Runtime
{
    internal static class KeyOperations<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern int IndexOf(T[] array, T value, int count);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern bool Equal(ref T left, T right);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern int Hash(ref T value);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern bool IsNull(T value);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern bool ContainsReferences();
    }
}

namespace Cilica.Runtime
{
    internal static class CopiedArraySearch<T>
    {
        internal static int IndexOf(T[] array, T value, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                T candidate = array[i];
                if (KeyOperations<T>.Equal(ref candidate, value)) return i;
            }
            return -1;
        }
    }
    internal static class ValueArraySearch<T>
    {
        internal static int IndexOf(T[] array, T value, int count)
        {
            for (int i = 0; i < count; ++i)
                if (KeyOperations<T>.Equal(ref array[i], value)) return i;
            return -1;
        }
    }
}
