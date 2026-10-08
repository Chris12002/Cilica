using System;
namespace Cilica.Fixtures.Execution
{
    // Independently authored semantic probes; no candidate source is compiled here.
    public static class CandidateStringProbe
    {
        public static int NullOrEmpty(int input, int mode)
        {
            string value = null;
            if (input == 0) value = "";
            if (input == 1) value = " ";
            if (input == -1) value = "\0";
            if (input == 127) value = "\ud800";
            if (input == 32768) value = "\udfff";
            if (input == int.MaxValue) value = "abc";
            if (mode == 1) value = string.Concat(value, "");
            if (mode == 2) value = string.Concat(value, "\0");
            return string.IsNullOrEmpty(value) ? 1 : 0;
        }

        public static int Evaluation(int input, int mode)
        {
            int count = input;
            try
            {
                bool empty = string.IsNullOrEmpty(Argument(ref count, mode));
                return unchecked(count * 2 + (empty ? 1 : 0));
            }
            catch (InvalidOperationException)
            {
                return unchecked(count * 3);
            }
        }

        public static string Argument(ref int count, int mode)
        {
            count = unchecked(count + 1);
            if (mode == 1) throw new InvalidOperationException("argument");
            if (mode == 2) return "";
            return null;
        }

        // A two-row dynamic program, intentionally independent of Fastenshtein's
        // in-place algorithm. Exercises the same bounded IL/library requirements.
        public static int EditDistance(int first, int second)
        {
            string left = Text(first), right = Text(second);
            int[] previous = new int[right.Length + 1];
            int[] current = new int[right.Length + 1];
            for (int j = 0; j <= right.Length; j++) previous[j] = j;
            for (int i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= right.Length; j++)
                {
                    int replace = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                    int remove = previous[j] + 1, insert = current[j - 1] + 1;
                    int best = remove < insert ? remove : insert;
                    current[j] = replace < best ? replace : best;
                }
                int[] swap = previous; previous = current; current = swap;
            }
            return previous[right.Length];
        }

        public static string Text(int index)
        {
            if (index == 0) return "";
            if (index == 1) return "kitten";
            if (index == 2) return "sitting";
            if (index == 3) return "\0a\ud800";
            if (index == 4) return "\0b\udfff";
            if (index == 5) return "\ud83d\ude04";
            if (index == 6) return "\ud83d\ude13";
            return "ababa";
        }
    }
}
