namespace Cilica.Fixtures.Execution
{
    public struct EmptyBoxValue { }
    public static class BoxedValueProbe
    {
        public static ValueObjectLeaf Copy(object boxed, int pressure)
        {
            var copy = (ValueObjectLeaf)boxed;
            copy.Marker = unchecked(copy.Marker + ValueObjectProbe.Pressure(pressure));
            return copy;
        }
        public static int Execute(int number, int mode)
        {
            var original = ValueObjectProbe.Make(number, 1);
            object first = original, second = original;
            if (first == second) return -100;
            if (mode == 1) first = null;
            if (mode == 2) first = default(EmptyBoxValue);
            var copy = Copy(first, 5);
            copy.Node.Number = unchecked(copy.Node.Number + 1);
            var returned = Copy(first, 9);
            if (copy.Node != original.Node || returned.Node != original.Node) return -200;
            return unchecked(copy.Marker + returned.Marker + ((ValueObjectLeaf)second).Marker + original.Node.Number);
        }
    }
}
