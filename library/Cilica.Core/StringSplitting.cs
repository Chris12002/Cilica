// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded character/string-array Split adaptation; see ../provenance.json.
namespace System
{
    [Flags]
    public enum StringSplitOptions { None = 0, RemoveEmptyEntries = 1, TrimEntries = 2 }
    public sealed partial class String
    {
        public string[] Split(char separator, StringSplitOptions options)
        {
            int flags = (int)options;
            if ((flags & ~3) != 0) throw new ArgumentException("Value of flags is invalid.", "options");
            int length = Length, start = 0, count = 0, separators = 0;
            for (int end = 0; end < length; ++end)
            {
                if (this[end] != separator) continue;
                ++separators;
                if (SplitEntryPresent(start, end, flags)) ++count;
                start = end + 1;
            }
            if (SplitEntryPresent(start, length, flags)) ++count;
            // The pinned no-separator shortcut retains the input string and
            // canonical empty-array identity. Postprocessed all-empty splits
            // instead produce their own zero-length array.
            if (separators == 0)
            {
                if (count == 0) return Array.Empty<string>();
                return new string[] { SplitEntry(0, length, flags) };
            }
            var result = new string[count];
            start = 0; int output = 0;
            for (int end = 0; end < length; ++end)
            {
                if (this[end] != separator) continue;
                if (SplitEntryPresent(start, end, flags)) result[output++] = SplitEntry(start, end, flags);
                start = end + 1;
            }
            if (SplitEntryPresent(start, length, flags)) result[output] = SplitEntry(start, length, flags);
            return result;
        }
        public string[] Split(string[] separator, StringSplitOptions options)
        {
            int flags = (int)options;
            if ((flags & ~3) != 0) throw new ArgumentException("Value of flags is invalid.", "options");
            int length = Length, start = 0, count = 0, matches = 0;
            for (int end = 0; end < length;)
            {
                int width = MatchStringSeparator(separator, end);
                if (width == 0) { ++end; continue; }
                ++matches;
                if (SplitEntryPresent(start, end, flags)) ++count;
                start = end + width;
                end = start;
            }
            if (SplitEntryPresent(start, length, flags)) ++count;
            if (matches == 0)
            {
                if (count == 0) return Array.Empty<string>();
                return new string[] { SplitEntry(0, length, flags) };
            }
            // Rescan within the single-threaded execution profile. There are no
            // callbacks between scans; neither text nor separator entries change.
            var result = new string[count];
            start = 0; int output = 0;
            for (int end = 0; end < length;)
            {
                int width = MatchStringSeparator(separator, end);
                if (width == 0) { ++end; continue; }
                if (SplitEntryPresent(start, end, flags)) result[output++] = SplitEntry(start, end, flags);
                start = end + width;
                end = start;
            }
            if (SplitEntryPresent(start, length, flags)) result[output] = SplitEntry(start, length, flags);
            return result;
        }

        private int MatchStringSeparator(string[] separator, int index)
        {
            if (separator == null)
                return Cilica.Runtime.UnicodeWhitespace.Contains(this[index]) ? 1 : 0;
            int separatorCount = separator.Length;
            if (separatorCount == 0)
                return Cilica.Runtime.UnicodeWhitespace.Contains(this[index]) ? 1 : 0;
            for (int candidate = 0; candidate < separatorCount; ++candidate)
            {
                string value = separator[candidate];
                if (IsNullOrEmpty(value) || value.Length > Length - index) continue;
                int matched = 0;
                while (matched < value.Length && this[index + matched] == value[matched]) ++matched;
                // First matching separator wins, including shorter prefixes.
                if (matched == value.Length) return matched;
            }
            return 0;
        }

        private bool SplitEntryPresent(int start, int end, int flags)
        {
            if ((flags & 1) == 0) return true;
            if ((flags & 2) != 0)
                while (start < end && Cilica.Runtime.UnicodeWhitespace.Contains(this[start])) ++start;
            return start < end;
        }
        private string SplitEntry(int start, int end, int flags)
        {
            if ((flags & 2) != 0)
            {
                while (start < end && Cilica.Runtime.UnicodeWhitespace.Contains(this[start])) ++start;
                while (start < end && Cilica.Runtime.UnicodeWhitespace.Contains(this[end - 1])) --end;
            }
            return Substring(start, end - start);
        }
    }
}
