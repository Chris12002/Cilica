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
    }
}
