namespace Cilica.Fixtures.Execution
{
    public static class ValueCopyProbe
    {
        public static int Read(ValueLeaf value) { return value.Number; }

        public static int Copy(int number)
        {
            var original = new ValueLeaf { Number = number };
            var copy = original;
            original.Number = 7;
            return unchecked(Read(copy) + Read(original));
        }

        public static int Swap(int number)
        {
            var first = new ValueLeaf { Number = number };
            var second = new ValueLeaf { Number = 7 };
            for (int i = 0; i < 3; ++i)
            {
                var temporary = first;
                first = second;
                second = temporary;
            }
            return unchecked(Read(first) * 10 + Read(second));
        }

        public static int Nested(int number)
        {
            var pair = new ValuePair { Left = new ValueLeaf { Number = number } };
            var copy = pair;
            pair.Left.Number = 7;
            return unchecked(copy.Left.Number + pair.Left.Number);
        }
    }
}
