namespace Cilica.Fixtures.Execution
{
    public class GenericNode<T>
    {
        public T Item;
        public readonly T Initial;
        public GenericNode<T> Next;
        public GenericNode(T item, GenericNode<T> next) { Item = item; Initial = item; Next = next; }
    }
    public class GenericChild<T> : GenericNode<T>
    {
        public T Extra;
        public GenericChild(T item, GenericNode<T> next) : base(item, next)
        {
            Extra = item;
            ValueObjectProbe.Pressure(3);
        }
    }
    public static class GenericReferenceProbe
    {
        public static int Execute(int number, int divisor)
        {
            var left = new GenericChild<int>(number, null);
            var right = new GenericChild<ValueObjectPayload>(new ValueObjectPayload(number), null);
            left.Next = left;
            right.Next = right;
            var pair = GenericPair<GenericNode<int>, GenericNode<ValueObjectPayload>>.Create(left, right);
            left = null;
            right = null;
            var boxed = ClosedGenericProbe.RoundTrip(pair);
            pair = default;
            var returned = ClosedGenericProbe.Relay(boxed);
            ValueObjectProbe.Bump(ref returned.First.Item);
            return unchecked(returned.First.Item / divisor + returned.Second.Item.Number + returned.Second.Next.Initial.Number);
        }
        public static int UnsupportedInlineField(int number)
        {
            var value = new GenericNode<ValueObjectLeaf>(default, null);
            return value.Item.Marker + number;
        }
    }
}
