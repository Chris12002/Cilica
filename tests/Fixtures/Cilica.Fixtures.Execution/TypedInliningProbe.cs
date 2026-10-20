namespace Cilica.Fixtures.Execution
{
public static class TypedInliningProbe
{
    public static int Depth1(int value) => unchecked(value + 1);
    public static int Depth2(int value) => Depth1(value);
    public static int Depth3(int value) => Depth2(value);
    public static int Depth4(int value) => Depth3(value);
    public static int Depth5(int value) => Depth4(value);
    public static int Depth6(int value) => Depth5(value);
    public static int Depth7(int value) => Depth6(value);
    public static int Depth8(int value) => Depth7(value);
    public static int Depth9(int value) => Depth8(value);
    public static int Fan0(int value) => unchecked(value * 3 + 7);
    public static int Fan1(int value) => unchecked(Fan0(value) + Fan0(value));
    public static int Fan2(int value) => unchecked(Fan1(value) + Fan1(value));
    public static int Fan3(int value) => unchecked(Fan2(value) + Fan2(value));
    public static int Fan4(int value) => unchecked(Fan3(value) + Fan3(value));
    public static int Fan5(int value) => unchecked(Fan4(value) + Fan4(value));
    public static int Fan6(int value) => unchecked(Fan5(value) + Fan5(value));
    public static int Recursive(int value, int count) => count == 0 ? value : unchecked(1 + Recursive(value, count - 1));
    public static int Run(int a, int b) => unchecked(Depth9(a) + Fan6(b) + Recursive(a, b & 3));
}
}
