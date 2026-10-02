#nullable enable
namespace Cilica.Fixtures.Execution
{
    public class CastBase
    {
        public readonly int Value;
        public CastBase(int value) { Value = value; }
    }
    public sealed class CastLeaf : CastBase
    {
        public CastLeaf(int value) : base(value) { }
    }
    public sealed class CastSibling : CastBase
    {
        public CastSibling(int value) : base(value) { }
    }
    // Used only as a type-test target: no constructor or signature discovers it.
    public sealed class CastTargetOnly { }

    public static class ObjectCastProbe
    {
        public static object? Create(int kind, int value)
        {
            if (kind < 0) return null;
            if (kind == 0) return new CastBase(value);
            if (kind == 1) return new CastLeaf(value);
            if (kind == 2) return new CastSibling(value);
            return new ProbeNode(value);
        }
        public static CastLeaf? Checked(object? value) => (CastLeaf?)value;
        public static CastLeaf? Tested(object? value) => value as CastLeaf;
        public static int Read(CastLeaf? value, int extra) => unchecked(value!.Value + extra);
        public static int CastOrder(int kind, int value) => Read(Checked(Create(kind, value)), 10 / value);

        public static int Tests(int kind, int value)
        {
            object? original = Create(kind, value);
            CastBase? parent = original as CastBase;
            CastLeaf? leaf = Tested(original);
            CastSibling? sibling = original as CastSibling;
            int result = 0;
            if (parent != null) result += 1;
            if (leaf != null) result += 2;
            if (sibling != null) result += 4;
            if (original is ProbeNode) result += 8;
            if (original is CastTargetOnly) result += 16;
            // Cast results must stay rooted across allocations and loop backedges.
            for (int i = 0; i < 3; i++)
            {
                var later = new ProbeNode(i);
                if (leaf != null)
                {
                    CastLeaf again = (CastLeaf)parent!;
                    if (again == original && leaf == again) result = unchecked(result + again.Value + later.Initial);
                }
            }
            return result;
        }
    }
}
