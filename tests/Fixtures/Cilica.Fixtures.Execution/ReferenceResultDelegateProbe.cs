using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
public delegate DelegateBox BoxFactory(int value);
public delegate T ResultFactory<T>(int value);
public delegate object ObjectFactory(int value);
public delegate DelegateBox ReferenceResult(DelegateBox box, int mode);
public delegate ulong WideFactory(int value);
public delegate DictionaryLeaf ValueFactory(int value);
public delegate ref int ByRefFactory(DelegateBox value);
public delegate RecursiveFactory RecursiveFactory(int value);
public sealed class ResultReceiver
{
    public BoxFactory Back;
    public DelegateBox Box;
    public DelegateBox Get(int value)
    {
        var pressure = new DelegateBox(); pressure.Value = value;
        Box.Value = unchecked(Box.Value + pressure.Value);
        return Box;
    }
}
public static class ReferenceResultDelegateProbe
{
    public static DelegateBox Make(int value) => new DelegateBox { Value = value };
    public static int Fresh(int input, int mode)
    {
        var callback = new BoxFactory(Make);
        var first = Forward(callback, input);
        var second = callback(mode);
        return unchecked(first.Value + second.Value + (first == second ? 7 : 11));
    }
    public static DelegateBox Forward(BoxFactory callback, int value)
    {
        var result = callback(value);
        var pressure = new DelegateBox { Value = value };
        result.Value = unchecked(result.Value + pressure.Value);
        return result;
    }
    public static int InstanceCycle(int input, int mode)
    {
        var receiver = new ResultReceiver { Box = new DelegateBox { Value = input } };
        receiver.Back = new BoxFactory(receiver.Get);
        var first = receiver.Back(mode);
        var second = receiver.Back(1);
        return unchecked(first.Value + (first == second && second == receiver.Box ? 13 : 17));
    }
    public static DelegateBox Maybe(int value) => value < 0 ? null : Make(value);
    public static int Null(int input, int mode)
    {
        var callback = new BoxFactory(Maybe);
        var result = callback(input);
        var pressure = Make(mode);
        return result == null ? pressure.Value : unchecked(result.Value + pressure.Value);
    }
    public static DelegateBox Throwing(int value)
    {
        var held = Make(value);
        if (value < 0) throw new ArgumentException("callback", "value");
        return held;
    }
    public static int Fault(int input, int mode)
    {
        var callback = new BoxFactory(Throwing);
        var held = callback(1);
        try { return unchecked(callback(input).Value + held.Value); }
        catch (ArgumentException e) { return unchecked(held.Value + e.ParamName.Length + Make(mode).Value); }
    }
    public static int NullReceiver(int input, int mode)
    {
        BoxFactory callback = null;
        try { return callback(input / mode).Value; }
        catch (DivideByZeroException) { return 11; }
        catch (OverflowException) { return 17; }
        catch (NullReferenceException) { return 23; }
    }
    public static string MakeString(int value) => string.Concat("result", "root");
    public static int String(int input, int mode)
    {
        var callback = new ResultFactory<string>(MakeString);
        string result = callback(input);
        var pressure = Make(mode);
        return unchecked(result.Length + pressure.Value);
    }
    public static DelegateBox[] MakeArray(int value) => new DelegateBox[] { Make(value) };
    public static int Array(int input, int mode)
    {
        var callback = new ResultFactory<DelegateBox[]>(MakeArray);
        var result = callback(input);
        var pressure = Make(mode);
        return unchecked(result[0].Value + pressure.Value);
    }
    public static IDelegateValue MakeInterface(int value) => Make(value);
    public static int Interface(int input, int mode)
    {
        var callback = new ResultFactory<IDelegateValue>(MakeInterface);
        var result = callback(input);
        var pressure = Make(mode);
        return unchecked(result.Read() + pressure.Value);
    }
    public static object MakeObject(int value) => Make(value);
    public static int Object(int input, int mode)
    {
        var callback = new ObjectFactory(MakeObject);
        object result = callback(input);
        var pressure = Make(mode);
        return unchecked(((DelegateBox)result).Value + pressure.Value);
    }
    public static int Generic(int input, int mode)
    {
        var callback = new ResultFactory<DelegateBox>(Make);
        var result = callback(input);
        var pressure = Make(mode);
        return unchecked(result.Value + pressure.Value);
    }
    public static int Dictionary(int input, int mode)
    {
        var receiver = new ResultReceiver { Box = Make(input) };
        var handlers = new Dictionary<Type, BoxFactory>();
        handlers.TryAdd(typeof(DelegateBox), new BoxFactory(receiver.Get));
        handlers.TryAdd(typeof(ResultReceiver), new BoxFactory(Make));
        if (!handlers.TryGetValue(typeof(DelegateBox), out var handler)) return -1;
        var result = handler(mode);
        handlers.TryGetValue(typeof(ResultReceiver), out var factory);
        var pressure = factory(1);
        return unchecked(result.Value + pressure.Value + (result == receiver.Box ? 19 : 23));
    }
    // The array exists only in a delegate signature, with no target body or invocation.
    public static int SignatureOnlyArray(int input, int mode)
    {
        ResultFactory<DelegateBox[]> callback = null;
        return callback == null ? input : mode;
    }
    public static DelegateBox Echo(DelegateBox box, int mode)
    {
        var pressure = Make(mode);
        box.Value = unchecked(box.Value + pressure.Value);
        return box;
    }
    public static int ReferenceArgument(int input, int mode)
    {
        var callback = new ReferenceResult(Echo);
        var original = Make(input);
        var result = callback(original, mode);
        var pressure = Make(1);
        return unchecked(result.Value + pressure.Value + (original == result ? 29 : 31));
    }
    public static int UnsupportedCovariance(int input, int mode)
    {
        var callback = new ObjectFactory(Make);
        return ((DelegateBox)callback(input)).Value;
    }
    public static ulong Wide(int value) => (ulong)(uint)value;
    public static int UnsupportedUnsigned(int input, int mode) => (int)new WideFactory(Wide)(input);
    public static DictionaryLeaf Value(int value) => default;
    public static int ValueResult(int input, int mode) => (int)new ValueFactory(Value)(input).Number;
    public static ref int ByRef(DelegateBox value) => ref value.Value;
    public static int UnsupportedByRef(int input, int mode) => new ByRefFactory(ByRef)(Make(input));
    public static RecursiveFactory Recursive(int value) => null;
    public static int UnsupportedRecursive(int input, int mode) => new RecursiveFactory(Recursive)(input) == null ? input : mode;
}
}
