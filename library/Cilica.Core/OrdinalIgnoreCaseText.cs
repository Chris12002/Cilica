// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from pinned ordinal comparison; see ../provenance.json.
namespace Cilica.Runtime
{
    internal static class OrdinalIgnoreCaseText
    {
        // A valid pair sorts above every unpaired UTF-16 unit, as in pinned ICU
        // ordinal comparison. Lone surrogates retain their original unit value.
        private static int Scalar(string text, int index)
        {
            int first = text[index];
            if (first >= 0xd800 && first <= 0xdbff && index + 1 < text.Length)
            {
                int second = text[index + 1];
                if (second >= 0xdc00 && second <= 0xdfff)
                    return 0x10000 + ((first - 0xd800) << 10) + second - 0xdc00;
            }
            return first;
        }
        internal static bool Equal(string first, string second)
        {
            if ((object)first == (object)second) return true;
            if (first == null || second == null || first.Length != second.Length) return false;
            return Compare(first, second) == 0;
        }
        internal static int Compare(string first, string second)
        {
            if ((object)first == (object)second) return 0;
            if (first == null) return -1;
            if (second == null) return 1;
            int length = first.Length < second.Length ? first.Length : second.Length;
            int index = 0;
            while (index < length)
            {
                int a = Scalar(first, index), b = Scalar(second, index);
                if (a > 0xffff && b <= 0xffff) return 1;
                if (b > 0xffff && a <= 0xffff) return -1;
                int difference = UnicodeOrdinalUpper.Map(a) - UnicodeOrdinalUpper.Map(b);
                if (difference != 0) return difference;
                index += a > 0xffff ? 2 : 1;
            }
            return first.Length - second.Length;
        }
        internal static int Hash(string text)
        {
            // Deterministic private hash over the same canonical UTF-16 sequence
            // as Equal. Numeric equality with .NET's randomized hash is not an API.
            int hash = 5381;
            for (int index = 0; index < text.Length; index++)
            {
                int scalar = UnicodeOrdinalUpper.Map(Scalar(text, index));
                if (scalar > 0xffff)
                {
                    hash = unchecked(hash * 33 ^ (0xd800 + ((scalar - 0x10000) >> 10)));
                    hash = unchecked(hash * 33 ^ (0xdc00 + ((scalar - 0x10000) & 1023)));
                    index++;
                }
                else hash = unchecked(hash * 33 ^ scalar);
            }
            return hash;
        }
    }
}
