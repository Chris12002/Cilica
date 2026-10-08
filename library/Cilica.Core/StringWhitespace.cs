// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from pinned String.IsNullOrWhiteSpace and Char.IsWhiteSpace.
namespace System
{
    public sealed partial class String
    {
        // Owned composition of the existing pinned whitespace predicate and
        // checked substring operation; no new intrinsic or culture dependency.
        public string Trim()
        {
            int start = 0, end = Length;
            while (start < end && Cilica.Runtime.UnicodeWhitespace.Contains(this[start])) ++start;
            while (start < end && Cilica.Runtime.UnicodeWhitespace.Contains(this[end - 1])) --end;
            if (start == 0 && end == Length) return this;
            return Substring(start, end - start);
        }

        public static bool IsNullOrWhiteSpace(string value)
        {
            if (value == null) return true;
            for (int i = 0; i < value.Length; i++)
            {
                if (!Cilica.Runtime.UnicodeWhitespace.Contains(value[i])) return false;
            }
            return true;
        }
    }
}
