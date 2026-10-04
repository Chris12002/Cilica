namespace Cilica.Fixtures.Execution
{
    public struct ValueArrayLeaf { public int[] Data; public int Marker; }
    public struct ValueArrayPair { public ValueArrayLeaf Left; public ValueArrayLeaf Right; }

    public static class ValueArrayProbe
    {
        public static ValueArrayLeaf Make(int number, int length)
        {
            ValueArrayLeaf result = default;
            result.Data = new int[length];
            result.Data[0] = number;
            result.Marker = number;
            return result;
        }
        public static int Pressure(int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                var scratch = new int[4];
                scratch[0] = i;
                sum = unchecked(sum + scratch[0]);
            }
            return sum;
        }
        public static ValueArrayLeaf AllocateEcho(ValueArrayLeaf value, int count)
        {
            value.Marker = unchecked(value.Marker + Pressure(count));
            return value;
        }
        public static void Replace(ref ValueArrayLeaf value, int number)
        {
            value = Make(number, 2);
            value.Marker = unchecked(value.Marker + Pressure(6));
        }
        public static int Execute(int number, int length)
        {
            var saved = Make(number, length);
            ValueArrayPair pair = default;
            pair.Left = saved;
            pair.Right = AllocateEcho(saved, 4);
            if (pair.Left.Data != pair.Right.Data) return -100;
            Replace(ref pair.Left, unchecked(number + 1));
            if (saved.Data == pair.Left.Data) return -200;
            var returned = AllocateEcho(pair.Right, 8);
            return unchecked(saved.Data[0] + returned.Data[0] + pair.Left.Data[0] + returned.Data.Length + returned.Marker);
        }
    }
}
