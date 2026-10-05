// Cilica runtime-coupled default key operations. Closed-world selection replaces
// reflection; each reached operation needs its exact independently approved grant.
namespace Cilica.Runtime
{
    internal static class KeyOperations<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern bool Equal(ref T left, T right);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern int Hash(ref T value);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern bool IsNull(T value);
    }
}
