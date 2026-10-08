namespace Cilica.Fixtures.Execution
{
    // Independent scalar receiver reductions, not retained candidate source.
    public static class CandidateScalarArrayReceiverProbe
    {
        public static int EqualsElement(int first, int second)
        {
            var values = new int[] { first, second };
            return values[0].Equals(values[1]) ? 1 : 0;
        }
        public static int EqualsAfterArgumentMutation(int first, int second)
        {
            var values = new int[] { first };
            return values[0].Equals(Replace(values, second)) ? 1 : 0;
        }
        private static int Replace(int[] values, int value)
        {
            var temporary = new int[17];
            temporary[0] = value;
            values[0] = temporary[0];
            return temporary[0];
        }
    }
}
