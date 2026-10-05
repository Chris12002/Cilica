namespace Cilica.Fixtures.Execution
{
    public static class NarrowScalars
    {
        public static int SignedByte(int input, int count)
        {
            sbyte value = unchecked((sbyte)input);
            for (int i = 0; i < (count & 7); ++i) value = Increment(value);
            return value;
        }
        private static sbyte Increment(sbyte value) => unchecked((sbyte)(value + 79));
        public static int UnsignedByte(int input, int count)
        {
            byte value = unchecked((byte)input);
            for (int i = 0; i < (count & 7); ++i) value = Increment(value);
            return value;
        }
        private static byte Increment(byte value) => unchecked((byte)(value + 179));
        public static int SignedShort(int input, int count)
        {
            short value = unchecked((short)input);
            for (int i = 0; i < (count & 7); ++i) value = Increment(value);
            return value;
        }
        private static short Increment(short value) => unchecked((short)(value + 17999));
        public static int UnsignedShort(int input, int count)
        {
            ushort value = unchecked((ushort)input);
            for (int i = 0; i < (count & 7); ++i) value = Increment(value);
            return value;
        }
        private static ushort Increment(ushort value) => unchecked((ushort)(value + 37999));
        public static int Character(int input, int count)
        {
            char value = unchecked((char)input);
            for (int i = 0; i < (count & 7); ++i) value = Increment(value);
            return value;
        }
        private static char Increment(char value) => unchecked((char)(value + 37999));
        public static int Boolean(int input, int count)
        {
            bool value = input != 0;
            for (int i = 0; i < (count & 7); ++i) value = Toggle(value);
            return value ? 1 : 0;
        }
        private static bool Toggle(bool value) => !value;
        public static int CheckedReturn(int input, int unused) => CheckedByte(input);
        private static byte CheckedByte(int value) => checked((byte)value);
    }
}
