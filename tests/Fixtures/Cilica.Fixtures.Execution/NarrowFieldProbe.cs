namespace Cilica.Fixtures.Execution
{
    public sealed class NarrowFieldBox
    {
        public sbyte SignedByte; public byte Byte; public short Short; public ushort UnsignedShort; public char Char; public bool Bool;
        public NarrowFieldValue Value;
    }
    public struct NarrowFieldValue { public sbyte SignedByte; public char Char; public bool Bool; }
    public static class NarrowFieldStatics
    {
        public static sbyte SignedByte; public static byte Byte; public static short Short; public static ushort UnsignedShort; public static char Char; public static bool Bool;
    }
    public static class NarrowFieldProbe
    {
        public static int Copy(int input, int mode)
        {
            var box = new NarrowFieldBox();
            box.Value.SignedByte = (sbyte)input; box.Value.Char = (char)mode; box.Value.Bool = input != 0;
            var copy = box.Value; box.Value = default(NarrowFieldValue);
            return copy.SignedByte + copy.Char + (copy.Bool ? 1 : 0) + box.Value.Char;
        }
    }
}
