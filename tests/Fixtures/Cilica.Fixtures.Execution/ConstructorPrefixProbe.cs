using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class PrefixTrace { public int Value; }
    public class PrefixBase
    {
        public readonly int Value;
        public PrefixBase(int value, PrefixTrace trace) { trace.Value = unchecked(trace.Value * 7 + 3); Value = value; }
        public PrefixBase(Func<int, int> transform, int value, PrefixTrace trace) : this(transform(value), trace) { }
        public PrefixBase(ValueVectorBox node, PrefixTrace trace) : this(node.Number, trace) { }
    }
    public sealed class PrefixConditional : PrefixBase
    {
        public readonly ValueVectorBox Initial = new ValueVectorBox(11);
        public readonly int After;
        public PrefixConditional(int a, int b, PrefixTrace trace) : base(a < b ? ConstructorPrefixProbe.Stamp(trace, a) : ConstructorPrefixProbe.Stamp(trace, b), trace)
        { After = unchecked(a + b); }
    }
    public sealed class PrefixThrow : PrefixBase
    {
        public PrefixThrow(int a, PrefixTrace trace) : base(a < 0 ? throw new ArgumentException("prefix") : ConstructorPrefixProbe.Stamp(trace, a), trace) { }
    }
    public sealed class PrefixCachedDelegate : PrefixBase
    {
        public PrefixCachedDelegate(int a, PrefixTrace trace) : base(value => unchecked(value + 17), a, trace) { }
    }
    public sealed class PrefixChain : PrefixBase
    {
        private PrefixChain(int a, PrefixTrace trace) : base(a, trace) { trace.Value = unchecked(trace.Value * 7 + 5); }
        public PrefixChain(int a, int b, PrefixTrace trace) : this(a < b ? ConstructorPrefixProbe.Stamp(trace, a) : ConstructorPrefixProbe.Stamp(trace, b), trace)
        { trace.Value = unchecked(trace.Value * 7 + 6); }
    }
    public sealed class PrefixReference : PrefixBase
    {
        public PrefixReference(int a, int b, PrefixTrace trace) : base((a & 1) == 0 ? new ValueVectorBox(a) : new ValueVectorBox(b), trace) { }
    }
    public sealed class PrefixHandler : PrefixBase
    {
        public readonly int After;
        public PrefixHandler(int a, int b, PrefixTrace trace) : base(a < b ? a : b, trace)
        {
            try { if ((a & 1) == 0) throw new InvalidOperationException(); After = b; }
            catch (InvalidOperationException) { After = a; }
            finally { trace.Value = unchecked(trace.Value * 7 + 4); }
        }
    }
    public static class ConstructorPrefixProbe
    {
        public static int Stamp(PrefixTrace trace, int value) { trace.Value = unchecked(trace.Value * 7 + 2); return value; }
        public static int Conditional(int a, int b) { var trace = new PrefixTrace(); var value = new PrefixConditional(a, b, trace); return unchecked(value.Value + value.After + value.Initial.Number + trace.Value); }
        public static int Throw(int a, int b)
        { var trace = new PrefixTrace(); try { var value = new PrefixThrow(a, trace); return unchecked(value.Value + b + trace.Value); } catch (ArgumentException) { return unchecked(trace.Value + a - b); } }
        public static int CachedDelegate(int a, int b)
        { var trace = new PrefixTrace(); var one = new PrefixCachedDelegate(a, trace); var two = new PrefixCachedDelegate(b, trace); return unchecked(one.Value + two.Value + trace.Value); }
        public static int Chain(int a, int b) { var trace = new PrefixTrace(); var value = new PrefixChain(a, b, trace); return unchecked(value.Value + trace.Value); }
        public static int Reference(int a, int b) { var trace = new PrefixTrace(); var value = new PrefixReference(a, b, trace); return unchecked(value.Value + trace.Value); }
        public static int Handlers(int a, int b) { var trace = new PrefixTrace(); var value = new PrefixHandler(a, b, trace); return unchecked(value.Value + value.After + trace.Value); }
    }
}
