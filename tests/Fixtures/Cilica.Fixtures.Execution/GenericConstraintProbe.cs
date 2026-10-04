namespace Cilica.Fixtures.Execution
{
    public class GenericConstructible { public GenericConstructible() { } }
    public class GenericNoConstructor { private GenericNoConstructor() { } }
    public static class GenericConstraintProbe
    {
        public static T Struct<T>(T value) where T : struct => value;
        public static T Reference<T>(T value) where T : class => value;
        public static T Construct<T>(T value) where T : new() => value;
        public static int Unused<T>(int value) => value;
        public static int Execute(int number)
        {
            var value = Struct(number);
            var receiver = Reference<GenericConstructible>(null);
            if (Construct(receiver) != null) return -1;
            ConstrainedValue<int> pair = default;
            pair.Value = Construct(value);
            return Unused<ConstrainedValue<int>>(pair.Value);
        }
    }
}
