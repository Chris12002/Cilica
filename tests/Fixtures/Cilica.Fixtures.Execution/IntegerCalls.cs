namespace Cilica.Fixtures.Execution
{
    public static class IntegerCalls
    {
        public static int Difference(int a, int b) => unchecked(a - b);
        public static int Scale(int a, int b) => unchecked(Difference(a, b) * 3);
        public static int Diamond(int a, int b, int c) => Difference(Scale(a, b), Scale(b, c));

        public static int Ordered(int a, int b, int c)
        {
            int value = a;
            return unchecked(Difference(value, value = b) + value * c);
        }

        public static int CallLoop(int count, int a, int b)
        {
            int value = a;
            for (int index = 0; index < count; index++) value = Difference(value, b);
            return value;
        }

        public static int Recursive(int count, int a, int b) => count <= 0 ? a : Recursive(count - 1, unchecked(a + b), b);
        public static int Mutual(int count, int a, int b) => count <= 0 ? a : MutualOther(count - 1, unchecked(a + b), b);
        private static int MutualOther(int count, int a, int b) => count <= 0 ? a : Mutual(count - 1, unchecked(a - b), b);
    }
}
