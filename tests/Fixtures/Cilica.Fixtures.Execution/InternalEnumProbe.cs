using System;
namespace Cilica.Fixtures.Execution
{
    [Flags] internal enum PreparationFlags { None = 0, First = 1, Second = 2, All = 3 }
    public sealed class InternalEnumHolder
    {
        private PreparationFlags flags;
        public InternalEnumHolder(int bits) { flags = (PreparationFlags)bits; }
        public int Read() => (int)flags;
        public int Change(int bits) { var previous = flags; flags |= (PreparationFlags)bits; return (int)previous; }
    }
    public struct InternalEnumCell
    {
        private PreparationFlags flags;
        public InternalEnumCell(int bits) { flags = (PreparationFlags)bits; }
        public int Read() => (int)flags;
        public void Change(int bits) { flags ^= (PreparationFlags)bits; }
    }
    public static class InternalEnumProbe
    {
        private static PreparationFlags saved;
        private static PreparationFlags Echo(PreparationFlags flags) => flags;
        private static void Edit(ref PreparationFlags flags, int bits) { flags ^= (PreparationFlags)bits; }
        public static int Locals(int a, int b)
        {
            PreparationFlags value = (PreparationFlags)a; Edit(ref value, b);
            return (int)Echo(value);
        }
        public static int Objects(int a, int b)
        {
            var holder = new InternalEnumHolder(a); int old = holder.Change(b);
            return unchecked(old + holder.Read());
        }
        public static int Values(int a, int b)
        {
            var value = new InternalEnumCell(a); var copy = value; value.Change(b);
            return unchecked(value.Read() + copy.Read());
        }
        public static int ArraysOfValues(int a, int b)
        {
            var values = new InternalEnumCell[1]; values[0] = new InternalEnumCell(a);
            values[0].Change(b); return values[0].Read();
        }
        public static int Statics(int a, int b)
        {
            saved = (PreparationFlags)a; var previous = saved; saved |= (PreparationFlags)b;
            return unchecked((int)previous + (int)saved);
        }
        public static int UnsupportedBox(int a, int b) { object value = (PreparationFlags)a; return (int)(PreparationFlags)value; }
        public static int UnsupportedGeneric(int a, int b) { var box = new EnumHolder<PreparationFlags>(); box.Value = (PreparationFlags)a; return (int)box.Value; }
    }
}
