using System;

namespace Cilica.Fixtures.Execution
{
    public sealed class EmbeddingExceptionOther { public int Value; }
    public sealed class EmbeddingExceptionState { public static int Calls; }
    public static class EmbeddingExceptionProbe
    {
        public static int Run(int mode, int input)
        {
            EmbeddingExceptionState.Calls++;
            if (mode == 0) throw new InvalidOperationException("copy\0\ud800\udfff");
            if (mode == 1) return 10 / input;
            if (mode == 2) throw new ArgumentException("bad\0input", "p\ud800");
            if (mode == 3) throw new InvalidOperationException();
            if (mode == 4) throw (Exception)null;
            if (mode == 5) {
                try { throw new InvalidOperationException("caught"); }
                catch (InvalidOperationException) { return 17; }
            }
            return input;
        }
        public static EmbeddingExceptionOther Make(int mode)
        {
            Run(mode, 0);
            return new EmbeddingExceptionOther();
        }
        public static int Calls() => EmbeddingExceptionState.Calls;
        public static int Churn(int count)
        {
            int sum = 0;
            for (int i = 0; i < count; ++i) sum = unchecked(sum + new EmbeddingExceptionOther { Value = i }.Value);
            return sum;
        }
        public static int Root(int mode, int input)
        {
            if (mode == 9) return Make(6).Value;
            if (mode == 10) return Churn(input);
            if (mode == 11) return Calls();
            return Run(mode, input);
        }
    }
}
