namespace Cilica.Fixtures.Execution
{
    public struct GenericPair<T, U>
    {
        public T First;
        public U Second;
        public static GenericPair<T, U> Create(T first, U second)
        {
            GenericPair<T, U> pair = default;
            pair.First = first;
            pair.Second = second;
            return pair;
        }
        public static GenericPair<U, T> Swap(GenericPair<T, U> pair) => GenericPair<U, T>.Create(pair.Second, pair.First);
    }
    public struct GenericEnvelope<T> { public GenericPair<int, T> Payload; }
    public struct ConstrainedValue<T> where T : struct { public T Value; }

    public static class ClosedGenericTypeProbe
    {
        public static int Execute(int number, int mode)
        {
            var graph = ValueObjectProbe.Make(number, 1);
            var original = GenericPair<int, ValueObjectNode>.Create(number, graph.Node);
            var swapped = GenericPair<int, ValueObjectNode>.Swap(original);
            GenericEnvelope<ValueObjectNode> envelope = default;
            envelope.Payload = original;
            object box = ClosedGenericProbe.RoundTrip(swapped);
            if (mode == 1) box = original;
            if (mode == 2) box = null;
            graph = default;
            original = default;
            swapped = default;
            ValueObjectProbe.Pressure(12);
            var returned = (GenericPair<ValueObjectNode, int>)box;
            ValueObjectProbe.Bump(ref envelope.Payload.First);
            if (envelope.Payload.Second != returned.First) return -100;
            return unchecked(returned.First.Number + returned.Second + envelope.Payload.First);
        }
        public static int Constraint(int number)
        {
            ConstrainedValue<int> value = default;
            value.Value = number;
            return value.Value;
        }
    }
}
