// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded String/out adaptation of pinned Boolean.TryParse; see library/provenance.json.
namespace Cilica.Runtime
{
    internal static class BooleanParsing
    {
        internal static bool TryParse(string text, out bool result)
        {
            result = false;
            if (text == null) return false;
            int start = 0, end = text.Length;
            while (start < end && (text[start] == 0 || UnicodeWhitespace.Contains(text[start]))) start++;
            while (end > start && (text[end - 1] == 0 || UnicodeWhitespace.Contains(text[end - 1]))) end--;
            int length = end - start;
            if (length == 4 && (text[start] | 32) == 't' && (text[start + 1] | 32) == 'r'
                && (text[start + 2] | 32) == 'u' && (text[start + 3] | 32) == 'e')
            {
                result = true;
                return true;
            }
            return length == 5 && (text[start] | 32) == 'f' && (text[start + 1] | 32) == 'a'
                && (text[start + 2] | 32) == 'l' && (text[start + 3] | 32) == 's' && (text[start + 4] | 32) == 'e';
        }
    }
}
