using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    // Independent reductions of reachable DiffPlex requirements still outside
    // this batch. They are admission gaps, not observed semantic mismatches.
    public static class CandidateGapProbe
    {
        public static int BooleanVector(int input, int mode)
        {
            var values = new bool[2]; values[0] = input != 0;
            return values[mode & 1] ? 1 : 0;
        }
        public static int CharacterText(int input, int mode) => ((char)input).ToString().Length;
        public static int StringSeparators(int input, int mode) => "a\r\nb".Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length;
        public static int IgnoreCaseComparer(int input, int mode)
        {
            var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            values.Add("A", input); return values["a"];
        }
    }
}
