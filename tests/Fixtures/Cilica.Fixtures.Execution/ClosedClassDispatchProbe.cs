using System;

namespace Cilica.Fixtures.Execution
{
    public class AllocatingClassDispatch<T> : ClosedDispatchBase<T>
    {
        public override T Echo(T value)
        {
            IClosedDispatch<T> nested = new ClosedDispatchExplicit<T>();
            return nested.Echo(value);
        }
        public override int Code()
        {
            ClosedDispatchNewSlot<long> nested = new ClosedDispatchFinal<long>();
            return nested.Code() + base.Code();
        }
    }
    public static class ClosedClassDispatchProbe
    {
        public static int Wide(int input, int mode)
        {
            ClosedDispatchBase<long> target;
            if (mode == 0) target = new ClosedDispatchOverride<long>();
            else if (mode == 1) target = new ClosedDispatchFinal<long>();
            else target = new AllocatingClassDispatch<long>();
            long value = ((long)input << 32) | unchecked((uint)mode);
            long echoed = target.Echo(value);
            return unchecked((int)(echoed ^ (echoed >> 32)) + target.Code() + WideObjects.Values(input, mode));
        }
        public static int Slots(int input, int mode)
        {
            var leaf = new ClosedDispatchFinal<long>();
            ClosedDispatchBase<long> original = leaf;
            ClosedDispatchNewSlot<long> hidden = leaf;
            var final = new ClosedDispatchChain<long>();
            return unchecked(input + original.Code() * 10 + hidden.Code() + final.Code() + mode);
        }
        public static int References(int input, int other)
        {
            ClosedDispatchBase<object> target = new AllocatingClassDispatch<object>();
            object original = new WideCell(input);
            object copy = target.Echo(original);
            return copy == original ? input ^ target.Code() : other;
        }
        public static int Faults(int input, int divisor)
        {
            ClosedDispatchBase<long> target = null;
            try { return unchecked((int)target.Echo(12L / divisor)); }
            catch (DivideByZeroException) { return input ^ 53; }
            catch (NullReferenceException) { return input ^ 41; }
        }
        public static int SameWidth(int input, int mode)
        {
            ClosedDispatchBase<int> signed = new AllocatingClassDispatch<int>();
            ClosedDispatchBase<uint> unsigned = new AllocatingClassDispatch<uint>();
            return unchecked(signed.Echo(input) + (int)unsigned.Echo((uint)mode) + signed.Code() + unsigned.Code());
        }
        public static int ValueSignature(int input, int mode)
        {
            ClosedDispatchBase<WideOuter> target = new ClosedDispatchBase<WideOuter>();
            return target.Echo(default).Marker;
        }
    }
}
