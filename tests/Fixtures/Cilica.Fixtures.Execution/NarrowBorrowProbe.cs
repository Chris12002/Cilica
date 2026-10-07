using System;
namespace Cilica.Fixtures.Execution
{
public static class NarrowBorrowProbe
{
    public static int Aliases(int value, int mode)
    {
        short first = (short)value, second = (short)mode;
        return Touch(ref first, ref first, value, mode) + first + Touch(ref first, ref second, value, mode) + second;
    }
    private static int Touch(ref short first, ref short second, int value, int mode)
    {
        first = (short)(value + mode);
        int prior = second;
        second = (short)(value ^ mode);
        return prior + first;
    }
    public static int Fault(int value, int mode)
    {
        bool result = value != 0;
        try { WriteAndThrow(ref result, mode); }
        catch (InvalidOperationException e) { return (result ? 41 : 17) + e.Message.Length; }
        return -1;
    }
    private static void WriteAndThrow(ref bool result, int mode)
    {
        result = mode != 0;
        throw new InvalidOperationException("after write");
    }
    public static int Scoped(int value, int mode)
    {
        byte data = (byte)value;
        ref byte alias = ref data;
        for (int i = 0; i < 3; i++) alias = (byte)(alias + mode);
        return data;
    }
}

}
