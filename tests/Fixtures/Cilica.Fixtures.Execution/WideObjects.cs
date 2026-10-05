using System;

namespace Cilica.Fixtures.Execution
{
    public sealed class WideCell
    {
        public long Number;
        public readonly long Initial;
        public WideCell(long number) { Number = number; Initial = number; }
    }
    public sealed class WideGenericCell<T> where T : struct
    {
        public T Number;
        public WideGenericCell(T number) { Number = number; }
    }
    public struct WidePayload { public long Number; public object Reference; }
    public struct WideOuter { public int Marker; public WidePayload Nested; }
    public static class WideObjects
    {
        private static long Seed(int input) => ((long)input << 32) | unchecked((uint)(input ^ 0x12345678));
        public static int Heap(int input, int delta)
        {
            var cell = new WideCell(Seed(input));
            Mutate(ref cell.Number, unchecked((long)delta * 0x100000001L));
            return unchecked((int)(cell.Number ^ (cell.Number >> 32) ^ cell.Initial));
        }
        private static void Mutate(ref long value, long delta) { _ = new WideCell(delta); value = unchecked(value + delta); }
        private static WideOuter Make(long number)
        {
            return new WideOuter { Marker = 7, Nested = new WidePayload { Number = number, Reference = new int[1] } };
        }
        public static T Echo<T>(T value) => value;
        public static int Values(int input, int delta)
        {
            var value = Echo(Make(Seed(input))); object boxed = value;
            var copy = (WideOuter)boxed;
            Mutate(ref copy.Nested.Number, unchecked((long)delta * 0x100000001L));
            var original = (WideOuter)boxed;
            long bits = copy.Nested.Number ^ original.Nested.Number;
            return unchecked((int)(bits ^ (bits >> 32))) + copy.Marker
                + (copy.Nested.Reference == original.Nested.Reference ? 1 : 0);
        }
        public static int Exceptions(int input, int mode)
        {
            long value = Seed(input); int result;
            try { result = Calculation(ref value, mode); }
            catch (OverflowException) { result = 41; }
            catch (DivideByZeroException) { result = 53; }
            finally { value ^= 0x100000001L; }
            return unchecked(result ^ (int)(value >> 32) ^ (int)value);
        }
        private static int Calculation(ref long value, int mode)
        {
            _ = new WideCell(value);
            if (mode == 0) value = checked(value + long.MaxValue);
            else if (mode == 1) value /= mode - 1;
            else if (mode == 2) return checked((int)value);
            else value = unchecked(-value) >> (mode & 63);
            return unchecked((int)(value ^ (value >> 32)));
        }
        public static int Unsigned(int input, int mode)
        {
            var cell = new WideGenericCell<ulong>(((ulong)unchecked((uint)input) << 32) | unchecked((uint)mode));
            ulong value = cell.Number;
            if (value > 0x8000000000000000UL) value = ~value;
            try { cell.Number = checked(value * unchecked((uint)mode)); }
            catch (OverflowException) { cell.Number = ulong.MaxValue; }
            return unchecked((int)(cell.Number ^ (cell.Number >> 32)));
        }
    }
}
