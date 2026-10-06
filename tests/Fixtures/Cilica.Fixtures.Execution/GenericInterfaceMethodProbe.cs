using System;
namespace Cilica.Fixtures.Execution
{

public interface IGenericMethod<T>
{
    U Echo<U>(T owner, U value);
    int Stamp<U>(U value, int stamp);
    void Set<U>(U value, int stamp);
}
public class GenericMethod<T> : IGenericMethod<T>
{
    public T Last = default(T)!;
    public int Count;
    public U Echo<U>(T owner, U value) { Last = owner; Count++; var noise = new GenericMethodNode { Value = Count }; if (noise.Value < 0) throw new InvalidOperationException(); return value; }
    public int Stamp<U>(U value, int stamp) { Count++; return stamp + Count; }
    public void Set<U>(U value, int stamp) { Count = stamp; }
}
public sealed class GenericMethodChild<T> : GenericMethod<T> { }
public sealed class GenericMethodExplicit<T> : IGenericMethod<T>
{
    public int Count;
    U IGenericMethod<T>.Echo<U>(T owner, U value) { Count++; return value; }
    int IGenericMethod<T>.Stamp<U>(U value, int stamp) { return stamp - ++Count; }
    void IGenericMethod<T>.Set<U>(U value, int stamp) { Count = stamp; }
}
public interface IGenericOverload
{
    U Echo<U>(U value);
    int Echo<U>(int value);
}
public sealed class GenericOverload : IGenericOverload
{
    public U Echo<U>(U value) { return value; }
    public int Echo<U>(int value) { return value + 17; }
}
public sealed class GenericMethodNode { public int Value; }
public struct GenericMethodPacket { public int Value; public GenericMethodNode Node; }
public interface IGenericLayer<T>
{
    IGenericLayer<T> Add<U>(Func<T, U> selector);
    int Measure(T value);
}
public sealed class GenericLayerSeed<T> : IGenericLayer<T>
{
    public IGenericLayer<T> Add<U>(Func<T, U> selector) { return new GenericLayer<T, U> { Parent = this, Selector = selector }; }
    public int Measure(T value) { return 0; }
}
public sealed class GenericLayer<T, U> : IGenericLayer<T>
{
    public IGenericLayer<T> Parent = null!;
    public Func<T, U> Selector = null!;
    public IGenericLayer<T> Add<V>(Func<T, V> selector) { return new GenericLayer<T, V> { Parent = this, Selector = selector }; }
    public int Measure(T value) { Selector(value); return Parent.Measure(value) + 1; }
}
public interface IGenericOverride { U Echo<U>(U value); }
public class GenericOverride : IGenericOverride { public virtual U Echo<U>(U value) { return value; } }
public static class GenericInterfaceMethodProbe
{
    private static int NumberKey(int x) { if (x < 0) throw new InvalidOperationException(); return x; }
    private static string TextKey(int x) { return "key"; }
    public static int Factory(int a, int b)
    {
        IGenericLayer<int> layer = new GenericLayerSeed<int>();
        layer = layer.Add<int>(NumberKey).Add<string>(TextKey);
        try { return layer.Measure(a) + b; } catch (InvalidOperationException) { return b + 31; }
    }
    public static int UnsupportedOverride(int a, int b) { IGenericOverride c = new GenericOverride(); return c.Echo<int>(a); }
    public static int UnsupportedMixed(int a, int b)
    {
        var c = new GenericMethod<int>(); IGenericMethod<int> view = c;
        return view.Echo<int>(a, b) + c.Echo<int>(a, b);
    }
    public static int UnsupportedClass(int a, int b) { return new GenericMethod<int>().Echo<int>(a, b); }
    public static int NullOnly(int a, int b)
    {
        IGenericMethod<int> c = null!;
        try { return c.Echo<int>(a, b); } catch (NullReferenceException) { return a + b + 41; }
    }
    public static int Faults(int a, int b)
    {
        IGenericMethod<int> c = new GenericMethod<int>();
        c.Set<string>("fault", a);
        var packet = new GenericMethodPacket { Value = b, Node = new GenericMethodNode { Value = a } };
        try { return c.Echo<GenericMethodPacket>(a, packet).Value; }
        catch (InvalidOperationException) { return b + 37; }
    }
    public static int Scalars(int a, int b)
    {
        IGenericMethod<int> c = new GenericMethod<int>();
        return c.Echo<int>(a, b) + (int)c.Echo<long>(b, (long)a * 7) + c.Stamp<uint>((uint)a, b) + c.Stamp<int>(a, b);
    }
    public static int ClosedOwners(int a, int b)
    {
        IGenericMethod<string> c = new GenericMethodChild<string>();
        var node = new GenericMethodNode { Value = a };
        var same = c.Echo<GenericMethodNode>("owner", node);
        return (same == node ? 19 : 2) + same.Value + c.Echo<int>("other", b);
    }
    public static int Explicit(int a, int b)
    {
        IGenericMethod<int> c = new GenericMethodExplicit<int>();
        c.Set<string>("value", a); return c.Echo<int>(a, b) + c.Stamp<long>(b, a);
    }
    public static int Receivers(int a, int b)
    {
        IGenericMethod<int> c = a < 0 ? (IGenericMethod<int>)new GenericMethodExplicit<int>() : new GenericMethodChild<int>();
        return c.Echo<int>(a, b) + c.Stamp<string>("value", a);
    }
    public static int Null(int a, int b)
    {
        IGenericMethod<int> c = a < 0 ? null! : new GenericMethod<int>();
        try { return c.Echo<int>(a, b); } catch (NullReferenceException) { return a + 23; }
    }
    public static int Values(int a, int b)
    {
        IGenericMethod<int> c = new GenericMethod<int>();
        var node = new GenericMethodNode { Value = b };
        var value = new GenericMethodPacket { Value = a, Node = node };
        var result = c.Echo<GenericMethodPacket>(a, value);
        return result.Value + result.Node.Value + (result.Node == node ? 29 : 3);
    }
    private static U EchoOpen<U>(IGenericOverload c, U value) { return c.Echo<U>(value); }
    public static int Overloads(int a, int b)
    {
        IGenericOverload c = new GenericOverload();
        return EchoOpen<int>(c, a) + c.Echo<int>(b);
    }
}

}
