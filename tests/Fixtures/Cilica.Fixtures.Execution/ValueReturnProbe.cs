namespace Cilica.Fixtures.Execution
{
    public static class ValueReturnProbe
    {
        public static ValueLeaf Make(int number) { return new ValueLeaf { Number = number }; }
        public static ValueLeaf Change(ValueLeaf value, int divisor)
        {
            value.Number /= divisor;
            return value;
        }
        public static ValueLeaf Echo(ValueLeaf value) { return value; }
        public static ValueLeaf Forward(ValueLeaf value) { return Echo(value); }
        public static int Execute(int number, int divisor)
        {
            var saved = Forward(Change(Make(number), divisor));
            var other = Make(7);
            return unchecked(saved.Number + other.Number);
        }
    }
}
