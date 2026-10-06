// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System
{
    public enum StringComparison
    {
        CurrentCulture = 0,
        CurrentCultureIgnoreCase = 1,
        InvariantCulture = 2,
        InvariantCultureIgnoreCase = 3,
        Ordinal = 4,
        OrdinalIgnoreCase = 5,
    }
}

namespace System
{
    public sealed partial class String
    {
        public static bool IsNullOrEmpty(string value) => value == null || value.Length == 0;
        public static int CompareOrdinal(string left, string right)
        {
            if ((object)left == (object)right) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            // Match the pinned two-code-unit comparisons, including its zero
            // terminator/padding reads for short strings and odd-length prefixes.
            int difference = Cilica.Runtime.OrdinalText.Unit(left, 0) - Cilica.Runtime.OrdinalText.Unit(right, 0);
            if (difference != 0) return difference;
            difference = Cilica.Runtime.OrdinalText.Unit(left, 1) - Cilica.Runtime.OrdinalText.Unit(right, 1);
            if (difference != 0) return difference;
            int remaining = (left.Length < right.Length ? left.Length : right.Length) - 2;
            int index = 2;
            while (remaining > 0)
            {
                difference = Cilica.Runtime.OrdinalText.Unit(left, index) - Cilica.Runtime.OrdinalText.Unit(right, index);
                if (difference != 0) return difference;
                difference = Cilica.Runtime.OrdinalText.Unit(left, index + 1) - Cilica.Runtime.OrdinalText.Unit(right, index + 1);
                if (difference != 0) return difference;
                remaining -= 2;
                if (remaining > 0) index += 2;
            }
            return left.Length - right.Length;
        }
    }
}

namespace Cilica.Runtime
{
    internal static class OrdinalText
    {
        internal static int Unit(string value, int index) => index < value.Length ? value[index] : 0;
    }
}
