namespace Cilica.Fixtures.Execution
{

public class ProvenFieldBase
{
    public int Number;
    public long Wide;
    public ProvenFieldBase Link;
    public int Read() => Number;
}
public sealed class ProvenFieldChild : ProvenFieldBase { public new int Number; }
public sealed class ProvenFieldOther : ProvenFieldBase { public long Tail; }
public sealed class ProvenFieldThird : ProvenFieldBase { }
public sealed class ProvenFieldFourth : ProvenFieldBase { }
public sealed class ProvenFieldFifth : ProvenFieldBase { }

public static class ProvenFieldProbe
{
    private static int Read(ProvenFieldBase value) => unchecked(value.Read() + (int)value.Wide + value.Link.Number);
    public static int Mixed(int a, int b)
    {
        var child = new ProvenFieldChild();
        ProvenFieldBase parent = child;
        parent.Number = a; child.Number = b; parent.Wide = ((long)a << 32) | (uint)b;
        parent.Link = new ProvenFieldBase { Number = unchecked(a + b) };
        var other = new ProvenFieldOther { Number = b, Wide = a, Link = parent, Tail = long.MinValue };
        return unchecked(Read(parent) + Read(other) + child.Number + (int)(other.Tail >> 32));
    }
    public static int Many(int a, int b)
    {
        ProvenFieldBase value;
        switch ((a & 7) % 6)
        {
            case 0: value = new ProvenFieldBase(); break;
            case 1: value = new ProvenFieldChild(); break;
            case 2: value = new ProvenFieldOther(); break;
            case 3: value = new ProvenFieldThird(); break;
            case 4: value = new ProvenFieldFourth(); break;
            default: value = new ProvenFieldFifth(); break;
        }
        value.Number = b; return value.Read();
    }
    public static int Null(int a, int b)
    {
        ProvenFieldBase value = null;
        try { return value.Number; }
        catch (System.NullReferenceException) { return unchecked(a + b); }
    }
}
}
