// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Cilica adaptation of Number.Formatting.cs at the pinned runtime commit.
// See ../provenance.json. Only invariant decimal text is implemented here.
// Checked managed strings replace pointer writes and runtime-private allocation.

namespace Cilica.Library
{
    public static class IntegerFormatting
    {
        public static string Int32Invariant(int value) => Int64Invariant(value);
        public static string UInt32Invariant(uint value) => UInt64Invariant(value);

        public static string Int64Invariant(long value)
        {
            if (value >= 0) return UInt64Invariant((ulong)value);
            // Unsigned subtraction also handles the minimum signed value.
            return "-" + UInt64Invariant(unchecked(0UL - (ulong)value));
        }

        public static string UInt64Invariant(ulong value)
        {
            string tail = "";
            // Preserve the upstream two-digit division order, substituting
            // bounded string composition for writes through a moving pointer.
            while (value >= 100)
            {
                uint remainder = (uint)(value % 100);
                value /= 100;
                tail = TwoDigits(remainder) + tail;
            }
            if (value >= 10) return TwoDigits((uint)value) + tail;
            return Digit((uint)value) + tail;
        }

        private static string TwoDigits(uint value) => Digit(value / 10) + Digit(value % 10);

        private static string Digit(uint value)
        {
            if (value == 0) return "0";
            if (value == 1) return "1";
            if (value == 2) return "2";
            if (value == 3) return "3";
            if (value == 4) return "4";
            if (value == 5) return "5";
            if (value == 6) return "6";
            if (value == 7) return "7";
            if (value == 8) return "8";
            return "9";
        }
    }
}
