using System;
namespace Cilica.Fixtures.Execution
{
public static class InitializationControl { public static int Calls; }
public sealed class InitializedFields
{
    public static readonly int First = ReadLater();
    public static readonly int Later = 5;
    public static readonly string Name = string.Concat("initialized", " value");
    private static int ReadLater() { return Later + 2; }
}
public sealed class FailedFields
{
    public static readonly int Value = Fail();
    private static int Fail() { InitializationControl.Calls++; throw new InvalidOperationException("initializer failed"); }
}
public sealed class InitializedClosed<T>
{
    public static readonly object Value = new object();
}
public sealed class FailedClosed<T>
{
    public static readonly int Value = Fail();
    private static int Fail() { throw new Exception("generic"); }
}
public sealed class StrictInitialized
{
    public static int Value;
    static StrictInitialized() { Value = 7; }
}
public static class TypeInitializationProbe
{
    public static int UnsupportedStrict(int input, int mode) { return StrictInitialized.Value; }
    public static int GenericFailure(int input, int mode)
    {
        try { return FailedClosed<int>.Value; }
        catch (TypeInitializationException caught) { return caught.TypeName.Length + caught.Message.Length * 100
            + (caught.TypeName == "Cilica.Fixtures.Execution.FailedClosed`1" ? 101 : 203); }
    }
    public static int Fields(int input, int mode)
    {
        return InitializedFields.First + InitializedFields.Later * 10 + InitializedFields.Name.Length;
    }
    public static int Failure(int input, int mode)
    {
        TypeInitializationException first = null;
        try { return FailedFields.Value; } catch (TypeInitializationException caught) { first = caught; }
        string pressure = string.Concat("collect", "now");
        try { return FailedFields.Value; }
        catch (TypeInitializationException caught)
        {
            return (caught == first ? 1 : 0) + (caught.InnerException == first.InnerException ? 2 : 0)
                + caught.InnerException.Message.Length * 10 + caught.TypeName.Length + InitializationControl.Calls * 1000 + pressure.Length;
        }
    }
    public static int Closed(int input, int mode)
    {
        object first = InitializedClosed<int>.Value;
        object second = InitializedClosed<string>.Value;
        return (first == InitializedClosed<int>.Value ? 1 : 0) + (first != second ? 2 : 0);
    }
}
}
