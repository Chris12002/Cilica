using System;

namespace Cilica.Fixtures.Execution
{
    public sealed class EmbeddingModuleNode { public int Value; }
    public struct EmbeddingModuleValue { public int Number; }
    public sealed class EmbeddingModuleState
    {
        public static EmbeddingModuleNode Held;
        public static int Number;
    }

    public static class EmbeddingModuleProbe
    {
        // Explicit inclusion root: the module exports selected callees from this
        // single compiled closure; it does not execute this method during startup.
        public static int ModuleRoot(int operation, int input, int delta)
        {
            if (operation == 0) return Reset(input);
            if (operation == 1) return Step(input, delta);
            if (operation == 2) return Read();
            if (operation == 3) return Catch(input, delta);
            if (operation == 4) return Private(input);
            return Inline(new EmbeddingModuleValue { Number = input });
        }

        public static int Read() => EmbeddingModuleState.Held == null ? -100 : unchecked(EmbeddingModuleState.Held.Value + EmbeddingModuleState.Number);
        public static int Reset(int input)
        {
            var node = new EmbeddingModuleNode(); node.Value = input;
            EmbeddingModuleState.Held = node; EmbeddingModuleState.Number = 0;
            return Read();
        }
        private static int Private(int input) => input;
        public static int Inline(EmbeddingModuleValue input) => input.Number;

        public static int Catch(int input, int delta)
        {
            try { return Step(input, delta); }
            catch (InvalidOperationException) { return 42; }
        }

        public static int Step(int input, int delta)
        {
            if (EmbeddingModuleState.Held == null)
            {
                var held = new EmbeddingModuleNode(); held.Value = input;
                EmbeddingModuleState.Held = held;
            }
            var temporary = new EmbeddingModuleNode(); temporary.Value = delta;
            EmbeddingModuleState.Number = unchecked(EmbeddingModuleState.Number + delta);
            if (delta < 0) throw new InvalidOperationException("embedding failure");
            return unchecked(EmbeddingModuleState.Held.Value + EmbeddingModuleState.Number + temporary.Value);
        }
    }
}
