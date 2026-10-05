using System;
namespace Cilica.Fixtures.Execution
{
public sealed class StaticBox { public int Value; }
public sealed class PlainStatics
{
    public static int Number;
    public static StaticBox Held;
    public static int[] Values;
    public static Action<StaticBox, int> Callback;
}
public sealed class GenericStatics<T> { public static int Number; public static T Held; }
public static class StaticStorageProbe
{
    private static void Publish(int input)
    {
        var box = new StaticBox(); box.Value = input; PlainStatics.Held = box;
        PlainStatics.Values = new int[2]; PlainStatics.Values[0] = input;
    }
    public static void Apply(StaticBox value, int mode) { value.Value = unchecked(value.Value + mode); }
    public static int References(int input, int mode)
    {
        Publish(input);
        string pressure = string.Concat("static", "root");
        PlainStatics.Number = mode;
        PlainStatics.Callback = new Action<StaticBox, int>(Apply);
        PlainStatics.Callback(PlainStatics.Held, PlainStatics.Number);
        int result = unchecked(PlainStatics.Held.Value + PlainStatics.Values[0] * 31 + pressure.Length);
        PlainStatics.Held = null; PlainStatics.Values = null; PlainStatics.Callback = null;
        return result;
    }
    public static int Closed(int input, int mode)
    {
        GenericStatics<int>.Number = input; GenericStatics<uint>.Number = mode;
        var box = new StaticBox(); box.Value = input;
        GenericStatics<StaticBox>.Held = box; GenericStatics<object>.Held = null;
        string pressure = string.Concat("closed", "static");
        int result = unchecked(GenericStatics<int>.Number * 31 + GenericStatics<uint>.Number * 17 + GenericStatics<StaticBox>.Held.Value + pressure.Length);
        if (GenericStatics<object>.Held != null) result++;
        GenericStatics<StaticBox>.Held = null;
        return result;
    }
    public static int Faults(int input, int mode)
    {
        PlainStatics.Held = null;
        try { PlainStatics.Number = input / mode; }
        catch (DivideByZeroException) { PlainStatics.Number = 17; }
        catch (OverflowException) { PlainStatics.Number = 23; }
        try { return PlainStatics.Held.Value; }
        catch (NullReferenceException) { return PlainStatics.Number; }
    }
    public static int Persistent(int input, int mode)
    {
        if (PlainStatics.Held == null) Publish(input);
        PlainStatics.Number = unchecked(PlainStatics.Number + mode);
        return unchecked(PlainStatics.Number + PlainStatics.Held.Value);
    }
}
}
