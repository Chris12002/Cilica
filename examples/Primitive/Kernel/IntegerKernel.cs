namespace Cilica.Examples.Primitive;

public static class IntegerKernel
{
    public static int Calculate(int initial, int step, int count)
    {
        int result = initial;
        for (int index = 0; index < count; index++) result = Step(result, step);
        return result;
    }

    public static int Step(int value, int step) => checked(value + step);
}
