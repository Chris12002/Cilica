namespace Cilica.Fixtures.Execution
{
    public struct DefaultStaticValue
    {
        public static readonly DefaultStaticValue Empty;
        public long Number;
        public object Reference;
    }
    public struct DefaultStaticEmpty { public static readonly DefaultStaticEmpty Empty; }
    public sealed class DefaultStaticHolder<T>
    {
        public static readonly T Empty;
        public static readonly int Number;
        public static readonly long Wide;
        public static readonly bool Flag;
        public int Value;
        public DefaultStaticHolder(int value) { Value = value; }
    }
    public sealed class InitializedStaticHolder
    {
        public static readonly int Value = 19;
    }
    public static class DefaultStaticProbe
    {
        public static int Values(int input, int mode)
        {
            var value = DefaultStaticValue.Empty;
            value.Number = input;
            value.Reference = new DefaultStaticHolder<int>(mode);
            var again = DefaultStaticValue.Empty;
            return (int)value.Number + (int)again.Number + (again.Reference == null ? 13 : 17)
                + ((DefaultStaticHolder<int>)value.Reference).Value;
        }
        public static int Generic(int input, int mode)
        {
            var instance = new DefaultStaticHolder<DefaultStaticValue>(input);
            var empty = DefaultStaticHolder<DefaultStaticValue>.Empty;
            long wide = DefaultStaticHolder<long>.Empty + DefaultStaticHolder<object>.Wide;
            object reference = DefaultStaticHolder<object>.Empty;
            var zeroSize = DefaultStaticEmpty.Empty;
            var copy = DefaultStaticHolder<DefaultStaticEmpty>.Empty;
            Consume(zeroSize, copy);
            return instance.Value + (int)empty.Number + (empty.Reference == null ? 19 : 23)
                + (int)wide + DefaultStaticHolder<object>.Number + (reference == null ? 29 : 31)
                + (DefaultStaticHolder<object>.Flag ? 37 : 41) + mode;
        }
        public static void Consume(DefaultStaticEmpty first, DefaultStaticEmpty second) { }
        public static int Initialized(int input, int mode) => InitializedStaticHolder.Value + input + mode;
    }
}
