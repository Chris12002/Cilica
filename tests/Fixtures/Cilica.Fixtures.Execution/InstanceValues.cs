using System;

namespace Cilica.Fixtures.Execution
{
    public sealed class InstanceValueCell<T>
    {
        public long Total;
        public InstanceValueCell(int input) { Total = input; }
        public long Current => Total;
        public T Echo(T value) { _ = new WideCell(7); return value; }
        public WideOuter Transform(WideOuter value, ref long extra, int delta)
        {
            _ = new WideCell(extra);
            Total = unchecked(Total + extra + delta);
            value.Nested.Number ^= Total;
            value.Nested.Reference = new WideCell(Total);
            value.Marker += delta;
            extra = Total;
            return value;
        }
        public void Change(ref WidePayload value, int delta)
        {
            value.Reference = new WideCell(value.Number);
            value.Number = unchecked(value.Number + delta + Total);
        }
    }
    public static class InstanceValues
    {
        private static WideOuter Make(int input, int mode)
        {
            if (mode == 0) throw new ArgumentException("argument probe");
            return new WideOuter { Marker = input, Nested = new WidePayload { Number = ((long)input << 32) | (uint)mode, Reference = new WideCell(input) } };
        }
        public static int Flow(int input, int mode)
        {
            var cell = new InstanceValueCell<WideOuter>(input);
            var original = Make(input, 1);
            long extra = ((long)mode << 32) | (uint)input;
            var changed = cell.Transform(cell.Echo(original), ref extra, mode);
            cell.Change(ref changed.Nested, mode);
            return unchecked((int)(changed.Nested.Number ^ (changed.Nested.Number >> 32) ^ cell.Current ^ extra) + changed.Marker)
                + (changed.Nested.Reference == original.Nested.Reference ? 17 : 23);
        }
        public static int NullOrder(int input, int mode)
        {
            InstanceValueCell<WideOuter> cell = mode == 2 ? new InstanceValueCell<WideOuter>(input) : null;
            try
            {
                var value = cell.Echo(Make(input, mode));
                return value.Marker + (value.Nested.Reference == null ? 3 : 7);
            }
            catch (ArgumentException) { return 31; }
            catch (NullReferenceException) { return 43; }
        }
    }
}
