using System;

namespace Cilica.Fixtures.Execution
{
    public class EmbeddingHandleBase { public int Value; }
    public sealed class EmbeddingHandleNode : EmbeddingHandleBase { public EmbeddingHandleNode Next; }
    public sealed class EmbeddingHandleOther { public int Value; }
    public sealed class EmbeddingHandleState { public static int Created; }

    public static class EmbeddingHandleProbe
    {
        public static int Root(int mode, int input)
        {
            var node = Create(input);
            if (mode == 0) { Mutate(node, 1); return Read(node); }
            if (mode == 1) return Same(Alias(node), node);
            if (mode == 2) { Link(node, node); return Churn(4); }
            if (mode == 3) return ReadBase(node);
            if (mode == 4) return Other().Value;
            if (mode == 5) return Calls();
            return Read(Alias(null));
        }
        public static EmbeddingHandleNode Create(int value)
        {
            EmbeddingHandleState.Created++;
            if (value < 0) throw new InvalidOperationException("handle result failure");
            return new EmbeddingHandleNode { Value = value };
        }
        public static EmbeddingHandleOther Other() => new EmbeddingHandleOther { Value = 7 };
        public static EmbeddingHandleNode Alias(EmbeddingHandleNode node) => node;
        public static int Read(EmbeddingHandleNode node)
        {
            var pressure = new EmbeddingHandleBase();
            return unchecked(node.Value + pressure.Value);
        }
        public static int ReadBase(EmbeddingHandleBase node) => node.Value;
        public static int Same(EmbeddingHandleNode left, EmbeddingHandleNode right) => left == right ? 1 : 0;
        public static void Mutate(EmbeddingHandleNode node, int delta) => node.Value = unchecked(node.Value + delta);
        public static void Link(EmbeddingHandleNode left, EmbeddingHandleNode right) => left.Next = right;
        public static int Calls() => EmbeddingHandleState.Created;
        public static int Churn(int count)
        {
            int sum = 0;
            for (int i = 0; i < count; ++i) sum = unchecked(sum + new EmbeddingHandleNode { Value = i }.Value);
            return sum;
        }
    }
}
