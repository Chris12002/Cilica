namespace Cilica.Fixtures.Execution
{
public delegate int NestedCallback(int first, int second);
public static class NestedCacheProbe
{
    private sealed class Cache
    {
        public static readonly Cache Instance = new Cache();
        public int Value;
        public Cache() { Value = 17; }
        internal int Add(int first, int second) { return Value + first + second; }
    }
    private sealed class Sibling
    {
        public Sibling() { }
        public int Read(int value) { return Cache.Instance.Add(value, 2); }
    }
    public static int Generated(int first, int second)
    {
        NestedCallback callback = (a, b) => a + b;
        return callback(first, second);
    }
    public static int Declared(int first, int second)
    {
        Cache value = Cache.Instance;
        return value.Add(first, second) + value.Value;
    }
    public static int Siblings(int first, int second) { return new Sibling().Read(first) + second; }
}
}
