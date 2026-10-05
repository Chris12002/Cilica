using System;

namespace Cilica.Fixtures.Execution
{
    public delegate T ContractFunction<out T>(int input);
    public delegate int TaggedFunction<T>(int input);
    public sealed class ActionReceiver
    {
        public int Value;
        public Action<int, int> Back;
        public void Set(int first, int second) { Value = first / second; }
    }
    public static class GenericDelegateProbe
    {
        public static int AddOne(int input) => unchecked(input + 1);
        public static int SubtractOne(int input) => unchecked(input - 1);
        public static void Check(int input, int mode)
        {
            if (input == mode) throw new ArgumentException("equal", "input");
        }
        public static int Capture(int input, int mode)
        {
            var receiver = new ActionReceiver();
            var callable = new Action<int, int>(receiver.Set);
            receiver.Back = callable;
            string pressure = string.Concat("keep", "capture");
            try { receiver.Back(input, mode); return receiver.Value + pressure.Length; }
            catch (DivideByZeroException) { return 11; }
            catch (OverflowException) { return 17; }
        }
        public static int Static(int input, int mode)
        {
            var callable = new Action<int, int>(Check);
            try { callable(input, mode); return input; }
            catch (ArgumentException error) { return error.ParamName.Length; }
        }
        public static int Return(int input, int mode)
        {
            ContractFunction<int> callable = mode < 0 ? new ContractFunction<int>(AddOne) : new ContractFunction<int>(SubtractOne);
            return callable(input);
        }
        public static int ExactTypes(int input, int mode)
        {
            object first = new TaggedFunction<int>(AddOne);
            object second = new TaggedFunction<uint>(SubtractOne);
            return (first is TaggedFunction<uint> ? 1 : 3) + (second is TaggedFunction<int> ? 5 : 7)
                + ((TaggedFunction<int>)first)(input) + ((TaggedFunction<uint>)second)(mode);
        }
        public static int Null(int input, int mode)
        {
            Action<int, int> callable = null;
            try { callable(input / mode, mode); return 0; }
            catch (DivideByZeroException) { return 11; }
            catch (OverflowException) { return 17; }
            catch (NullReferenceException) { return 23; }
        }
        public static int InvalidCast(int input, int mode)
        {
            object value = new TaggedFunction<int>(AddOne);
            try { return ((TaggedFunction<uint>)value)(input); }
            catch (InvalidCastException error) { return error.Message.Length + mode; }
        }
        public static int ReferenceParameter(int input, int mode)
        {
            var callable = new Action<string, int>(ReferenceTarget);
            callable("text", input);
            return mode;
        }
        public static void ReferenceTarget(string value, int input) { if (value == null) throw new ArgumentException(); }
        public static int UnsupportedWide(int input, int mode)
        {
            var callable = new Action<long, int>(WideTarget);
            callable(input, mode);
            return mode;
        }
        public static void WideTarget(long value, int input) { if (value == input) throw new ArgumentException(); }
    }
}
