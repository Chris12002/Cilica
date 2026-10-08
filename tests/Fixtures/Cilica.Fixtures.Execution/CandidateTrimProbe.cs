using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    // Independent reduction of DiffPlex BuildPieceHashes(ignoreWhitespace).
    public static class CandidateTrimProbe
    {
        public static int Units(int first, int unused)
        {
            int mask = 0;
            for (int i = 0; i < 32; ++i)
            {
                char unit = (char)(first + i);
                string source = new StringBuilder().Append(unit).Append('x').Append(unit).ToString();
                string trimmed = source.Trim();
                if (trimmed.Length == 1 && trimmed[0] == 'x' && (object)trimmed != (object)source)
                    mask |= 1 << i;
                else if ((object)trimmed != (object)source)
                    throw new InvalidOperationException("changed non-whitespace or wrong trimmed payload");
            }
            return mask;
        }

        public static int Text(int input, int index)
        {
            string source = " \tA\0\ud800\udfff B\u3000\r\n";
            if (input == 0) source = "";
            if (input == 1) source = " \t\u0085\u00a0\u1680\u2000\u200a\u2028\u2029\u202f\u205f\u3000";
            if (input == 2) source = " unchanged ";
            if (input == 3) source = "\u200b\ufeff";
            if (input == 4) source = "x \t y";
            if (input == 5) source = "   x";
            if (input == 6) source = "x   ";
            string result = source.Trim();
            // Force a later allocation while the result remains live.
            string another = new StringBuilder().Append('!').ToString();
            if (index == -1) return result.Length;
            if (index == -2) return (object)result == (object)source ? 1 : 0;
            if (index == -3) return (object)result == (object)string.Empty ? 1 : 0;
            return index >= 0 && index < result.Length ? result[index] : another[0];
        }

        public static int NullReceiver(int input, int mode)
        {
            int count = input;
            try
            {
                string result = Receiver(ref count, mode).Trim();
                return unchecked(count * 31 + result.Length);
            }
            catch (NullReferenceException) { return unchecked(count * 17); }
            catch (InvalidOperationException) { return unchecked(count * 19); }
        }

        public static string Receiver(ref int count, int mode)
        {
            count = unchecked(count + 1);
            if (mode == 0) return null;
            if (mode == 1) throw new InvalidOperationException("receiver");
            return "  x  ";
        }

        public static int UnsupportedCharacters(int unused, int other) => "xx".Trim('x').Length;
        public static int UnsupportedStart(int unused, int other) => " x ".TrimStart().Length;
    }
}
