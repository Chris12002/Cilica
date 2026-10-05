using System;

namespace Cilica.Fixtures.Execution
{
    public sealed class EmbeddingNode { public int Value; }
    public sealed class EmbeddingState
    {
        public static EmbeddingNode Held;
        public static int Number;
    }

    public static class EmbeddingProbe
    {
        public static int Catch(int input, int delta)
        {
            try { return Step(input, delta); }
            catch (InvalidOperationException) { return 42; }
        }

        public static int Step(int input, int delta)
        {
            if (EmbeddingState.Held == null)
            {
                var held = new EmbeddingNode(); held.Value = input;
                EmbeddingState.Held = held;
            }
            var temporary = new EmbeddingNode(); temporary.Value = delta;
            EmbeddingState.Number = unchecked(EmbeddingState.Number + delta);
            if (delta < 0) throw new InvalidOperationException("embedding failure");
            return unchecked(EmbeddingState.Held.Value + EmbeddingState.Number + temporary.Value);
        }
    }
}
