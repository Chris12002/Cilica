using System;
namespace Cilica.Fixtures.Execution
{
public static class NativeModuleProbe
{
    public sealed class Node
    {
        public int Value;
        public Node(int value) { Value = value; }
        public int Apply(int value) => unchecked(Value + value);
    }
    public struct Pair { public long Wide; public Node Node; }
    private static class Seed { public static readonly int Value = Read(); private static int Read() => 17; }
    private static Node Make(int value) => new(value);
    private static int Read(ref Pair pair, int add)
    {
        pair.Wide = unchecked(pair.Wide + add);
        pair.Node = Make(unchecked(pair.Node.Value + Seed.Value));
        return unchecked((int)(pair.Wide >> 32) + pair.Node.Value);
    }
    private static int Invoke(Func<int, int> call, int value) => call(value);
    private static int Fault(int value)
    {
        if (value < 0) throw new ArgumentException("module");
        return value;
    }
    public static int Legacy(int first, int second) => unchecked(ManagedExceptionProbe.Composed(first) + second);
    public static int Run(int first, int second)
    {
        var node = Make(first);
        var pair = new Pair { Wide = ((long)first << 32) | (uint)second, Node = node };
        Func<int, int> call = node.Apply;
        int value = Read(ref pair, second);
        try { value = unchecked(value + Invoke(call, Fault(second))); }
        catch (ArgumentException e) { value = unchecked(value + e.Message.Length); }
        return unchecked(value + pair.Node.Value + node.Value);
    }
}
}
