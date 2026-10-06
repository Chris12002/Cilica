using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class OpaqueGenericHolder<T>
    {
        public T Value;
        public OpaqueGenericHolder(T value) { Value = value; }
        public T Read() => Value;
    }
    public static class OpaqueGenericBridge
    {
        public static T Echo<T>(T value) => value;
        public static T[] Array<T>(T value) { var values = new T[2]; values[0] = value; return values; }
    }
    public static class PrivateGenericProbe
    {
        private sealed class Context
        {
            public int Value;
            public Context(int value) { Value = value; }
        }
        private static Context Echo(Context value) => value;
        public static int Copy(int a, int b) { var value = OpaqueGenericBridge.Echo(new Context(a)); return unchecked(value.Value + b); }
        public static int Holder(int a, int b) { var holder = new OpaqueGenericHolder<Context>(new Context(a)); var copy = holder.Read(); holder.Value = new Context(b); return unchecked(copy.Value + holder.Value.Value); }
        public static int Arrays(int a, int b) { var values = OpaqueGenericBridge.Array(new Context(a)); values[1] = new Context(b); return unchecked(values[0].Value + values[1].Value); }
        public static int Delegate(int a, int b) { var call = new Func<Context, Context>(Echo); return unchecked(call(new Context(a)).Value + b); }
    }
}
