namespace Cilica.Fixtures.Execution
{
    public static class IntegerChecked
    {
        public static int Add(int a, int b, int unused) => checked(a + b);
        public static int Subtract(int a, int b, int unused) => checked(a - b);
        public static int Multiply(int a, int b, int unused) => checked(a * b);
        // Only the arithmetic is checked. Casts preserve incoming/outgoing bits;
        // checked conversions are a separate unsupported capability.
        public static int AddUnsigned(int a, int b, int unused) => unchecked((int)checked(unchecked((uint)a) + unchecked((uint)b)));
        public static int SubtractUnsigned(int a, int b, int unused) => unchecked((int)checked(unchecked((uint)a) - unchecked((uint)b)));
        public static int MultiplyUnsigned(int a, int b, int unused) => unchecked((int)checked(unchecked((uint)a) * unchecked((uint)b)));
        public static int Compound(int a, int b, int c) => checked((a - b) * c);
        public static int CompoundUnsigned(int a, int b, int c) =>
            unchecked((int)checked(checked(unchecked((uint)a) + unchecked((uint)b)) * unchecked((uint)c)));
        public static int Transitive(int a, int b, int c) => LevelOne(a, b, c);
        private static int LevelOne(int a, int b, int c) => unchecked(Add(a, b, c) + c);
        public static int Loop(int count, int a, int b)
        {
            int result = a;
            for (int index = 0; index < count; index++) result = Add(result, b, count);
            return result;
        }
        public static int Recursive(int count, int a, int b) =>
            count <= 0 ? Add(a, b, count) : Recursive(count - 1, a, b);
        public static int Conditional(int condition, int a, int b) =>
            condition == 0 ? unchecked(a + b) : Add(a, b, condition);
        public static int ArithmeticBeforeDivision(int a, int b, int c) =>
            unchecked(Add(a, b, c) + IntegerFaults.Divide(a, c, b));
        public static int DivisionBeforeArithmetic(int a, int b, int c) =>
            unchecked(IntegerFaults.Divide(a, c, b) + Add(a, b, c));
    }
}
