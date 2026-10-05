using System;
namespace Cilica.Fixtures.Execution
{
public static class TypeInitializationExceptionProbe
{
    public static int Payload(int input, int mode)
    {
        Exception inner = mode < 0 ? null : new ArgumentException("inner", "value");
        string name = input < 0 ? null : "Example\0\ud800.Type";
        var error = new TypeInitializationException(name, inner);
        string pressure = string.Concat("keep", "payload");
        int result = error.TypeName.Length + error.Message.Length * 17 + pressure.Length;
        result += error.InnerException == inner ? 1 : 0;
        result += (object)error.Message == (object)error.Message ? 2 : 0;
        result += (object)error.TypeName == (object)(name ?? "") ? 4 : 0;
        result += error.InnerException == null ? 8 : error.InnerException.Message.Length;
        return result;
    }
    public static int Chain(int input, int mode)
    {
        var original = new Exception("origin");
        var first = new TypeInitializationException("First", original);
        Exception second = new TypeInitializationException("Second", first);
        string pressure = string.Concat("nested", "root");
        try { throw second; }
        catch (TypeInitializationException caught)
        {
            return (caught.InnerException == first ? 1 : 0) + (caught.InnerException.InnerException == original ? 2 : 0)
                + (original.InnerException == null ? 4 : 0) + caught.TypeName.Length + caught.InnerException.Message.Length + pressure.Length;
        }
    }
    public static int Null(int input, int mode)
    {
        TypeInitializationException error = null; int result = 0;
        try { _ = error.TypeName; } catch (NullReferenceException) { result += 1; }
        try { _ = error.InnerException; } catch (NullReferenceException) { result += 2; }
        try { _ = error.Message; } catch (NullReferenceException) { result += 4; }
        return result;
    }
    public static int CodeUnit(int input, int index)
    {
        var error = new TypeInitializationException(input < 0 ? null : "A\0\ud800\udfff", null);
        try { return error.Message[index]; } catch (IndexOutOfRangeException) { return -1; }
    }
}
}
