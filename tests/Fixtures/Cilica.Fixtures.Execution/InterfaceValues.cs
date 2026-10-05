using System;

namespace Cilica.Fixtures.Execution
{
    public interface IValueDispatch<T>
    {
        T Echo(T value);
        WideOuter Change(WideOuter value, ref long extra, int delta);
        void Borrow(ref WidePayload value, int delta);
    }
    public class InterfaceValueBase<T> : VirtualValueBase<T>, IValueDispatch<T> { }
    public sealed class InterfaceValueOverride<T> : InterfaceValueBase<T>
    {
        public override T Echo(T value) { _ = new WideCell(19); return base.Echo(value); }
        public override WideOuter Change(WideOuter value, ref long extra, int delta)
        {
            value = base.Change(value, ref extra, delta);
            value.Nested.Reference = new WideCell(extra);
            value.Nested.Number ^= extra;
            if (delta == 0) throw new ArgumentException("changed");
            return value;
        }
        public override void Borrow(ref WidePayload value, int delta) { base.Borrow(ref value, delta); value.Number ^= ((long)delta << 32); }
    }
    public sealed class InterfaceValueExplicit<T> : IValueDispatch<T>
    {
        T IValueDispatch<T>.Echo(T value)
        {
            IValueDispatch<T> next = new InterfaceValueOverride<T>();
            return next.Echo(value);
        }
        WideOuter IValueDispatch<T>.Change(WideOuter value, ref long extra, int delta)
        {
            extra = unchecked(extra + value.Nested.Number);
            value.Nested.Reference = new WideCell(extra);
            value.Marker = unchecked(value.Marker + delta);
            return value;
        }
        void IValueDispatch<T>.Borrow(ref WidePayload value, int delta)
        {
            value.Reference = new WideCell(delta);
            value.Number = unchecked(value.Number + delta);
        }
    }
    public static class InterfaceValues
    {
        private static WideOuter Make(int input, int mode)
        {
            if (mode == 0) throw new ArgumentException("argument");
            return new WideOuter { Marker = input, Nested = new WidePayload { Number = ((long)input << 32) | (uint)mode, Reference = new WideCell(input) } };
        }
        public static int Flow(int input, int mode)
        {
            IValueDispatch<WideOuter> target = mode == 0 ? new InterfaceValueBase<WideOuter>() : new InterfaceValueExplicit<WideOuter>();
            var original = Make(input, 1);
            long extra = input;
            var changed = target.Change(target.Echo(original), ref extra, mode);
            target.Borrow(ref changed.Nested, mode);
            return unchecked((int)(changed.Nested.Number ^ (changed.Nested.Number >> 32) ^ extra) + changed.Marker)
                + (changed.Nested.Reference == original.Nested.Reference ? 17 : 23);
        }
        public static int Aliasing(int input, int mode)
        {
            IValueDispatch<WideOuter> target = new InterfaceValueOverride<WideOuter>();
            var value = Make(input, 1);
            var copy = value;
            try { value = target.Change(value, ref value.Nested.Number, mode); }
            catch (ArgumentException) { return unchecked((int)value.Nested.Number + copy.Marker); }
            return unchecked((int)value.Nested.Number + (int)copy.Nested.Number + value.Marker);
        }
        public static int NullOrder(int input, int mode)
        {
            IValueDispatch<WideOuter> target = mode == 2 ? new InterfaceValueOverride<WideOuter>() : null;
            try { var value = target.Echo(Make(input, mode)); return value.Marker + (value.Nested.Reference == null ? 3 : 7); }
            catch (ArgumentException) { return 31; }
            catch (NullReferenceException) { return 43; }
        }
        public static int Empty(int input, int mode)
        {
            IValueDispatch<EmptyVirtualValue> target = mode == 0 ? null : new InterfaceValueExplicit<EmptyVirtualValue>();
            try { var value = target.Echo(default); return input ^ mode; }
            catch (NullReferenceException) { return 47; }
        }
    }
}
