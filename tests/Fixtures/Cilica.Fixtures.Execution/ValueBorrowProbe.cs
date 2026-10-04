namespace Cilica.Fixtures.Execution
{
    public static class ValueBorrowProbe
    {
        public static int Execute(int number)
        {
            ValuePair pair = default;
            pair.Left.Number = number;
            pair.Right.Number = 7;
            Swap(ref pair.Left, ref pair.Right);
            var saved = pair;
            Swap(ref pair.Left, ref pair.Left);
            pair = saved;
            int changed = MutateCopy(pair);
            return unchecked(Read(in pair) * 10000 + changed * 100 + pair.Right.Number);
        }

        public static void Swap(ref ValueLeaf left, ref ValueLeaf right)
        {
            var saved = left;
            left = right;
            right = saved;
        }

        public static int MutateCopy(ValuePair value)
        {
            value.Left.Number = 99;
            return value.Left.Number;
        }

        public static int Read(in ValuePair value) { return value.Left.Number; }
    }
}
