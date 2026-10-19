#nullable enable
using System;

namespace Cilica.Fixtures.Execution
{

public class LeafGetterBase
{
    public int Number;
    public long Wide;
    public LeafGetterBase? Child;
    public int GetNumber() => Number;
    public long GetWide() => Wide;
    public LeafGetterBase? GetChild() => Child;
    public int AddOne() => unchecked(Number + 1);
    public int Mutate() => ++Number;
}

public sealed class LeafGetterDerived : LeafGetterBase { public int Extra; }

public static class LeafGetterProbe
{
    public static int Run(int a, int b)
    {
        var child = new LeafGetterBase { Number = b };
        LeafGetterBase parent = new LeafGetterDerived { Number = a, Wide = ((long)a << 32) | (uint)b, Child = child, Extra = 7 };
        var result = parent.GetChild()!;
        var churn = new LeafGetterBase { Number = 3 }; // Returned reference survives a later allocation.
        return unchecked(parent.GetNumber() + (int)(parent.GetWide() >> 32) + result.GetNumber() + churn.Number + parent.AddOne() + parent.Mutate());
    }

    // A null argument to a static call reaches the getter's ldfld, rather than
    // a callvirt check in the caller. The catch exercises fault materialization.
    public static int Read(LeafGetterBase value) => value.Number;
    public static int Fault(int a, int b)
    {
        try { return Read(null!); }
        catch (NullReferenceException) { return unchecked(a + b); }
    }
}
}
