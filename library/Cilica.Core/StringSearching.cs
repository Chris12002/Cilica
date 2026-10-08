// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded managed scan adapted from pinned String.Searching.cs.
namespace System
{
    public sealed partial class String
    {
        public int LastIndexOf(char value) => LastIndexOf(value, Length - 1, Length);
        public int LastIndexOf(char value, int startIndex) => LastIndexOf(value, startIndex, unchecked(startIndex + 1));
        public int LastIndexOf(char value, int startIndex, int count)
        {
            // The pinned implementation returns before validating empty ranges.
            if (Length == 0) return -1;
            if ((uint)startIndex >= (uint)Length)
                throw new ArgumentOutOfRangeException("startIndex", "Index was out of range. Must be non-negative and less than the size of the collection.");
            if ((uint)count > (uint)startIndex + 1)
                throw new ArgumentOutOfRangeException("count", "Count must be positive and count must refer to a location within the string/array/collection.");
            int first = startIndex + 1 - count;
            for (int index = startIndex; index >= first; --index)
                if (this[index] == value) return index;
            return -1;
        }

        public int IndexOf(char value) => IndexOf(value, 0, Length);
        public int IndexOf(char value, int startIndex) => IndexOf(value, startIndex, unchecked(Length - startIndex));
        public int IndexOf(char value, int startIndex, int count)
        {
            if ((uint)startIndex > (uint)Length)
                throw new ArgumentOutOfRangeException("startIndex", "Index was out of range. Must be non-negative and less than or equal to the size of the collection.");
            if ((uint)count > (uint)(Length - startIndex))
                throw new ArgumentOutOfRangeException("count", "Count must be positive and count must refer to a location within the string/array/collection.");
            int end = startIndex + count;
            for (int index = startIndex; index < end; ++index)
                if (this[index] == value) return index;
            return -1;
        }
    }
}
