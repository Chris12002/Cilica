// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from pinned String.IsNullOrWhiteSpace and Char.IsWhiteSpace.
namespace System
{
    public sealed partial class String
    {
        public static bool IsNullOrWhiteSpace(string value)
        {
            if (value == null) return true;
            for (int i = 0; i < value.Length; i++)
            {
                int c = value[i];
                // Unicode White_Space is entirely in the BMP. Unpaired surrogates,
                // zero-width space and BOM are deliberately not whitespace.
                bool white = (c >= 0x0009 && c <= 0x000d) || c == 0x0020
                    || c == 0x0085 || c == 0x00a0 || c == 0x1680
                    || (c >= 0x2000 && c <= 0x200a) || c == 0x2028
                    || c == 0x2029 || c == 0x202f || c == 0x205f || c == 0x3000;
                if (!white) return false;
            }
            return true;
        }
    }
}
