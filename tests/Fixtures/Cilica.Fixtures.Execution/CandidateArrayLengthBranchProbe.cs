namespace Cilica.Fixtures.Execution
{
    // Independent reduction of an array-empty check; no upstream source retained.
    public static class CandidateArrayLengthBranchProbe
    {
        public static int EmptyPair(int first, int second)
        {
            var left = new int[first & 3];
            var right = new int[second & 3];
            if (left.Length == 0 && right.Length == 0) return 1;
            return 0;
        }
    }
}
