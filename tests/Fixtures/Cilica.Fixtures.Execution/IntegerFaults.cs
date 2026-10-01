namespace Cilica.Fixtures.Execution
{
    public static class IntegerFaults
    {
        public static int Divide(int a, int b, int unused) => a / b;
        public static int UnsignedDivide(int a, int b, int unused) => unchecked((int)((uint)a / (uint)b));
        public static int TransitiveDivide(int a, int b, int c) => LevelOne(a, b, c);
        private static int LevelOne(int a, int b, int c) => LevelTwo(a, b, c);
        private static int LevelTwo(int a, int b, int c) => unchecked(Divide(a, b, c) + c);
        public static int FirstFault(int a, int b, int c) => unchecked(Divide(a, b, c) + Divide(a, c, b));
        public static int ReverseFirstFault(int a, int b, int c) => unchecked(Divide(a, c, b) + Divide(a, b, c));
        public static int LoopDivide(int count, int a, int b)
        {
            int result = a;
            for (int index = 0; index < count; index++) result = Divide(result, b, count);
            return result;
        }
        public static int ConditionalDivide(int condition, int a, int b) =>
            condition == 0 ? unchecked(a + b) : Divide(a, b, condition);
        public static int RecursiveDivide(int count, int a, int b) =>
            count <= 0 ? Divide(a, b, count) : RecursiveDivide(count - 1, a, b);
    }
}
