using System;
namespace Cilica.Fixtures.Execution
{
    public struct InitializedPoint
    {
        public int Number;
        public long Wide;
        public static readonly InitializedPoint Seed = new InitializedPoint(17, -3);
        public InitializedPoint(int number, long wide) { Number = number; Wide = wide; }
    }
    public struct InitializedSlot<T>
    {
        public T Item;
        public static readonly int Stamp = 23;
        public static InitializedSlot<T> Current;
        public InitializedSlot(T item) { Item = item; }
    }
    public struct InitializedReference
    {
        public StaticBox Box;
        public static readonly InitializedReference Seed = new InitializedReference(new StaticBox { Value = 37 });
        public InitializedReference(StaticBox box) { Box = box; }
    }
    public struct InitializedRecursive
    {
        public int Number;
        public static readonly InitializedRecursive Seed = Create();
        public InitializedRecursive(int number) { Number = number; }
        private static InitializedRecursive Create() { var prior = Seed; return new InitializedRecursive(prior.Number + 19); }
    }
    public struct InitializedFailure
    {
        public int Number;
        public static readonly InitializedFailure Seed = Create();
        private static InitializedFailure Create() { throw new InvalidOperationException("struct init"); }
    }
    public struct InitializedEmpty { public static readonly int Stamp = 29; }
    public struct StrictInitializedValue
    {
        public int Number;
        public static readonly int Stamp;
        static StrictInitializedValue() { Stamp = 31; }
    }
    public static class InitializedStructProbe
    {
        public static int Numeric(int a, int b)
        {
            var value = InitializedPoint.Seed; value.Number = a;
            var again = InitializedPoint.Seed;
            return unchecked(value.Number + again.Number + (int)again.Wide + b);
        }
        public static int Closed(int a, int b)
        {
            InitializedSlot<int>.Current = new InitializedSlot<int>(a);
            InitializedSlot<long>.Current = new InitializedSlot<long>(unchecked(((long)b << 32) | (uint)a));
            var wide = InitializedSlot<long>.Current;
            var narrow = InitializedSlot<int>.Current;
            return unchecked(narrow.Item + (int)(wide.Item >> 32)
                + InitializedSlot<int>.Stamp + InitializedSlot<long>.Stamp);
        }
        public static int References(int a, int b)
        {
            var first = InitializedReference.Seed;
            string pressure = string.Concat("struct", "root");
            var again = InitializedReference.Seed;
            return unchecked(first.Box.Value + (object.ReferenceEquals(first.Box, again.Box) ? 100 : 0) + pressure.Length + a + b);
        }
        public static int Persistent(int a, int b)
        {
            var value = InitializedReference.Seed;
            value.Box.Value = unchecked(value.Box.Value + b);
            return unchecked(value.Box.Value + a);
        }
        public static int Reentrant(int a, int b) { var seed = InitializedRecursive.Seed; return unchecked(seed.Number + a + b); }
        public static int Failure(int a, int b)
        {
            Exception first = null;
            try { var value = InitializedFailure.Seed; return value.Number; }
            catch (TypeInitializationException error) { first = error.InnerException; }
            try { var value = InitializedFailure.Seed; return value.Number; }
            catch (TypeInitializationException error)
            {
                return unchecked((object.ReferenceEquals(first, error.InnerException) ? 100 : 0) + error.InnerException.Message.Length + a + b);
            }
        }
        public static int Empty(int a, int b) { var values = new InitializedEmpty[1]; values[0] = default; return unchecked(values.Length + InitializedEmpty.Stamp + a + b); }
        public static int UnsupportedStrict(int a, int b) { var value = new StrictInitializedValue { Number = a }; return unchecked(value.Number + StrictInitializedValue.Stamp + b); }
        public static int UnsupportedAddress(int a, int b) { InitializedSlot<int>.Current.Item = a; return b; }
    }
}
