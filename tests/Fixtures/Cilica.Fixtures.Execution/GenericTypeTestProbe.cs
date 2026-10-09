using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class OpaqueTypeTestBridge
    {
        public static bool Is<T>(object value) => value is T;
        public static T As<T>(object value) where T : class => value as T;
        public static T Cast<T>(object value) => (T)value;
        public static bool IsArray<T>(object value) => value is T[];
        public static T[] CastArray<T>(object value) => (T[])value;
    }
    public sealed class OpaqueTypeTestOwner<T> where T : class
    { public T Read(object value) => value as T; }
    public static class GenericTypeTestProbe
    {
        private class Node { public int Value; public Node(int value) { Value = value; } }
        private sealed class Child : Node { public Child(int value) : base(value) { } }
        public sealed class Box<T> { public T Value; public Box(T value) { Value = value; } }
        private struct Payload { public int Value; public Node Reference; }
        private enum Tag { First = -3, Second = 7 }
        private static void Pressure() { for (int i = 0; i < 8; ++i) { var node = new Node(i); node.Value++; } }
        public static int IsAs(int a, int b)
        {
            object source = (a & 1) == 0 ? new Child(a) : new Node(b);
            var node = OpaqueTypeTestBridge.As<Node>(source); var child = OpaqueTypeTestBridge.As<Child>(source);
            Pressure(); return unchecked(node.Value + (OpaqueTypeTestBridge.Is<Child>(source) ? 3 : 7)
                + (child == null ? 11 : child.Value) + (OpaqueTypeTestBridge.Is<Node>(null) ? 17 : b));
        }
        public static int Cast(int a, int b)
        {
            object source = (a & 1) == 0 ? new Child(a) : new Node(b);
            try { var node = OpaqueTypeTestBridge.Cast<Child>(source); Pressure(); return unchecked(node.Value + b); }
            catch (InvalidCastException) { return unchecked(a - b); }
        }
        public static int NullCast(int a, int b)
        { var node = OpaqueTypeTestBridge.Cast<Node>(null); Pressure(); return unchecked(a + b + (node == null ? 3 : 7)); }
        public static int Owner(int a, int b)
        { var owner = new OpaqueTypeTestOwner<Node>(); var node = owner.Read(new Child(a)); Pressure(); return unchecked(node.Value + b); }
        public static int Constructed(int a, int b)
        {
            object source = new Box<Node>(new Child(a)); var box = OpaqueTypeTestBridge.Cast<Box<Node>>(source);
            Pressure(); return unchecked(box.Value.Value + b + (OpaqueTypeTestBridge.Is<Box<Child>>(source) ? 13 : 3));
        }
        public static int Vectors(int a, int b)
        {
            object source = new Child[] { new Child(a) }; var nodes = OpaqueTypeTestBridge.CastArray<Node>(source);
            Pressure(); return unchecked(nodes[0].Value + b + (OpaqueTypeTestBridge.IsArray<Node>(source) ? 3 : 17)
                + (OpaqueTypeTestBridge.IsArray<Child>(source) ? 7 : 19));
        }
        public static int ValueVectors(int a, int b)
        {
            var item = new Payload(); item.Value = a; item.Reference = new Node(b);
            object source = new Payload[] { item }; var values = OpaqueTypeTestBridge.CastArray<Payload>(source);
            Pressure(); return unchecked(values[0].Value + values[0].Reference.Value + (OpaqueTypeTestBridge.IsArray<Payload>(source) ? 3 : 17));
        }
        public static int EnumVectors(int a, int b)
        {
            object source = new Tag[] { (Tag)a, (Tag)b }; var values = OpaqueTypeTestBridge.CastArray<Tag>(source);
            Pressure(); return unchecked((int)values[0] + (int)values[1] + (OpaqueTypeTestBridge.IsArray<Tag>(source) ? 5 : 19));
        }
        public static int BoundVector(int a, int b)
        {
            var item = new Payload(); item.Value = a; item.Reference = new Node(b);
            object source = new Payload[] { item }; var values = OpaqueTypeTestBridge.Cast<Payload[]>(source);
            Pressure(); return unchecked(values[0].Value + values[0].Reference.Value + (OpaqueTypeTestBridge.Is<Payload[]>(source) ? 3 : 17));
        }
        public static int PrimitiveVectors(int a, int b)
        {
            object source = new int[] { a, b }; var values = OpaqueTypeTestBridge.CastArray<int>(source);
            Pressure(); return unchecked(values[0] + values[1] + (OpaqueTypeTestBridge.IsArray<int>(source) ? 3 : 17)
                + (OpaqueTypeTestBridge.IsArray<long>(source) ? 5 : 19));
        }
        public static int CastCollection(int a, int b)
        {
            var values = new List<object>(); values.Add(new Child(a)); values.Add(new Node(b));
            var nodes = values.Cast<Node>().ToArray(); Pressure(); return unchecked(nodes[0].Value + nodes[1].Value);
        }
        private static TypeSequence Sequence(int a, int b) => new TypeSequence(new Child(a), null, new Node(b), 0);
        public static int OfTypeSequence(int a, int b)
        { var nodes = Sequence(a, b).OfType<Node>().ToArray(); Pressure(); return unchecked(nodes[0].Value + nodes[1].Value); }
        public static int CastSequence(int a, int b)
        { var nodes = Sequence(a, b).Cast<Node>().ToArray(); Pressure(); return unchecked(nodes[0].Value + nodes[2].Value + (nodes[1] == null ? 3 : 17)); }
        public static int Skip(int a, int b)
        { var nodes = new Node[] { new Node(b), new Child(a) }; var result = nodes.Skip(1).ToArray(); Pressure(); return unchecked(result[0].Value + b); }
    }
}
