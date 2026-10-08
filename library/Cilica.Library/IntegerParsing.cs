// Cilica-owned parsers for selected invariant integer grammars.
namespace Cilica.Library
{
    public static class IntegerParsing
    {
        public static bool Int32Integer(string text, out int result) => Int32IntegerStatus(text, out result) == 0;
        public static int ParseInt32Integer(string text)
        {
            if (text == null) throw new System.ArgumentNullException("s");
            int status = Int32IntegerStatus(text, out int result);
            if (status == 0) return result;
            if (status == 2) throw new System.OverflowException("Value was either too large or too small for an Int32.");
            throw new System.FormatException(System.String.Concat("The input string '", text, "' was not in a correct format."));
        }
        // 0: success, 1: format, 2: overflow. An invalid suffix wins over overflow.
        private static int Int32IntegerStatus(string text, out int result)
        {
            result = 0;
            if (text == null) return 1;
            int index = 0, length = text.Length;
            while (index < length && White(text[index])) index++;
            if (index == length) return 1;
            bool negative = text[index] == '-';
            if (negative || text[index] == '+') index++;
            int firstDigit = index;
            uint magnitude = 0, limit = negative ? 2147483648u : 2147483647u;
            bool overflow = false;
            while (index < length)
            {
                uint digit = unchecked((uint)(text[index] - '0'));
                if (digit > 9) break;
                if (!overflow)
                {
                    if (magnitude > limit / 10 || magnitude == limit / 10 && digit > limit % 10) overflow = true;
                    else magnitude = magnitude * 10 + digit;
                }
                index++;
            }
            if (index == firstDigit) return 1;
            while (index < length && White(text[index])) index++;
            // The pinned integer parser accepts only NULs after trailing whitespace.
            while (index < length && text[index] == '\0') index++;
            if (index != length) return 1;
            if (overflow) return 2;
            if (negative) result = unchecked(-(int)magnitude);
            else result = unchecked((int)magnitude);
            return 0;
        }
        public static long ParseInt64Integer(string text)
        {
            if (text == null) throw new System.ArgumentNullException("s");
            int status = Int64IntegerStatus(text, out long result);
            if (status == 0) return result;
            if (status == 2) throw new System.OverflowException("Value was either too large or too small for an Int64.");
            throw new System.FormatException(System.String.Concat("The input string '", text, "' was not in a correct format."));
        }
        // 0: success, 1: format, 2: overflow. An invalid suffix wins over overflow.
        private static int Int64IntegerStatus(string text, out long result)
        {
            result = 0;
            if (text == null) return 1;
            int index = 0, length = text.Length;
            while (index < length && White(text[index])) index++;
            if (index == length) return 1;
            bool negative = text[index] == '-';
            if (negative || text[index] == '+') index++;
            int firstDigit = index;
            ulong magnitude = 0, limit = negative ? 9223372036854775808UL : 9223372036854775807UL;
            bool overflow = false;
            while (index < length)
            {
                uint digit = unchecked((uint)(text[index] - '0'));
                if (digit > 9) break;
                if (!overflow)
                {
                    if (magnitude > limit / 10 || magnitude == limit / 10 && digit > limit % 10) overflow = true;
                    else magnitude = magnitude * 10 + digit;
                }
                index++;
            }
            if (index == firstDigit) return 1;
            while (index < length && White(text[index])) index++;
            // The pinned integer parser accepts only NULs after trailing whitespace.
            while (index < length && text[index] == '\0') index++;
            if (index != length) return 1;
            if (overflow) return 2;
            if (negative) result = unchecked(-(long)magnitude);
            else result = unchecked((long)magnitude);
            return 0;
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
