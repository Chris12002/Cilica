using System;
namespace Cilica.Fixtures.Execution
{
    public abstract class AbstractValueArgumentBase
    {
        public abstract long Read(WideOuter value);
        public abstract object Reference(WideOuter value);
        public abstract void Change(WideOuter value);
    }
    public sealed class AbstractValueArgumentLeaf : AbstractValueArgumentBase
    {
        public override long Read(WideOuter value)
        {
            var pressure = new WideCell(value.Marker);
            return unchecked(value.Nested.Number + ((WideCell)value.Nested.Reference).Number + pressure.Initial);
        }
        public override object Reference(WideOuter value) { _ = new WideCell(17); return value.Nested.Reference; }
        public override void Change(WideOuter value)
        {
            ((WideCell)value.Nested.Reference).Number = value.Marker;
            value.Marker = 43; value.Nested.Number = 47; value.Nested.Reference = new WideCell(53);
        }
    }
    internal abstract class AbstractValueArgumentInternal<T> { internal abstract bool Permits(T value); }
    internal sealed class AbstractValueArgumentInternalLeaf : AbstractValueArgumentInternal<WideOuter>
    {
        internal override bool Permits(WideOuter value) { _ = new WideCell(7); return value.Marker == (int)((WideCell)value.Nested.Reference).Number; }
    }
    public abstract class AbstractValueArgumentEmpty { public abstract int Read(EmptyVirtualValue value, int number); }
    public sealed class AbstractValueArgumentEmptyLeaf : AbstractValueArgumentEmpty { public override int Read(EmptyVirtualValue value, int number) => unchecked(number + 19); }
    public abstract class AbstractValueArgumentBorrow { public abstract int Read(ref WideOuter value); }
    public static class AbstractValueArgumentProbe
    {
        private static WideOuter Make(int a, int b) => new WideOuter { Marker = a, Nested = new WidePayload { Number = ((long)a << 32) | unchecked((uint)b), Reference = new WideCell(b) } };
        private static WideOuter Argument(int a, int b) { if (b == 0) throw new ArgumentException("argument"); return Make(a, b); }
        public static int Scalars(int a, int b)
        {
            AbstractValueArgumentBase target = new AbstractValueArgumentLeaf(); var value = Make(a,b);
            long result = target.Read(value); return unchecked((int)result ^ (int)(result >> 32));
        }
        public static int Copies(int a, int b)
        {
            AbstractValueArgumentBase target = new AbstractValueArgumentLeaf(); var value = Make(a,b); var original = value.Nested.Reference;
            target.Change(value); var reference = target.Reference(value);
            return unchecked(value.Marker + (int)value.Nested.Number + (int)((WideCell)reference).Number + (reference == original ? 71 : 97));
        }
        public static int InternalGeneric(int a, int b)
        {
            AbstractValueArgumentInternal<WideOuter> target = new AbstractValueArgumentInternalLeaf(); return target.Permits(Make(a,b)) ? 11 : 13;
        }
        public static int NullOrder(int a, int b)
        {
            AbstractValueArgumentBase target = a == 0 ? null : new AbstractValueArgumentLeaf();
            try { return unchecked((int)target.Read(Argument(a,b))); }
            catch (ArgumentException) { return 31; } catch (NullReferenceException) { return 37; }
        }
        public static int Empty(int a, int b)
        {
            AbstractValueArgumentEmpty target = b == 0 ? null : new AbstractValueArgumentEmptyLeaf();
            try { return target.Read(default, a); } catch (NullReferenceException) { return 41; }
        }
        public static int NullOnly(int a, int b)
        {
            AbstractValueArgumentBase target = null;
            try { target.Change(Make(a,b)); return 43; } catch (NullReferenceException) { return 47; }
        }
        public static int UnsupportedBorrow(int a, int b) { AbstractValueArgumentBorrow target = null; var value = Make(a,b); return target.Read(ref value); }
    }
}
