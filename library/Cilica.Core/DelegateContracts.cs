// Cilica-owned contract. Construction/Invoke use the existing checked
// single-target delegate representation; only admitted closed signatures run.
namespace System
{
    public delegate void Action<in T>(T value);
    public delegate TResult Func<in T, out TResult>(T value);
    public delegate TResult Func<in T1, in T2, out TResult>(T1 first, T2 second);
    public delegate bool Predicate<in T>(T value);
    public delegate int Comparison<in T>(T left, T right);
    public delegate void Action<in T1, in T2>(T1 first, T2 second);
}
