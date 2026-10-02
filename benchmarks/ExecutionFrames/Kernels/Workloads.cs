namespace Cilica.Benchmarks.ExecutionFrames;

// Authored inputs, shared by the independent .NET reference process and the
// compiler's metadata reader. No arbitrary assembly is executed by the harness.
public static class Workloads
{
    public static int Arithmetic(int seed, int count) => unchecked(((seed * 1664525 + 1013904223) ^ (seed >> 13)) + count);

    public static int Loop(int seed, int count)
    {
        int value = seed;
        for (int i = 0; i < count; i++) value = unchecked((value * 1664525 + 1013904223) ^ (value >> 13));
        return value;
    }

    public static int Calls(int seed, int count)
    {
        int value = seed;
        for (int i = 0; i < count; i++) value = Step(value, i);
        return value;
    }

    private static int Step(int value, int index) => unchecked((value * 1664525 + 1013904223) ^ (value >> 13) ^ index);
}
