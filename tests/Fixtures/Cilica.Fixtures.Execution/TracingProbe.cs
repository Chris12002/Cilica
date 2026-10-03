namespace Cilica.Fixtures.Execution
{

public class TraceBase
{
    public TraceNode Next;
    public int Number;
}
public class TraceNode : TraceBase
{
    public TraceNode Alias;
    public object[] Edges;
    public string Name;
}
public static class TracingProbe
{
    public static int Run(int seed)
    {
        var a = new TraceNode(); var b = new TraceNode();
        a.Next = b; b.Next = a; a.Alias = b;
        a.Edges = new object[3]; a.Edges[0] = a; a.Edges[1] = b;
        a.Name = "root"; b.Name = string.Concat(a.Name, "!");
        int[] numbers = new int[2]; numbers[0] = seed;
        a.Number = numbers[0];
        return Finish(a, numbers);
    }
    public static int Finish(TraceNode node, int[] numbers)
    {
        var extra = new TraceNode(); extra.Next = node;
        extra.Name = string.Concat(node.Name, "?");
        return numbers[0] + extra.Name.Length + node.Next!.Name!.Length;
    }
}
}
