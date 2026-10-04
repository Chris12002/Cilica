namespace Cilica.Fixtures.Execution
{

public static class ClosedGenericProbe
{
    public static T Identity<T>(T value) => value;
    public static T Relay<T>(T value) { ValueObjectProbe.Pressure(7); return Identity<T>(value); }
    public static T RoundTrip<T>(T value) => (T)(object)value!;
    public static int Divide<T>(int divisor, T value) => 10 / divisor;
    public static T Clear<T>() => default!;
    public static T RequireStruct<T>(T value) where T : struct => value;
    public static int Recurse<T>(int n, T value) => n == 0 ? 0 : unchecked(n + Recurse<T>(n - 1, value));
    public static int Expand<T>(int n) => n == 0 ? 0 : Expand<T[]>(n - 1);

    public static int Execute(int number)
    {
        var value = ValueObjectProbe.Make(number, 1);
        if (number == 11) return Divide(0, value);
        var copy = Relay(value);
        var integer = Relay(number);
        var roundTrip = RoundTrip(copy);
        var empty = Clear<EmptyBoxValue>();
        _ = RoundTrip(empty);
        return unchecked(roundTrip.Marker + integer + Recurse(4, copy) + roundTrip.Node.Number);
    }

    public static int Constraint(int number) => RequireStruct(ValueObjectProbe.Make(number, 1)).Marker;
    public static int Expansion(int number) => Expand<ValueObjectLeaf>(number);
}
}
