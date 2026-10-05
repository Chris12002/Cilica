// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from pinned String.GetNonRandomizedHashCode; checked UTF-16 indexing
// replaces unsafe word reads. This private hash is not String.GetHashCode.
namespace Cilica.Runtime
{
    internal static class StringKeyOperations
    {
        internal static bool Equal(ref string left, string right) => string.Equals(left, right);
        private static uint Rotate(uint value) => (value << 5) | (value >> 27);
        private static uint Pair(string value, int index)
        {
            uint first = value[index];
            uint second = index + 1 < value.Length ? value[index + 1] : 0u;
            return first | (second << 16);
        }
        internal static int Hash(ref string value)
        {
            if (value == null) return 0;
            uint first = (5381u << 16) + 5381u, second = first;
            int remaining = value.Length, index = 0;
            while (remaining > 2)
            {
                remaining -= 4;
                first = (Rotate(first) + first) ^ Pair(value, index);
                second = (Rotate(second) + second) ^ Pair(value, index + 2);
                index += 4;
            }
            if (remaining > 0) second = (Rotate(second) + second) ^ Pair(value, index);
            return unchecked((int)(first + second * 1566083941u));
        }
    }
}
