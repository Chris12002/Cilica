namespace Cilica.Fixtures.Execution
{

public static class StringProbe
{
    public static string Echo(string value) => value;
    public static int Length() => Echo("A\0\ud800X\udfff").Length;
    public static int CodeUnit(int index) => Echo("A\0\ud800X\udfff")[index];
    public static int Equal() => Echo("cat") == Echo("cat") ? 1 : 0;
    public static int NotEqual() => Echo("cat") != Echo("dog") ? 1 : 0;
    public static int StaticEquals() => string.Equals(Echo("cat"), Echo("dog")) ? 1 : 0;
    public static int Concat() => string.Concat(Echo("A\0"), Echo("\ud800")).Length;
    public static int NullLength() => Echo(null!).Length;
    public static int NullCodeUnit(int index) => Echo(null!)[index];
    public static int NullConcat() => string.Concat(Echo(null!), Echo("A")).Length;
    public static int Fields()
    {
        var holder = new StringHolder(); holder.Value = Echo("word"); holder.Values = new string[1];
        holder.Values[0] = holder.Value; return Echo(holder.Values[0]).Length;
    }
    public static int Cast(int choose)
    {
        object value = choose == 0 ? Echo("text") : new StringHolder();
        return ((string)value).Length;
    }
    public static int Test() { object value = Echo("text"); return (value as string)!.Length; }
    public static int Joined(int choose) => (choose == 0 ? Echo("left") : Echo("right")).Length;
    public static int Dispatch()
    {
        IStringEcho contract = new StringEcho();
        return contract.Echo("hello").Length;
    }
    public static int Virtual() { StringEcho value = new StringEchoChild(); return value.Echo("hello").Length; }
    public static int Unsupported() => Echo("text").Substring(1).Length;
    public static int UnsupportedConcat() => string.Concat(Echo("a"), Echo("b"), Echo("c")).Length;
}

public class StringHolder { public string Value = "initial"; public string[] Values = null!; }
public interface IStringEcho { string Echo(string value); }
public class StringEcho : IStringEcho { public virtual string Echo(string value) => value; }
public class StringEchoChild : StringEcho { public override string Echo(string value) => string.Concat(value, "!"); }
}
