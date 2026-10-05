using System;

namespace Cilica.Fixtures.Execution
{
    public static class ClosedDispatchProbe
    {
        public static int Wide(int input, int mode)
        {
            IClosedDispatch<long> target;
            if (mode == 0) target = new ClosedDispatchOverride<long>();
            else if (mode == 1) target = new ClosedDispatchExplicit<long>();
            else if (mode == 2) target = new ClosedDispatchChain<long>();
            else target = new ClosedDispatchReimplemented<long>();
            long value = ((long)input << 32) | unchecked((uint)mode);
            long echoed = target.Echo(value);
            return unchecked((int)(echoed ^ (echoed >> 32)) + target.Code() + WideObjects.Values(input, mode));
        }
        public static int References(int input, int other)
        {
            IClosedDispatch<object> target = new ClosedDispatchExplicit<object>();
            object original = new WideCell(input);
            object copy = target.Echo(original);
            IClosedDispatch<object> next = new ClosedDispatchBase<object>();
            return (next.Echo(copy) == original ? input : other) ^ target.Code();
        }
        public static int Faults(int input, int divisor)
        {
            IClosedDispatch<long> target = null;
            try { return unchecked((int)target.Echo(12L / divisor)); }
            catch (DivideByZeroException) { return input ^ 53; }
            catch (NullReferenceException) { return input ^ 41; }
        }
        public static int SameWidth(int input, int other)
        {
            var both = new ClosedDispatchDual();
            IClosedDispatch<int> signed = both;
            IClosedDispatch<long> wide = both;
            IClosedDispatch<uint> unsigned = both;
            return unchecked(signed.Echo(input) + (int)wide.Echo(other) + (int)unsigned.Echo((uint)input) + signed.Code() + wide.Code() + unsigned.Code());
        }
        public static int ValueSignature(int input, int other)
        {
            IClosedDispatch<WideOuter> target = new ClosedDispatchBase<WideOuter>();
            return target.Echo(default).Marker;
        }
        public static int VariantCall(int input, int other)
        {
            IClosedProducer<object> target = new ClosedProducer<string>();
            return target.Get() == null ? input : other;
        }
        public static int ClassVirtual(int input, int other)
        {
            ClosedDispatchBase<long> target = new ClosedDispatchOverride<long>();
            return target.Code();
        }
    }
}
