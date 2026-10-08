// Cilica-owned parsers for selected invariant integer grammars.
namespace Cilica.Library
{
    public static class IntegerParsing
    {
        public static bool Int32Integer(string text, out int result)
        {
            result = 0;
            if (text == null) return false;
            int index = 0, length = text.Length;
            while (index < length && White(text[index])) index++;
            if (index == length) return false;
            bool negative = text[index] == '-';
            if (negative || text[index] == '+') index++;
            int firstDigit = index;
            uint magnitude = 0, limit = negative ? 2147483648u : 2147483647u;
            while (index < length)
            {
                uint digit = unchecked((uint)(text[index] - '0'));
                if (digit > 9) break;
                if (magnitude > limit / 10 || magnitude == limit / 10 && digit > limit % 10) return false;
                magnitude = magnitude * 10 + digit;
                index++;
            }
            if (index == firstDigit) return false;
            while (index < length && White(text[index])) index++;
            // The pinned integer parser accepts only NULs after trailing whitespace.
            while (index < length && text[index] == '\0') index++;
            if (index != length) return false;
            if (negative) result = unchecked(-(int)magnitude);
            else result = unchecked((int)magnitude);
            return true;
        }
        public static bool Int64None(string text, out long result)
        {
            result = 0;
            if (text == null) return false;
            int index = 0, length = text.Length;
            ulong magnitude = 0;
            const ulong limit = 9223372036854775807UL;
            while (index < length)
            {
                uint digit = unchecked((uint)(text[index] - '0'));
                if (digit > 9) break;
                if (magnitude > limit / 10 || magnitude == limit / 10 && digit > limit % 10) return false;
                magnitude = magnitude * 10 + digit;
                ++index;
            }
            if (index == 0) return false;
            // NumberStyles.None still permits the pinned parser's trailing NULs.
            while (index < length && text[index] == '\0') ++index;
            if (index != length) return false;
            result = unchecked((long)magnitude);
            return true;
        }
        private static bool White(char value) => value == ' ' || value >= '\t' && value <= '\r';
    }
}
