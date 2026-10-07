using System;
namespace Cilica.Fixtures.Execution
{
    public delegate long WideBinary(long left, long right);
    public delegate T WideGeneric<T>(long value, T other);
    public sealed class WideDelegateReceiver
    {
        public long Value;
        public Func<long, long> Back;
        public long Update(long value) { var pressure = new WideDelegateReceiver { Value = value }; Value = unchecked(Value + pressure.Value); return Value; }
        public void Set(long value) { Value = new WideDelegateReceiver { Value = value }.Value; }
    }
    public static class WideDelegateProbe
    {
        private static long Join(int a, int b) => ((long)a << 32) | (uint)b;
        private static int Fold(long value) => unchecked((int)value * 31 + (int)(value >> 32));
        public static long Mix(long left, long right) => unchecked((left ^ long.MinValue) + right);
        public static int Static(int a, int b)
        {
            var callback = new WideBinary(Mix); long x = Join(a,b), y = Join(b,a);
            long value = callback(x,y); return value == Mix(x,y) ? Fold(value) : 12345;
        }
        public static int Capture(int a, int b)
        {
            var receiver = new WideDelegateReceiver { Value = Join(a,b) };
            receiver.Back = new Func<long,long>(receiver.Update);
            long first = receiver.Back(long.MinValue), second = receiver.Back(Join(b,a));
            return first == unchecked(Join(a,b) + long.MinValue) && second == receiver.Value ? Fold(second) : 9876;
        }
        public static int Action(int a, int b)
        {
            var receiver = new WideDelegateReceiver(); var callback = new Action<long>(receiver.Set);
            callback(Join(a,b)); var pressure = new WideDelegateReceiver { Value = b };
            return receiver.Value == Join(a,b) ? unchecked(Fold(receiver.Value) + (int)pressure.Value) : 2468;
        }
        public static long Read(WideDelegateReceiver receiver) { var pressure = new WideDelegateReceiver { Value = 1 }; return unchecked(receiver.Value + pressure.Value); }
        public static int ReferenceArgument(int a, int b)
        {
            var receiver = new WideDelegateReceiver { Value = Join(a,b) }; var callback = new Func<WideDelegateReceiver,long>(Read);
            long result = callback(receiver); return result == unchecked(Join(a,b)+1) ? Fold(result) : 1357;
        }
        public static WideDelegateReceiver Make(long value) => new WideDelegateReceiver { Value = value };
        public static int ReferenceResult(int a, int b)
        {
            var callback = new Func<long,WideDelegateReceiver>(Make); var result = callback(Join(a,b));
            var pressure = callback(long.MaxValue); return result.Value == Join(a,b) ? unchecked(Fold(result.Value)+(int)pressure.Value) : 3579;
        }
        public static long Choose(long value, long other) => value < other ? value : other;
        public static int Generic(int a, int b)
        {
            var callback = new WideGeneric<long>(Choose); long result = callback(Join(a,b),Join(b,a));
            return result == Choose(Join(a,b),Join(b,a)) ? Fold(result) : 4680;
        }
        public static long Divide(long value, long divisor) => value / divisor;
        public static int Faults(int a, int b)
        {
            var callback = b == 2 ? null : new Func<long,long,long>(Divide);
            try { return Fold(callback(a == 0 ? long.MinValue : Join(a,b), b)); }
            catch (DivideByZeroException) { return 101; }
            catch (OverflowException) { return 103; }
            catch (NullReferenceException) { return 107; }
        }
        public static int NullTarget(int a, int b)
        {
            WideDelegateReceiver receiver = null;
            try { var callback = new Func<long,long>(receiver.Update); return Fold(callback(Join(a,b))); }
            catch (ArgumentException) { return unchecked(a+b); }
        }
    }
}
