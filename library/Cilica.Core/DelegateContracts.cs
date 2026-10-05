// Cilica-owned contract. Construction/Invoke use the existing checked
// single-target delegate representation; only admitted closed signatures run.
namespace System
{
    public delegate void Action<in T1, in T2>(T1 first, T2 second);
}
