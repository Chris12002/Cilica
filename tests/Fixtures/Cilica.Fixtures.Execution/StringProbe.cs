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
    public static int ConcatCodeUnit(int index) => string.Concat(Echo("A\0"), Echo("\ud800\ud83d\ude00\udfff"))[index];
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
    public static string Select(int choice)
    {
        if (choice == 0) return null!;
        if (choice == 1) return "";
        if (choice == 2) return "cat";
        if (choice == 3) return "dog";
        if (choice == 4) return "\0\ud800\udfff\ud83d\ude00";
        return string.Concat(Echo("c"), Echo("at"));
    }
    public static string Literal() => "cat";
    public static int LiteralIdentity() => (object)Echo("cat") == (object)Literal() ? 1 : 0;
    public static int Contents(int left, int right) => string.Equals(Select(left), Select(right)) ? 1 : 0;
    public static int Concatenation(int left, int right)
    {
        string a = Select(left), b = Select(right), result = string.Concat(a, b);
        return result.Length * 16 + ((object)result == (object)a ? 1 : 0)
            + ((object)result == (object)b ? 2 : 0) + ((object)result == (object)Echo("") ? 4 : 0);
    }
    public static int FreshIdentity()
    {
        string a = Select(5), b = Select(5);
        return (a == b ? 1 : 0) + ((object)a == (object)b ? 2 : 0) + ((object)a == (object)Literal() ? 4 : 0);
    }
    public static int Rooted(int count)
    {
        string value = "";
        for (int i = 0; i < count; i++) value = string.Concat(value, Echo("\ud800\0"));
        return value.Length;
    }
    public static int OrderedFault(int divisor) => Echo(null!)[12 / divisor];
    public static int Read(string value, int index) => value[index];
    public static int Boundaries(int index) => Read(Echo("abc"), index);
    public static int CheckedStore(int choice)
    {
        object[] values = new string[1];
        values[0] = choice == 0 ? (object)Echo("cat") : new StringHolder();
        return ((string)values[0]).Length;
    }
    public static int FrameworkInterface()
    {
        object value = Echo("cat"); return value is System.IComparable ? 1 : 0;
    }
    public static int FrameworkArrayStore()
    {
        object[] values = new System.IComparable[1]; values[0] = Echo("cat"); return values.Length;
    }
    public static int Unsupported() => Echo("text").Substring(1).Length;
    public static int UnsupportedConcat() => string.Concat(Echo("a"), Echo("b"), Echo("c")).Length;
}

public class StringHolder { public string Value = "initial"; public string[] Values = null!; }
public interface IStringEcho { string Echo(string value); }
public class StringEcho : IStringEcho { public virtual string Echo(string value) => value; }
public class StringEchoChild : StringEcho { public override string Echo(string value) => string.Concat(value, "!"); }
}
