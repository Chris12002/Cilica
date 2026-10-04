namespace Cilica.Fixtures.Execution
{
    public class LocationBase
    {
        public int Number;
        public LocationNode Next;
        public readonly int Initial;
        public LocationBase(int number) { Number = number; Initial = number; }
    }

    public class LocationNode : LocationBase
    {
        public object Any;
        public LocationNode(int number) : base(number) { }
    }

    public static class LocationProbe
    {
        public static int Build(int number)
        {
            var first = new LocationNode(number);
            var second = new LocationNode(7);
            first.Next = second;
            first.Any = second;
            return unchecked(first.Number + first.Next.Initial);
        }

        public static int Execute(int number)
        {
            int copy = number;
            Reborrow(ref number);
            var value = new LocationNode(number);
            Replace(ref value, number);
            Mutate(ref value.Number);
            return unchecked(copy + value.Number + Read(in value.Initial));
        }

        public static void Mutate(ref int number)
        {
            _ = new LocationNode(0);
            number = unchecked(number + 1);
        }

        public static void Reborrow(ref int number) { Mutate(ref number); }

        public static int Read(in int number) { return number; }

        public static void Replace(ref LocationNode value, int number)
        {
            value = new LocationNode(unchecked(number + 7));
            _ = new LocationNode(number);
        }
    }
}
