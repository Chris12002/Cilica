using System;

namespace Cilica.Fixtures.Execution
{
    public class ClassValueBase
    {
        public readonly long Number;
        public readonly object Reference;
        public ClassValueBase(WidePayload value, ref long extra, int mode)
        {
            _ = new WideCell(mode);
            Number = unchecked(value.Number + extra);
            Reference = value.Reference;
            extra ^= value.Number;
            Validate(mode);
        }
        private static void Validate(int mode) { if (mode == 0) throw new ArgumentException("constructor"); }
    }
    public sealed class ClassValueDerived : ClassValueBase
    {
        public readonly long Marker;
        public ClassValueDerived(WidePayload value, ref long extra, int mode) : base(Copy(value), ref extra, mode)
        {
            Marker = value.Number;
            value.Reference = new WideCell(mode);
        }
        private static WidePayload Copy(WidePayload value) { _ = new WideCell(17); return value; }
    }
    public sealed class ClassBorrowedValue<T>
    {
        public readonly int Marker;
        public ClassBorrowedValue(T value, ref T destination, int mode)
        {
            destination = value;
            _ = new WideCell(mode);
            Marker = mode;
        }
    }
    public sealed class ClassValueBeforeBase : ClassValueBase
    {
        public ClassValueBeforeBase(WidePayload value, ref long extra, int mode) : base(value, ref extra, unchecked((int)value.Number | mode)) { }
    }
    public static class ClassValueConstructors
    {
        private static WidePayload Make(int input, int mode)
        {
            if (mode == -1) throw new ArgumentNullException("argument");
            return new WidePayload { Number = ((long)input << 32) | (uint)mode, Reference = new WideCell(input) };
        }
        public static int Flow(int input, int mode)
        {
            long extra = mode;
            var value = Make(input, 1);
            try
            {
                var cell = new ClassValueDerived(value, ref extra, mode);
                return unchecked((int)(cell.Number ^ cell.Marker ^ extra)) + (cell.Reference == value.Reference ? 7 : 11);
            }
            catch (ArgumentException) { return unchecked((int)extra + 13); }
        }
        public static int Borrow(int input, int mode)
        {
            var value = Make(input, 1);
            var copy = default(WidePayload);
            var target = new ClassBorrowedValue<WidePayload>(value, ref copy, mode);
            var same = new ClassBorrowedValue<WidePayload>(value, ref value, mode);
            EmptyVirtualValue empty = default;
            var zero = new ClassBorrowedValue<EmptyVirtualValue>(empty, ref empty, mode);
            return unchecked((int)(copy.Number >> 32) + target.Marker + same.Marker + zero.Marker) + (copy.Reference == value.Reference ? 17 : 19);
        }
        public static int BeforeBase(int input, int mode)
        {
            long extra = mode;
            try { var cell = new ClassValueBeforeBase(Make(input, mode), ref extra, mode); return unchecked((int)cell.Number + (int)extra); }
            catch (ArgumentNullException) { return 23; }
            catch (ArgumentException) { return 29; }
        }
    }
}
