using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
public sealed class FrameworkReceiver
{
    public int Value;
    public Action<int> Back;
    public void Set(int value) { var pressure = new DelegateBox { Value = value }; Value = pressure.Value; }
}
public static class FrameworkDelegateProbe
{
    public static int Add(int first, int second) => unchecked(first + second);
    public static int Increment(int value) => unchecked(value + 1);
    public static int FuncOne(int input, int mode) => new Func<int, int>(Increment)(input);
    public static int FuncTwo(int input, int mode) => new Func<int, int, int>(Add)(input, mode);
    public static int FuncReference(int input, int mode)
    {
        var callback = new Func<int, DelegateBox>(ReferenceResultDelegateProbe.Make);
        var first = callback(input); var second = callback(mode);
        return unchecked(first.Value + second.Value + (first == second ? 3 : 7));
    }
    public static int FuncReferenceArguments(int input, int mode)
    {
        var callback = new Func<DelegateBox, int, DelegateBox>(ReferenceResultDelegateProbe.Echo);
        var original = new DelegateBox { Value = input }; var result = callback(original, mode);
        var pressure = new DelegateBox { Value = 1 };
        return unchecked(result.Value + pressure.Value + (original == result ? 11 : 13));
    }
    public static int FuncCapture(int input, int mode)
    {
        var receiver = new ResultReceiver { Box = new DelegateBox { Value = input } };
        var callback = new Func<int, DelegateBox>(receiver.Get);
        var first = callback(mode); var second = callback(1);
        return unchecked(first.Value + (first == second ? 17 : 19));
    }
    public static int ActionValue(int input, int mode)
    {
        var receiver = new FrameworkReceiver(); receiver.Back = new Action<int>(receiver.Set);
        receiver.Back(input); return unchecked(receiver.Value + mode);
    }
    public static void Change(DelegateBox value) { value.Value = unchecked(value.Value + new DelegateBox { Value = 1 }.Value); }
    public static int ActionReference(int input, int mode)
    {
        var callback = new Action<DelegateBox>(Change); var box = new DelegateBox { Value = input };
        callback(box); return unchecked(box.Value + mode);
    }
    public static bool Positive(int value) => value > 0;
    public static int PredicateInt(int input, int mode)
    {
        var callback = new Predicate<int>(Positive); return (callback(input) ? 23 : 29) + (callback(mode) ? 31 : 37);
    }
    public static bool Inspect(DelegateBox value)
    {
        var pressure = new DelegateBox { Value = 1 };
        return value != null && value.Value >= pressure.Value;
    }
    public static int PredicateReference(int input, int mode)
    {
        var callback = new Predicate<DelegateBox>(Inspect); var box = new DelegateBox { Value = input };
        return (callback(box) ? 41 : 43) + (callback(null) ? 47 : 53);
    }
    public static int Compare(int left, int right) => left < right ? -1 : left > right ? 1 : 0;
    public static int ComparisonInt(int input, int mode) => new Comparison<int>(Compare)(input, mode);
    public static int CompareReferences(DelegateBox left, DelegateBox right)
    {
        var pressure = new DelegateBox { Value = 1 };
        return Compare(left == null ? 0 : left.Value, right == null ? 0 : right.Value) + pressure.Value;
    }
    public static int ComparisonReference(int input, int mode)
    {
        var callback = new Comparison<DelegateBox>(CompareReferences);
        var left = new DelegateBox { Value = input }; var right = mode < 0 ? null : new DelegateBox { Value = mode };
        return callback(left, right);
    }
    public static int NullFunc(int input, int mode)
    {
        Func<int, DelegateBox> callback = null;
        try { return callback(input / mode).Value; }
        catch (DivideByZeroException) { return 59; }
        catch (OverflowException) { return 61; }
        catch (NullReferenceException) { return 67; }
    }
    public static bool Throwing(int value)
    {
        var pressure = new DelegateBox { Value = value };
        if (value < 0) throw new ArgumentException("predicate", "value");
        return pressure.Value != 0;
    }
    public static int PredicateFault(int input, int mode)
    {
        var callback = new Predicate<int>(Throwing);
        try { return callback(input) ? mode : input; }
        catch (ArgumentException e) { return unchecked(e.ParamName.Length + new DelegateBox { Value = mode }.Value); }
    }
    public static int FuncBoolean(int input, int mode)
    {
        var callback = new Func<int, bool>(Positive); return (callback(input) ? 71 : 73) + (callback(mode) ? 79 : 83);
    }
    public static int Dispatcher(int input, int mode)
    {
        var handlers = new Dictionary<Type, Func<DelegateBox, int, DelegateBox>>();
        handlers.TryAdd(typeof(DelegateBox), new Func<DelegateBox, int, DelegateBox>(ReferenceResultDelegateProbe.Echo));
        handlers.TryGetValue(typeof(DelegateBox), out var handler);
        var original = new DelegateBox { Value = input }; var result = handler(original, mode);
        var pressure = new DelegateBox { Value = 1 };
        return unchecked(result.Value + pressure.Value + (result == original ? 89 : 97));
    }
    public static int UnsupportedVariance(int input, int mode)
    {
        Func<int, DelegateBox> specific = new Func<int, DelegateBox>(ReferenceResultDelegateProbe.Make);
        Func<int, object> wider = specific;
        return ((DelegateBox)wider(input)).Value;
    }
    public static void AcceptWide(ulong value) { }
    public static int UnsupportedUnsigned(int input, int mode) { new Action<ulong>(AcceptWide)((ulong)(uint)input); return mode; }
    public static short Small(int value) => (short)value;
    public static int UnsupportedNarrowResult(int input, int mode) => new Func<int, short>(Small)(input);
    public static int BoolArgument(bool value) => value ? 1 : 0;
    public static int UnsupportedBooleanArgument(int input, int mode) => new Func<bool, int>(BoolArgument)(input > 0);
    public static void Empty(int value) { }
    public static int UnsupportedMulticast(int input, int mode)
    {
        var first = new Action<int>(Empty); var second = new Action<int>(Empty);
        first += second; first(input); return mode;
    }
}
}
