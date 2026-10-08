// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from pinned TextInfo.cs and pal_casing.c; see ../provenance.json.
namespace System
{
    public sealed partial class String
    {
        public string ToLowerInvariant()
        {
            int length = Length;
            if (length == 0) return Empty;
            bool copy = false;
            for (int i = 0; i < length; ++i)
            {
                int unit = this[i];
                // The pinned TextInfo path allocates for any non-ASCII input,
                // even when no characters change. Unchanged ASCII keeps identity.
                if (unit > 127 || (unit >= 'A' && unit <= 'Z')) { copy = true; break; }
            }
            if (!copy) return this;
            var units = new int[length];
            for (int i = 0; i < length; ++i)
            {
                int scalar = this[i];
                if (scalar >= 0xd800 && scalar <= 0xdbff && i + 1 < length)
                {
                    int low = this[i + 1];
                    if (low >= 0xdc00 && low <= 0xdfff)
                    {
                        scalar = Cilica.Runtime.UnicodeSimpleLower.Map(0x10000 + ((scalar - 0xd800) << 10) + low - 0xdc00);
                        // The pinned simple mappings preserve UTF-16 width.
                        units[i] = 0xd800 + ((scalar - 0x10000) >> 10);
                        units[++i] = 0xdc00 + ((scalar - 0x10000) & 1023);
                        continue;
                    }
                }
                units[i] = Cilica.Runtime.UnicodeSimpleLower.Map(scalar);
            }
            return Cilica.Runtime.StringConstruction.FromCodeUnits(units);
        }
        public string ToUpperInvariant()
        {
            int length = Length;
            if (length == 0) return Empty;
            bool copy = false;
            for (int i = 0; i < length; ++i)
            {
                int unit = this[i];
                // The pinned TextInfo path allocates for any non-ASCII input,
                // even when no characters change. Unchanged ASCII keeps identity.
                if (unit > 127 || (unit >= 'a' && unit <= 'z')) { copy = true; break; }
            }
            if (!copy) return this;
            var units = new int[length];
            for (int i = 0; i < length; ++i)
            {
                int scalar = this[i];
                if (scalar >= 0xd800 && scalar <= 0xdbff && i + 1 < length)
                {
                    int low = this[i + 1];
                    if (low >= 0xdc00 && low <= 0xdfff)
                    {
                        scalar = UpperInvariantScalar(0x10000 + ((scalar - 0xd800) << 10) + low - 0xdc00);
                        // The pinned simple mappings preserve UTF-16 width.
                        units[i] = 0xd800 + ((scalar - 0x10000) >> 10);
                        units[++i] = 0xdc00 + ((scalar - 0x10000) & 1023);
                        continue;
                    }
                }
                units[i] = UpperInvariantScalar(scalar);
            }
            return Cilica.Runtime.StringConstruction.FromCodeUnits(units);
        }
        // Ordinal casing deliberately leaves long s unchanged; invariant simple
        // uppercase maps it to S. Both contracts leave dotless i unchanged.
        private static int UpperInvariantScalar(int scalar) => scalar == 0x017f
            ? 0x0053 : Cilica.Runtime.UnicodeOrdinalUpper.Map(scalar);
    }
}
