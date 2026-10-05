using System;
namespace Cilica.Fixtures.Execution
{
public interface IDelegateValue { int Read(); }
public sealed class DelegateBox : IDelegateValue
{
    public int Value;
    public int Read() => Value;
}
public delegate int ReferenceFunction(DelegateBox value, int mode);
public sealed class ReferenceReceiver
{
    public Action<DelegateBox, int> Back;
    public int Result;
    public void Apply(DelegateBox box, int mode)
    {
        string pressure = string.Concat("during", "callback");
        box.Value = unchecked(box.Value + mode);
        Result = unchecked(box.Value * 31 + pressure.Length);
    }
}
public static class ReferenceDelegateProbe
{
    public static int Capture(int input, int mode)
    {
        var receiver = new ReferenceReceiver();
        var callback = new Action<DelegateBox, int>(receiver.Apply); receiver.Back = callback;
        var box = new DelegateBox(); box.Value = input;
        receiver.Back(box, mode);
        return unchecked(receiver.Result + box.Value);
    }
    public static int Function(int input, int mode)
    {
        var box = new DelegateBox(); box.Value = input;
        var callback = new ReferenceFunction(Read);
        return callback(box, mode);
    }
    public static int Read(DelegateBox box, int mode)
    {
        string pressure = string.Concat("function", "root");
        return unchecked(box.Value + mode + pressure.Length);
    }
    public static int Interface(int input, int mode)
    {
        var box = new DelegateBox(); box.Value = input;
        var action = new Action<IDelegateValue, string>(Inspect);
        try { action(box, mode < 0 ? null : "text"); return mode; }
        catch (ArgumentException e) { return unchecked(e.ParamName.Length + input); }
    }
    public static void Inspect(IDelegateValue box, string text)
    {
        if (text == null || box.Read() == text.Length) throw new ArgumentException("callback", "text");
    }
    public static int Arrays(int input, int mode)
    {
        var box = new DelegateBox(); box.Value = input;
        var values = new DelegateBox[1]; values[0] = box;
        var action = new Action<DelegateBox[], string>(Change);
        try { action(values, mode < 0 ? null : "text"); }
        catch (NullReferenceException) { return input; }
        return box.Value;
    }
    public static void Change(DelegateBox[] values, string text)
    {
        string pressure = string.Concat("array", "root");
        values[0].Value = unchecked(values[0].Value + text.Length + pressure.Length);
    }
    public static int Null(int input, int mode)
    {
        Action<DelegateBox, int> callback = null;
        try { callback(new DelegateBox(), input / mode); return -1; }
        catch (DivideByZeroException) { return 11; }
        catch (OverflowException) { return 17; }
        catch (NullReferenceException) { return 23; }
    }
    public static int UnsupportedVariance(int input, int mode)
    {
        var action = new Action<string, int>(AcceptObject);
        action("text", input); return mode;
    }
    public static void AcceptObject(object value, int number) { }
}
}
