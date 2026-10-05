using System;

namespace Cilica.Fixtures.Execution
{
    public struct NarrowConstructed
    {
        public readonly long Number;
        public readonly int Code;
        public NarrowConstructed(short first, byte second, char third, bool fourth)
        {
            Number = ((long)first << 32) | third;
            Code = second + (fourth ? 17 : 23);
            _ = new WideCell(Number);
        }
    }
    public static class NarrowObjects
    {
        private static T Echo<T>(T value) => value;
        public static int Composed(int input, int count)
        {
            var value = new NarrowConstructed(unchecked((short)input), Echo(unchecked((byte)count)), unchecked((char)input), count != 0);
            return unchecked((int)(value.Number ^ (value.Number >> 32)) + value.Code
                + NarrowScalars.SignedByte(input, count) + NarrowScalars.UnsignedByte(input, count)
                + NarrowScalars.SignedShort(input, count) + NarrowScalars.UnsignedShort(input, count)
                + NarrowScalars.Character(input, count) + NarrowScalars.Boolean(input, count));
        }
        public static int Checked(int input, int count)
        {
            try { return NarrowScalars.CheckedReturn(input, count); }
            catch (OverflowException) { return 71; }
        }
        private static void Change(ref byte value) { value++; }
        public static int Borrow(int input, int count) { byte value = unchecked((byte)input); Change(ref value); return value + count; }
    }
}
