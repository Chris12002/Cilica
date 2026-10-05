namespace Cilica.Fixtures.Execution
{
    public interface IClosedDispatch<T> { T Echo(T value); int Code(); }
    public interface IClosedDispatchChild<T> : IClosedDispatch<T> { }
    public class ClosedDispatchBase<T> : IClosedDispatchChild<T>
    {
        public virtual T Echo(T value) => value;
        public virtual int Code() => 11;
    }
    public class ClosedDispatchOverride<T> : ClosedDispatchBase<T>
    {
        public override T Echo(T value) => value;
        public override int Code() => 22;
    }
    public sealed class ClosedDispatchHidden<T> : ClosedDispatchBase<T>
    {
        public new T Echo(T value) => value;
        public new int Code() => 33;
    }
    public sealed class ClosedDispatchReimplemented<T> : ClosedDispatchBase<T>, IClosedDispatch<T>
    {
        public new T Echo(T value) => value;
        public new int Code() => 44;
    }
    public class ClosedDispatchExplicit<T> : IClosedDispatch<T>
    {
        T IClosedDispatch<T>.Echo(T value) => value;
        int IClosedDispatch<T>.Code() => 55;
    }
    public sealed class ClosedDispatchExplicitChild<T> : ClosedDispatchExplicit<T> { }
    public sealed class ClosedDispatchReexplicit<T> : ClosedDispatchExplicit<T>, IClosedDispatch<T>
    {
        T IClosedDispatch<T>.Echo(T value) => value;
        int IClosedDispatch<T>.Code() => 66;
    }
    public sealed class ClosedDispatchConcrete : ClosedDispatchOverride<long> { }
    public sealed class ClosedDispatchDual : IClosedDispatch<int>, IClosedDispatch<long>
    {
        int IClosedDispatch<int>.Echo(int value) => value;
        long IClosedDispatch<long>.Echo(long value) => value;
        int IClosedDispatch<int>.Code() => 77;
        int IClosedDispatch<long>.Code() => 88;
    }
    public sealed class ClosedDispatchReference<T> : IClosedDispatch<T> where T : class
    {
        public T Echo(T value) => value;
        public int Code() => 99;
    }
    public interface IClosedProducer<out T> { T Get(); }
    public sealed class ClosedProducer<T> : IClosedProducer<T>
    {
        public T Get() => default!;
    }
}
