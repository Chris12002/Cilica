using System;
namespace Cilica.Fixtures.Execution
{
    public struct StaticNumbers
    {
        public int Number;
        public long Wide;
        public StaticNumbers(int a, int b) { Number = a; Wide = unchecked(((long)a << 32) | (uint)b); }
    }
    public struct StaticAggregate
    {
        public StaticBox First;
        public StaticBox Second;
        public StaticNumbers Numbers;
        public StaticAggregate(StaticBox first, StaticBox second, int a, int b) { First = first; Second = second; Numbers = new StaticNumbers(a, b); }
    }
    public struct StaticEmpty { }
    public static class StaticValueSlot<T> { public static T Value; }
    public static class StaticValueInitialized { public static readonly StaticNumbers Value = new StaticNumbers(17, -3); }
    public static class StaticValueThrowing
    {
        public static readonly StaticAggregate Value = new StaticAggregate(new StaticBox(), new StaticBox(), 1, 2);
        public static readonly int Failed = Fail();
        private static int Fail() { throw new InvalidOperationException("static value failure"); }
    }
    public static class StaticValueProbe
    {
        private static StaticNumbers numeric;
        private static StaticAggregate aggregate;
        private static StaticEmpty empty;
        private static StaticAggregate Cleared() => new StaticAggregate(null, null, 0, 0);
        private static StaticEmpty Empty() => default(StaticEmpty);
        private static StaticNumbers Numbers(int a, int b) => new StaticNumbers(a, b);
        private static int Read(StaticNumbers value) => unchecked(value.Number + (int)value.Wide + (int)(value.Wide >> 32));
        public static int Numeric(int a, int b) { numeric = Numbers(a, b); var copy = numeric; numeric = new StaticNumbers(b, a); return unchecked(Read(copy) + Read(numeric)); }
        public static int References(int a, int b)
        {
            aggregate = new StaticAggregate(new StaticBox { Value = a }, new StaticBox { Value = b }, a, b);
            var copy = aggregate; aggregate = Cleared();
            string pressure = string.Concat("static", "value");
            return unchecked(copy.First.Value + copy.Second.Value + Read(copy.Numbers) + pressure.Length);
        }
        public static int Closed(int a, int b)
        {
            StaticValueSlot<StaticNumbers>.Value = Numbers(a, b);
            StaticValueSlot<StaticAggregate>.Value = new StaticAggregate(new StaticBox { Value = b }, null, b, a);
            var copy = StaticValueSlot<StaticAggregate>.Value; StaticValueSlot<StaticAggregate>.Value = Cleared();
            return unchecked(Read(StaticValueSlot<StaticNumbers>.Value) + Read(copy.Numbers) + copy.First.Value);
        }
        public static int Arrays(int a, int b) { numeric = Numbers(a, b); var values = new StaticNumbers[1]; values[0] = numeric; numeric = values[0]; return Read(numeric); }
        public static int EmptyValues(int a, int b) { empty = Empty(); var copy = empty; StaticValueSlot<StaticEmpty>.Value = copy; return unchecked(a + b); }
        public static int Initialized(int a, int b) => unchecked(Read(StaticValueInitialized.Value) + a + b);
        public static int Failure(int a, int b)
        {
            try { var value = StaticValueThrowing.Value; return value.First.Value; }
            catch (TypeInitializationException) { return 31; }
        }
        public static int Persistent(int a, int mode)
        {
            if (mode == 0) aggregate = new StaticAggregate(new StaticBox { Value = a }, new StaticBox { Value = a + 1 }, a, mode);
            if (mode == 1) aggregate = new StaticAggregate(new StaticBox { Value = a }, null, a, mode);
            if (mode == 2) aggregate = Cleared();
            var copy = aggregate;
            return copy.First == null ? 0 : copy.First.Value;
        }
        public static int UnsupportedAddress(int a, int b) { numeric.Number = a; return b; }
    }
}
