namespace Cilica.Fixtures.Execution
{
    public static class IntegerControlFlow
    {
        public static int Select(int a, int b, int unused) => a < b ? a : b;
        public static int Choose(int condition, int a, int b) => condition != 0 ? a : b;
        public static int UnsignedLess(int a, int b, int unused) => (uint)a < (uint)b ? 1 : 0;
        public static int Equal(int a, int b, int unused) => a == b ? 1 : 0;
        public static int Greater(int a, int b, int unused) => a > b ? 1 : 0;
        public static int UnsignedGreater(int a, int b, int unused) => (uint)a > (uint)b ? 1 : 0;
        public static int StackJoin(int condition, int a, int b) => unchecked((condition != 0 ? a : b) + (a < b ? b : a));

        public static int Sum(int count, int initial, int step)
        {
            int value = initial;
            for (int index = 0; index < count; index++) value = unchecked(value + step);
            return value;
        }

        public static int Nested(int count, int initial, int step)
        {
            int value = initial;
            for (int index = 0; index < count; index++)
                if (index < 3) value = unchecked(value + step);
                else value = unchecked(value - step);
            return value;
        }
    }
}
