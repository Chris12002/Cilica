using System;
namespace Cilica.Fixtures.Execution
{
    public static class ValueArrayVisibilityProbe
    {
        private struct Secret { public HeapLeaf Leaf; }
        private readonly struct ReadonlySecret
        {
            public readonly int Number;
            public readonly HeapPayload Payload;
            public ReadonlySecret(int number, HeapPayload payload) { Number = number; Payload = payload; }
        }
        private class Nest { public struct Child { public int Number; public HeapPayload Payload; } }
        private static void Pressure() { for (int i = 0; i < 8; ++i) { var node = new HeapPayload(i); node.Number++; } }
        public static int Internal(int a, int b)
        {
            var values = new HiddenValuePayload[2]; values[0].Number = a; values[0].Reference = new ValueVectorBox(b);
            values[1] = values[0]; values[0].Number = b; Pressure();
            return unchecked(values[1].Number + values[0].Number + values[1].Reference.Number);
        }
        public static int Private(int a, int b)
        {
            var values = new Secret[2]; values[0].Leaf.Number = a; values[0].Leaf.Payload = new HeapPayload(b);
            values[1] = values[0]; values[0].Leaf.Number = b; Pressure();
            return unchecked((int)values[1].Leaf.Number + (int)values[0].Leaf.Number + values[1].Leaf.Payload.Number);
        }
        public static int Nested(int a, int b)
        {
            var values = new Nest.Child[1]; values[0].Number = a; values[0].Payload = new HeapPayload(b); Pressure();
            return unchecked(values[0].Number + values[0].Payload.Number);
        }
        public static int Readonly(int a, int b)
        {
            var values = new ReadonlySecret[1]; values[0] = new ReadonlySecret(a, new HeapPayload(b)); Pressure();
            return unchecked(values[0].Number + values[0].Payload.Number);
        }
        public static int Empty(int a, int b)
        {
            var first = Array.Empty<Secret>(); var second = Array.Empty<Secret>(); var other = Array.Empty<ReadonlySecret>(); Pressure();
            return unchecked(a + b + (first == second ? 17 : 31) + ((object)first == other ? 43 : 47));
        }
        public static int Bounds(int a, int b)
        {
            try { var values = new Secret[a < b ? -1 : 1]; values[0].Leaf.Payload = new HeapPayload(b); return values[0].Leaf.Payload.Number; }
            catch (OverflowException) { return a; }
        }
    }
}
