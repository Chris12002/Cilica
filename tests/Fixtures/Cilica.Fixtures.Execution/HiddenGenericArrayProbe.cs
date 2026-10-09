using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class HiddenGenericArrayProbe
    {
        private struct Payload { public int Number; public ValueVectorBox Node; }
        private struct Container<T> { public T Item; }
        private struct Nothing { }
        private enum Choice { First = -7, Second = 19 }
        private static Payload Make(int a, int b) { var value = new Payload(); value.Number = a; value.Node = new ValueVectorBox(b); return value; }
        private static void Pressure() { for (int i = 0; i < 8; ++i) { var value = new ValueVectorBox(i); value.Number++; } }
        public static int Arrays(int a, int b)
        {
            var value = Make(a, b); var values = OpaqueGenericBridge.Array(value);
            value.Number = 37; Pressure(); values[1] = Make(b, a);
            return unchecked(values[0].Number + values[0].Node.Number * 3 + values[1].Number * 5 + values[1].Node.Number);
        }
        public static int OwnerArray(int a, int b)
        {
            var holder = new OpaqueGenericHolder<Payload>(Make(a, b)); var values = holder.Snapshot();
            holder.Value = Make(b, a); Pressure(); return unchecked(values[0].Number + values[0].Node.Number + holder.Value.Number * 7);
        }
        public static int Empty(int a, int b)
        {
            var one = OpaqueGenericBridge.Empty<Payload>(); var two = OpaqueGenericBridge.Empty<Payload>();
            var other = OpaqueGenericBridge.Empty<Container<Payload>>(); Pressure();
            return unchecked(a + b + (one == two ? 3 : 17) + ((object)one == other ? 31 : 7));
        }
        public static int Bounds(int a, int b)
        {
            try { var values = OpaqueGenericBridge.Sized(Make(a, b), a < b ? -1 : 2); Pressure(); return unchecked(values[0].Number + values[0].Node.Number); }
            catch (OverflowException) { return unchecked(a - b); }
        }
        public static int List(int a, int b)
        {
            var list = new List<Payload>(); for (int i = 0; i < 7; ++i) list.Add(Make(unchecked(a + i), b));
            var values = list.ToArray(); list.Clear(); Pressure(); return unchecked(values[0].Number + values[6].Node.Number + values[6].Number);
        }
        private static IEnumerable<Payload> Values(int a, int b) { yield return Make(a, b); yield return Make(b, a); }
        public static int Materialize(int a, int b)
        { var values = Values(a, b).ToArray(); Pressure(); return unchecked(values[0].Number + values[1].Node.Number + values.Length); }
        public static int Constructed(int a, int b)
        {
            var value = new Container<ValueVectorBox>(); value.Item = new ValueVectorBox(a);
            var values = OpaqueGenericBridge.Array(value); value.Item = new ValueVectorBox(b); Pressure();
            return unchecked(values[0].Item.Number + value.Item.Number + (values[1].Item == null ? 17 : 31));
        }
        public static int Enum(int a, int b)
        {
            var values = OpaqueGenericBridge.Array((Choice)a); values[1] = (Choice)b;
            var one = OpaqueGenericBridge.Empty<Choice>(); var two = OpaqueGenericBridge.Empty<Choice>(); Pressure();
            return unchecked((int)values[0] + (int)values[1] + (one == two ? 17 : 31));
        }
        public static int EmptyValue(int a, int b)
        {
            var values = OpaqueGenericBridge.Array(new Nothing()); Pressure();
            var one = OpaqueGenericBridge.Empty<Nothing>(); var two = OpaqueGenericBridge.Empty<Nothing>();
            return unchecked(a + b + values.Length + (one == two ? 11 : 19));
        }
    }
}
