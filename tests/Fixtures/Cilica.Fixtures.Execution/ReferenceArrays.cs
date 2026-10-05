using System;

namespace Cilica.Fixtures.Execution
{
    public sealed class ReferenceArrayHolder<T>
    {
        public T[] Items;
        public ReferenceArrayHolder(T[] items) { Items = items; }
    }
    public struct ReferenceArrayValue { public WideGenericCell<long>[] Items; public long Marker; }
    public static class ReferenceArrays
    {
        private static T[] Create<T>(int length) => new T[length];
        private static void Copy<T>(T[] source, T[] destination)
        {
            for (int i = 0; i < source.Length; ++i) destination[i] = source[i];
        }
        private static ReferenceArrayValue Make(int input)
        {
            var items = Create<WideGenericCell<long>>(3);
            items[0] = new WideGenericCell<long>(((long)input << 32) | 17L);
            items[2] = items[0];
            return new ReferenceArrayValue { Items = items, Marker = 0x100000003L };
        }
        public static int Generic(int input, int mode)
        {
            var value = Make(input);
            var holder = new ReferenceArrayHolder<WideGenericCell<long>>(Create<WideGenericCell<long>>(3));
            Copy(value.Items, holder.Items);
            object[] widened = holder.Items;
            var restored = (WideGenericCell<long>[])widened;
            long bits = restored[2].Number ^ value.Marker;
            return unchecked((int)(bits ^ (bits >> 32))) + (restored[1] == null ? 5 : 0)
                + (restored[0] == value.Items[2] ? mode : 0);
        }
        public static int FaultOrder(int input, int mode)
        {
            object[] items = new WideCell[2];
            var saved = new WideCell(input); items[0] = saved;
            int result = 0;
            try
            {
                if (mode == 0) items = null;
                items[mode] = new WideGenericCell<long>(input);
            }
            catch (NullReferenceException) { result = 11; }
            catch (IndexOutOfRangeException) { result = 23; }
            catch (ArrayTypeMismatchException) { result = 37; }
            return result + (items == null ? 0 : items[0] == saved ? 3 : 7);
        }
        public static int Casts(int input, int mode)
        {
            object value = mode == 0 ? null : mode == 1 ? (object)new WideCell[1] : new WideGenericCell<long>[2];
            var tested = value as WideCell[];
            try
            {
                var cast = (WideCell[])value;
                return cast == null ? input : cast.Length + (cast == tested ? 3 : 7);
            }
            catch (InvalidCastException error) { return error.Message.Length + (tested == null ? 11 : 13); }
        }
        public static int NullOnly(int input, int mode)
        {
            try { return ((object[])null)[input] == null ? mode : 7; }
            catch (NullReferenceException) { return 19; }
        }
        public static int WideElements(int input, int mode) { var items = new long[1]; items[0] = input; return (int)items[0] + mode; }
        public static int Nested(int input, int mode) { var items = new object[1][]; return items.Length + input + mode; }
    }
}
