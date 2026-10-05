// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of the pinned chunk append/snapshot algorithms; see provenance.
namespace System.Text
{
    public sealed class StringBuilder : Runtime.Serialization.ISerializable
    {
        // Int32 buffers hold zero-extended UTF-16 until narrow vectors are supported.
        private int[] chars;
        private int used;
        private int offset;
        private StringBuilder previous;
        public StringBuilder() { chars = new int[16]; }
        private StringBuilder(StringBuilder from)
        { chars = from.chars; used = from.used; offset = from.offset; previous = from.previous; }
        public int Length { get => used + offset; set { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); } }
        public int Capacity { get => chars.Length + offset; set { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); } }
        public int MaxCapacity => 2147483647;
        public StringBuilder Append(char value)
        {
            if (used == chars.Length) Expand(1);
            chars[used] = value;
            used++;
            return this;
        }
        public StringBuilder Append(string value)
        {
            if (value == null || value.Length == 0) return this;
            int count = value.Length;
            int remaining = chars.Length - used;
            if (count <= remaining)
            {
                for (int i = 0; i < count; ++i) chars[used + i] = value[i];
                used += count;
                return this;
            }
            int newLength = unchecked(Length + count);
            if (newLength < count)
                throw new ArgumentOutOfRangeException("valueCount", "The length cannot be greater than the capacity.");
            for (int i = 0; i < remaining; ++i) chars[used + i] = value[i];
            used = chars.Length;
            int rest = count - remaining;
            Expand(rest);
            for (int i = 0; i < rest; ++i) chars[i] = value[remaining + i];
            used = rest;
            return this;
        }
        private void Expand(int minimum)
        {
            int length = Length;
            if (unchecked(minimum + length) < minimum)
                throw new ArgumentOutOfRangeException("requiredLength", "capacity was less than the current size.");
            int size = length < 8000 ? length : 8000;
            if (size < minimum) size = minimum;
            if (unchecked(length + size) < size) throw new OutOfMemoryException();
            // Preserve allocation/failure order: create the buffer and predecessor
            // before replacing any chunk links, offset or used length.
            var buffer = new int[size];
            var predecessor = new StringBuilder(this);
            previous = predecessor;
            offset = length;
            used = 0;
            chars = buffer;
        }
        public override string ToString()
        {
            int length = Length;
            if (length == 0) return "";
            var result = new int[length];
            StringBuilder chunk = this;
            while (chunk != null)
            {
                for (int i = 0; i < chunk.used; ++i) result[chunk.offset + i] = chunk.chars[i];
                chunk = chunk.previous;
            }
            return Cilica.Runtime.StringConstruction.FromCodeUnits(result);
        }
        void Runtime.Serialization.ISerializable.GetObjectData(Runtime.Serialization.SerializationInfo info, Runtime.Serialization.StreamingContext context)
        { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
    }
}
namespace Cilica.Runtime
{
    internal static class StringConstruction
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern string FromCodeUnits(int[] values);
    }
}
