// Cilica-owned declarations matching the pinned read-only collection contracts.
// Implementations remain ordinary managed code. Runtime vector relationships
// require selected identities; executable array helpers need separate grants.
namespace System
{
    public interface IDisposable { void Dispose(); }
}

namespace System.Collections
{
    public interface IEnumerable { IEnumerator GetEnumerator(); }
    public interface IEnumerator
    {
        object Current { get; }
        bool MoveNext();
        void Reset();
    }
}

namespace System.Collections.Generic
{
    public interface IEnumerable<out T> : Collections.IEnumerable
    {
        new IEnumerator<T> GetEnumerator();
    }
    public interface IEnumerator<out T> : Collections.IEnumerator, IDisposable
    {
        new T Current { get; }
    }
    public interface IReadOnlyCollection<out T> : IEnumerable<T>
    {
        int Count { get; }
    }
    public interface IReadOnlyList<out T> : IReadOnlyCollection<T>
    {
        T this[int index] { get; }
    }
}
