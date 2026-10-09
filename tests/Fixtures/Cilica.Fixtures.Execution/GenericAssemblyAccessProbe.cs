using System;
namespace Cilica.Fixtures.Execution
{
    internal class AssemblyNode { public int Value; public AssemblyNode(int value) { Value = value; } }
    internal sealed class AssemblyChild : AssemblyNode { public AssemblyChild(int value) : base(value) { } }
    internal struct AssemblyPayload { public int Value; public AssemblyNode Node; }
    internal class AssemblyContainer { public sealed class Node { public int Value; public Node(int value) { Value = value; } } }
    internal sealed class AssemblyGenericNode<T> { public T Value; public AssemblyGenericNode(T value) { Value = value; } }
    public static class GenericAssemblyCaller<T>
    {
        public static int Test(object source) { var node = source as AssemblyNode; return node == null ? 19 : node.Value; }
        public static int Cast(object source) { try { return ((AssemblyChild)source).Value; } catch (InvalidCastException) { return 23; } }
        public static int Nested(object source) { var node = source as AssemblyContainer.Node; return node == null ? 29 : node.Value; }
        public static int Constructed(object source) { var node = source as AssemblyGenericNode<int>; return node == null ? 31 : node.Value; }
        public static int Arrays(int a, int b)
        { var value = new AssemblyPayload(); value.Value = a; value.Node = new AssemblyNode(b); var values = new AssemblyPayload[] { value }; return unchecked(values[0].Value + values[0].Node.Value); }
    }
    public static class GenericAssemblyAccessProbe
    {
        public static int Test(int a, int b) { object value = (a & 1) == 0 ? new AssemblyChild(a) : new object(); return unchecked(GenericAssemblyCaller<int>.Test(value) + b); }
        public static int Cast(int a, int b) { object value = (a & 1) == 0 ? new AssemblyChild(a) : new AssemblyNode(b); return unchecked(GenericAssemblyCaller<long>.Cast(value) + b); }
        public static int Nested(int a, int b) { return unchecked(GenericAssemblyCaller<int>.Nested(new AssemblyContainer.Node(a)) + b); }
        public static int Constructed(int a, int b) { return unchecked(GenericAssemblyCaller<long>.Constructed(new AssemblyGenericNode<int>(a)) + b); }
        public static int Arrays(int a, int b) { return GenericAssemblyCaller<int>.Arrays(a, b); }
    }
}
