// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Guid.cs constructor parsing; see provenance.
namespace Cilica.Runtime
{
    internal static class GuidParsing
    {
        internal static int[] Parse(string text)
        {
            if (text == null) throw new System.ArgumentNullException("g");
            int start = 0, end = text.Length;
            while (start < end && UnicodeWhitespace.Contains(text[start])) start++;
            while (end > start && UnicodeWhitespace.Contains(text[end - 1])) end--;
            int length = end - start;
            if (length < 32) throw Invalid(9);
            int first = text[start];
            if (first == '(')
            {
                if (length != 38 || text[end - 1] != ')') throw Invalid(8);
                return Standard(text, start + 1, 36, true);
            }
            if (first == '{')
            {
                if (text[start + 9] != '-') return Expanded(Compact(text, start, length));
                if (length != 38 || text[end - 1] != '}') throw Invalid(8);
                return Standard(text, start + 1, 36, true);
            }
            return Standard(text, start, length, text[start + 8] == '-');
        }

        private static int[] Standard(string text, int start, int length, bool dashed)
        {
            if (length != (dashed ? 36 : 32)) throw Invalid(8);
            if (dashed && (text[start + 8] != '-' || text[start + 13] != '-' || text[start + 18] != '-' || text[start + 23] != '-'))
                throw Invalid(4);
            // D permits the pinned legacy +/0x prefixes in the first five components.
            // Its last eight digits, and every N component, remain strict hexadecimal.
            uint a = Fixed(text, start, 8, dashed);
            uint b = Fixed(text, start + (dashed ? 9 : 8), 4, dashed);
            uint c = Fixed(text, start + (dashed ? 14 : 12), 4, dashed);
            uint de = Fixed(text, start + (dashed ? 19 : 16), 4, dashed);
            uint fg = Fixed(text, start + (dashed ? 24 : 20), 4, dashed);
            uint hijk = Fixed(text, start + (dashed ? 28 : 24), 8, false);
            var words = new int[4];
            words[0] = unchecked((int)a);
            words[1] = unchecked((int)(b | (c << 16)));
            words[2] = unchecked((int)((de >> 8) | ((de & 255) << 8) | ((fg >> 8) << 16) | ((fg & 255) << 24)));
            words[3] = unchecked((int)((hijk >> 24) | ((hijk >> 8) & 0xff00) | ((hijk << 8) & 0xff0000) | (hijk << 24)));
            return words;
        }

        private static uint Fixed(string text, int start, int length, bool compatible)
        {
            int overflow = 0;
            uint value;
            if (!Hex(text, start, length, compatible, out value, ref overflow)) throw Invalid(7);
            return value;
        }

        private static bool Hex(string text, int start, int length, bool compatible, out uint value, ref int overflow)
        {
            int end = start + length;
            if (compatible && start < end)
            {
                if (text[start] == '+') start++;
                if (start < end - 1 && text[start] == '0' && (text[start + 1] | 0x20) == 'x') start += 2;
            }
            while (start < end && text[start] == '0') start++;
            int digits = 0;
            uint result = 0;
            for (int i = start; i < end; i++)
            {
                int digit = Digit(text[i]);
                if (digit < 0)
                {
                    if (digits > 8) overflow = 1;
                    value = 0;
                    return false;
                }
                result = unchecked(result * 16 + (uint)digit);
                digits++;
            }
            if (digits > 8) overflow = 1;
            value = result;
            return true;
        }

        private static int Digit(int c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static string Compact(string text, int start, int length)
        {
            int end = start + length, count = 0;
            for (int i = start; i < end; i++) if (!UnicodeWhitespace.Contains(text[i])) count++;
            if (count == length) return text.Substring(start, length);
            var characters = new char[count];
            int output = 0;
            for (int i = start; i < end; i++)
                if (!UnicodeWhitespace.Contains(text[i])) characters[output++] = text[i];
            return new string(characters);
        }

        private static int[] Expanded(string text)
        {
            if (text.Length == 0 || text[0] != '{') throw Invalid(2);
            int index = 1;
            uint a = Component(text, ref index, ',', false);
            uint b = Component(text, ref index, ',', false);
            uint c = Component(text, ref index, ',', false);
            if (index >= text.Length || text[index] != '{') throw Invalid(2);
            index++;
            var words = new int[4];
            words[0] = unchecked((int)a);
            words[1] = unchecked((int)((b & 65535) | ((c & 65535) << 16)));
            for (int i = 0; i < 8; i++)
            {
                char delimiter = i == 7 ? '}' : ',';
                uint octet = Component(text, ref index, delimiter, true);
                int word = i < 4 ? 2 : 3;
                words[word] = words[word] | unchecked((int)(octet << ((i & 3) * 8)));
            }
            if (index >= text.Length || text[index] != '}') throw Invalid(5);
            if (index != text.Length - 1) throw Invalid(0);
            return words;
        }

        private static uint Component(string text, ref int index, char delimiter, bool octet)
        {
            if (index >= text.Length - 1 || text[index] != '0' || (text[index + 1] | 0x20) != 'x') throw Invalid(6);
            int start = index + 2, end = start;
            while (end < text.Length && text[end] != delimiter) end++;
            if (end == text.Length || end == start) throw Invalid(delimiter == '}' ? 1 : 3);
            int overflow = 0;
            uint value;
            bool success = Hex(text, start, end - start, true, out value, ref overflow);
            if (overflow != 0) throw new System.OverflowException("Value was either too large or too small for a UInt32.");
            if (octet && value > 255) throw Invalid(9);
            if (!success) throw Invalid(7);
            index = end + 1;
            return value;
        }

        private static System.FormatException Invalid(int kind)
        {
            string message;
            switch (kind)
            {
                case 0: message = "Additional non-parsable characters are at the end of the string."; break;
                case 1: message = "Could not find a brace, or the length between the previous token and the brace was zero (i.e., '0x,'etc.)."; break;
                case 2: message = "Expected {0xdddddddd, etc}."; break;
                case 3: message = "Could not find a comma, or the length between the previous token and the comma was zero (i.e., '0x,'etc.)."; break;
                case 4: message = "Dashes are in the wrong position for GUID parsing."; break;
                case 5: message = "Could not find the ending brace."; break;
                case 6: message = "Expected 0x prefix."; break;
                case 7: message = "Guid string should only contain hexadecimal characters."; break;
                case 8: message = "Guid should contain 32 digits with 4 dashes (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)."; break;
                default: message = "Unrecognized Guid format."; break;
            }
            return new System.FormatException(message);
        }
    }
}
