namespace Cilica.Fixtures.Execution
{
    public static class IntegerRemainder
    {
        public static int SignedRemainder(int a, int b, int unused) => a % b;
        public static int CheckedRemainder(int a, int b, int unused) => checked(a % b);
        public static int UnsignedRemainder(int a, int b, int unused) => unchecked((int)((uint)a % (uint)b));
        public static int TransitiveRemainder(int a, int b, int c) => LevelOne(a, b, c);
        private static int LevelOne(int a, int b, int c) => unchecked(SignedRemainder(a, b, c) + c);
        public static int RemainderLoop(int count, int a, int b)
        {
            int result = a;
            for (int index = 0; index < count; index++) result = SignedRemainder(result, b, count);
            return result;
        }
        public static int RecursiveRemainder(int count, int a, int b) =>
            count <= 0 ? SignedRemainder(a, b, count) : RecursiveRemainder(count - 1, a, b);
        public static int ConditionalRemainder(int condition, int a, int b) =>
            condition == 0 ? unchecked(a + b) : SignedRemainder(a, b, condition);
        public static int RemainderBeforeOverflow(int a, int b, int c) =>
            unchecked(SignedRemainder(a, c, b) + IntegerChecked.Add(a, b, c));
        public static int OverflowBeforeRemainder(int a, int b, int c) =>
            unchecked(IntegerChecked.Add(a, b, c) + SignedRemainder(a, c, b));
        public static int CompoundRemainder(int a, int b, int c) => (a % b) % c;
        public static int CompoundRemainderUnsigned(int a, int b, int c) => unchecked((int)(((uint)a % (uint)b) % (uint)c));
    }
}
