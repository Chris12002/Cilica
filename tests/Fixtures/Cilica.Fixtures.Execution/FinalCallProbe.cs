#nullable enable
using System;
namespace Cilica.Fixtures.Execution
{
public interface IFinalCall { int Read(int value); void Set(int value); }
public abstract class FinalCallBase : IFinalCall
{
    public int State;
    public int Read(int value) => unchecked(State + value);
    public void Set(int value) { State = value; }
}
public sealed class FinalCallLeaf : FinalCallBase
{
    public new int Read(int value) => unchecked(value - 731);
}
public static class FinalCallProbe
{
    public static int NullReturn(int a, int b)
    { FinalCallBase value = null!; try { return value.Read(a); } catch (NullReferenceException) { return 17; } }
    public static int NullVoid(int a, int b)
    { FinalCallBase value = null!; try { value.Set(a); return 0; } catch (NullReferenceException) { return 19; } }
    public static int ArgumentFault(int a, int b)
    { FinalCallBase value = null!; try { value.Set((a & 127) / b); return 0; } catch (DivideByZeroException) { return 11; } catch (NullReferenceException) { return 13; } }
    public static int Inherited(int a, int b)
    { var leaf = new FinalCallLeaf(); FinalCallBase value = leaf; value.Set(a); return unchecked(value.Read(b) + leaf.Read(b)); }
    public static int Interface(int a, int b)
    { IFinalCall value = new FinalCallLeaf(); value.Set(a); return value.Read(b); }
}
}
