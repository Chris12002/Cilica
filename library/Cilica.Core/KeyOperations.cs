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
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern bool IsValueType();
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

namespace Cilica.Runtime
{
    internal static class ReferenceKeyOperations<T> where T : class
    {
        internal static bool Equal(ref T left, T right)
        {
            if (left == null) return right == null;
            return right != null && left.Equals(right);
        }
        internal static int Hash(ref T value) => value == null ? 0 : value.GetHashCode();
    }
    internal static class EquatableReferenceKeyOperations<T> where T : class, System.IEquatable<T>
    {
        internal static bool Equal(ref T left, T right)
        {
            if (left == null) return right == null;
            return right != null && left.Equals(right);
        }
    }
}
