using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class OpaqueGenericHolder<T>
    {
        public T Value;
        public OpaqueGenericHolder(T value) { Value = value; }
        public T Read() => Value;
        public T[] Snapshot() { var copy = new T[1]; copy[0] = Value; return copy; }
    }
    public static class OpaqueGenericBridge
    {
        public static T[] Empty<T>() => System.Array.Empty<T>();
        public static T[] Sized<T>(T value, int length) { var values = new T[length]; if (length != 0) values[0] = value; return values; }
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
        private sealed class OtherContext { }
        private static Context Echo(Context value) => value;
        public static int Copy(int a, int b) { var value = OpaqueGenericBridge.Echo(new Context(a)); return unchecked(value.Value + b); }
        public static int Holder(int a, int b) { var holder = new OpaqueGenericHolder<Context>(new Context(a)); var copy = holder.Read(); holder.Value = new Context(b); return unchecked(copy.Value + holder.Value.Value); }
        public static int Arrays(int a, int b) { var values = OpaqueGenericBridge.Array(new Context(a)); values[1] = new Context(b); return unchecked(values[0].Value + values[1].Value); }
        public static int List(int a, int b) { var values = new List<Context>(); values.Add(new Context(a)); values.Add(new Context(b)); var copy = values.ToArray(); values.Clear(); return unchecked(copy[0].Value + copy[1].Value); }
        public static int OwnerArray(int a, int b) { var holder = new OpaqueGenericHolder<Context>(new Context(a)); var copy = holder.Snapshot(); holder.Value = new Context(b); return unchecked(copy[0].Value + holder.Read().Value); }
        public static int Empty(int a, int b) { var one = OpaqueGenericBridge.Empty<Context>(); var two = OpaqueGenericBridge.Empty<Context>(); var other = OpaqueGenericBridge.Empty<OtherContext>(); return unchecked(a + b + (one == two ? 3 : 17) + ((object)one == other ? 31 : 7)); }
        public static int Bounds(int a, int b) { try { var copy = OpaqueGenericBridge.Sized(new Context(b), (a & 1) == 0 ? -1 : 2); return unchecked(copy[0].Value + a); } catch (OverflowException) { return unchecked(b - a); } }
        public static int Delegate(int a, int b) { var call = new Func<Context, Context>(Echo); return unchecked(call(new Context(a)).Value + b); }
    }
}
