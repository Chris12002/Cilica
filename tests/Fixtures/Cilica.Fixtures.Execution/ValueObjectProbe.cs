namespace Cilica.Fixtures.Execution
{
    public class ValueObjectPayload
    {
        public int Number;
        public ValueObjectPayload(int number) { Number = number; }
    }
    public class ValueObjectNode
    {
        public int Number;
        public readonly int Initial;
        public ValueObjectNode Next;
        public readonly ValueObjectPayload Payload;
        public ValueObjectNode(int number, int divisor, ValueObjectNode next)
        {
            Next = next;
            Number = number / divisor;
            Initial = Number;
            Payload = new ValueObjectPayload(number);
        }
    }
    public class ValueObjectChild : ValueObjectNode
    {
        public ValueObjectPayload Extra;
        public ValueObjectChild(int number, int divisor, ValueObjectNode next) : base(number, divisor, next)
        { Extra = new ValueObjectPayload(unchecked(number + 1)); }
    }
    public struct ValueObjectLeaf { public ValueObjectNode Node; public int Marker; }
    public struct ValueObjectPair { public ValueObjectLeaf Left; public ValueObjectLeaf Right; }

    public static class ValueObjectProbe
    {
        public static ValueObjectLeaf Make(int number, int divisor)
        {
            ValueObjectLeaf result = default;
            result.Node = new ValueObjectChild(number, divisor, new ValueObjectChild(7, 1, null));
            result.Marker = number;
            return result;
        }
        public static int Pressure(int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                var scratch = new ValueObjectPayload(i);
                sum = unchecked(sum + scratch.Number);
            }
            return sum;
        }
        public static ValueObjectLeaf AllocateEcho(ValueObjectLeaf value, int count)
        {
            value.Marker = unchecked(value.Marker + Pressure(count));
            return value;
        }
        public static void Replace(ref ValueObjectLeaf value, int number)
        {
            value = Make(number, 1);
            value.Marker = unchecked(value.Marker + Pressure(6));
        }
        public static void Bump(ref int value)
        {
            Pressure(3);
            value = unchecked(value + 1);
        }
        public static int Execute(int number, int divisor)
        {
            var saved = Make(number, divisor);
            ValueObjectPair pair = default;
            pair.Left = saved;
            pair.Right = AllocateEcho(saved, 4);
            if (pair.Left.Node != pair.Right.Node) return -100;
            Replace(ref pair.Left, unchecked(number + 1));
            if (saved.Node == pair.Left.Node) return -200;
            Bump(ref pair.Left.Node.Number);
            var returned = AllocateEcho(pair.Right, 8);
            return unchecked(saved.Node.Number + returned.Node.Next.Initial + pair.Left.Node.Number
                + returned.Node.Payload.Number + returned.Marker);
        }
    }
}
