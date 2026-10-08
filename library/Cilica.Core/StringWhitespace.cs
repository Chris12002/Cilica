// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from pinned String.IsNullOrWhiteSpace and Char.IsWhiteSpace.
namespace System
{
    public sealed partial class String
    {
        // Owned bounded composition of pinned String.Manipulation.cs semantics.
        // Directions use bit 1 for the head and bit 2 for the tail. No native
        // pointers, temporary membership buffers or culture-dependent comparison.
        public string Trim() => TrimWhitespaceEdges(3);
        public string TrimStart() => TrimWhitespaceEdges(1);
        public string TrimEnd() => TrimWhitespaceEdges(2);

        public string Trim(char trimChar) => TrimCharacterEdges(trimChar, 3);
        public string TrimStart(char trimChar) => TrimCharacterEdges(trimChar, 1);
        public string TrimEnd(char trimChar) => TrimCharacterEdges(trimChar, 2);

        // params is lowered by the input compiler; its attribute is not required
        // by the exact array signature in this implementation library.
        public string Trim(char[] trimChars) => TrimSetEdges(trimChars, 3);
        public string TrimStart(char[] trimChars) => TrimSetEdges(trimChars, 1);
        public string TrimEnd(char[] trimChars) => TrimSetEdges(trimChars, 2);

        private string TrimWhitespaceEdges(int direction)
        {
            int start = 0, end = Length;
            if ((direction & 1) != 0)
                while (start < end && Cilica.Runtime.UnicodeWhitespace.Contains(this[start])) ++start;
            if ((direction & 2) != 0)
                while (start < end && Cilica.Runtime.UnicodeWhitespace.Contains(this[end - 1])) --end;
            return TrimmedEdges(start, end);
        }

        private string TrimCharacterEdges(char unit, int direction)
        {
            int start = 0, end = Length;
            if ((direction & 1) != 0)
                while (start < end && this[start] == unit) ++start;
            if ((direction & 2) != 0)
                while (start < end && this[end - 1] == unit) --end;
            return TrimmedEdges(start, end);
        }

        private string TrimSetEdges(char[] units, int direction)
        {
            if (units == null || units.Length == 0) return TrimWhitespaceEdges(direction);
            int start = 0, end = Length;
            if ((direction & 1) != 0)
                while (start < end && TrimSetContains(units, this[start])) ++start;
            if ((direction & 2) != 0)
                while (start < end && TrimSetContains(units, this[end - 1])) --end;
            return TrimmedEdges(start, end);
        }

        private static bool TrimSetContains(char[] units, char unit)
        {
            for (int i = 0; i < units.Length; ++i)
                if (units[i] == unit) return true;
            return false;
        }

        private string TrimmedEdges(int start, int end)
        {
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
