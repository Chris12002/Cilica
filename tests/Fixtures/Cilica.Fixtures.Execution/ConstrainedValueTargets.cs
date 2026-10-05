using System;

namespace Cilica.Fixtures.Execution
{
    public interface IDirectConstrainedValue<T> { T Change(T next); }
    public struct DirectConstrainedValue<T> : IDirectConstrainedValue<T>
    {
        public T Item;
        public T Change(T next) { var before = Item; Item = next; return before; }
        public override int GetHashCode() => 41;
    }
    public struct ExplicitConstrainedValue : IDirectConstrainedValue<int>
    {
        public int Number;
        int IDirectConstrainedValue<int>.Change(int next) { int before = Number; Number = next; return before; }
        public override int GetHashCode() => Number;
    }
    public struct ConstrainedWithoutOverride { public int Number; }
    public sealed class ConstrainedClass : IDirectConstrainedValue<int>
    {
        public int Change(int next) => next;
    }
}
