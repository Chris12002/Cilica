using System;

namespace Cilica.Fixtures.Execution
{
    internal static class AssemblyHelpers
    {
        internal static int Adjust(int value, int delta) => unchecked(value + delta);
        public static WidePayload Copy(WidePayload value, ref long extra)
        {
            extra ^= value.Number;
            value.Reference = new WideCell(extra);
            value.Number = unchecked(value.Number + extra);
            return value;
        }
        internal static int Guard(int value, int mode)
        {
            if (mode == 0) throw new ArgumentNullException("mode");
            return Adjust(value, mode);
        }
        internal static class Nested
        {
            internal static long Wide(long value) => unchecked(value * 17);
        }
    }
    public static class AssemblyCalls
    {
        public static int Flow(int input, int mode)
        {
            long extra = input;
            var value = new WidePayload { Number = AssemblyHelpers.Nested.Wide(((long)input << 32) | (uint)mode), Reference = new WideCell(mode) };
            var copy = AssemblyHelpers.Copy(value, ref extra);
            return AssemblyHelpers.Adjust(unchecked((int)(copy.Number ^ extra)), mode) + (copy.Reference == value.Reference ? 3 : 5);
        }
        public static int Fault(int input, int mode)
        {
            try { return AssemblyHelpers.Guard(input, mode); }
            catch (ArgumentNullException fault) { return fault.ParamName == "mode" ? 7 : 11; }
        }
    }
}
