#nullable enable
namespace Cilica.Fixtures.Execution
{
    public class HierarchyBase
    {
        private int _value;
        public readonly int Initial;
        public readonly ProbeNode Link;
        public ProbeNode? Zero;

        public HierarchyBase(int value, ProbeNode seed)
        {
            _value = value;
            Initial = value;
            Link = new ProbeNode(seed.Initial);
        }

        public int Change(int delta) => _value = unchecked(_value + delta);
        public int Read() => unchecked(_value + Initial + Link.Initial);
        public int Constant() => 29;
    }

    public class HierarchyMiddle : HierarchyBase
    {
        // C# evaluates this initializer before the base-call arguments.
        public readonly ProbeNode Before = new ProbeNode(13);
        public new readonly ProbeNode Link;
        public readonly ProbeNode Seed;

        public HierarchyMiddle(int value, ProbeNode seed) : base(checked(value + 1), seed)
        {
            Seed = seed;
            Link = new ProbeNode(value);
        }

        public int ReadMiddle() => unchecked(Read() + Link.Initial + Before.Initial + Seed.Initial);
    }

    public sealed class HierarchyLeaf : HierarchyMiddle
    {
        public readonly int Stamp;
        public HierarchyLeaf(int value, ProbeNode seed) : base(value, seed) { Stamp = value; }
    }

    public static class ObjectHierarchyProbe
    {
        public static HierarchyBase Widen(HierarchyBase value) => value;
        public static int Run(int left, int right)
        {
            var seed = new ProbeNode(right);
            var leaf = new HierarchyLeaf(left, seed);
            HierarchyBase parent = Widen(leaf);
            parent.Change(right);
            var later = new ProbeNode(parent.Initial);
            return unchecked(leaf.ReadMiddle() + parent.Read() + leaf.Stamp + later.Initial
                + (parent.Zero == null ? 1 : 0) + (parent == leaf ? 2 : 0));
        }

        public static int ArgumentBeforeNull(int left, int right)
        {
            HierarchyBase? value = null;
            return value!.Change(checked(left + right));
        }

        public static int BaseArgumentFault(int left, int right)
        {
            var seed = new ProbeNode(right);
            return new HierarchyLeaf(left / right, seed).Read();
        }
    }
}
