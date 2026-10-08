using System;
namespace Cilica.Fixtures.Execution
{
    internal sealed class AssemblyFieldHolder<T>
    {
        internal int Number;
        internal long Wide;
        internal T Value;
        internal readonly int Initial;
        internal AssemblyFieldHolder(int initial) { Initial = initial; }
    }
    public class AssemblyFieldFamily { protected internal int Number; }
    public class AssemblyFieldOuter
    {
        private int number;
        private sealed class Hidden { internal ValueVectorBox Value; }
        internal static int Nested(int a, int b)
        {
            var holder = new Hidden(); holder.Value = new ValueVectorBox(a);
            var saved = holder.Value; holder.Value = new ValueVectorBox(b);
            return unchecked(saved.Number + holder.Value.Number);
        }
        internal static class Reader
        {
            internal static int Run(int a, int b)
            {
                var holder = new AssemblyFieldOuter(); holder.number = a;
                Borrow(ref holder.number, b); return holder.number;
            }
            private static void Borrow(ref int value, int add) { var pressure = new ValueVectorBox(add); value = unchecked(value + pressure.Number); }
        }
    }
    public static class AssemblyFieldProbe
    {
        public static int Numbers(int a, int b)
        {
            var holder = new AssemblyFieldHolder<int>(a); holder.Number = a; holder.Wide = ((long)b << 32) + (uint)a;
            holder.Value = b; return unchecked(holder.Number + holder.Initial + (int)(holder.Wide >> 32) + holder.Value);
        }
        public static int References(int a, int b)
        {
            var holder = new AssemblyFieldHolder<ValueVectorBox>(a); holder.Value = new ValueVectorBox(a);
            var saved = holder.Value; holder.Value = new ValueVectorBox(b);
            return unchecked(saved.Number + holder.Value.Number);
        }
        private static void Bump(ref ValueVectorCell value, int b)
        { var pressure = new ValueVectorBox(b); value.Number = unchecked(value.Number + pressure.Number); value.Reference = pressure; }
        public static int Values(int a, int b)
        {
            var holder = new AssemblyFieldHolder<ValueVectorCell>(a);
            ValueVectorCell value = default; value.Number = a; value.Reference = new ValueVectorBox(a); value.Wide = (long)b << 32;
            holder.Value = value; Bump(ref holder.Value, b); var saved = holder.Value;
            holder.Value = default; return unchecked(value.Reference.Number + saved.Reference.Number + saved.Number + (int)(saved.Wide >> 32));
        }
        public static int Family(int a, int b)
        { var holder = new AssemblyFieldFamily(); holder.Number = a; return unchecked(holder.Number + b); }
        public static int Nested(int a, int b) => AssemblyFieldOuter.Nested(a, b);
        public static int PrivateAncestor(int a, int b) => AssemblyFieldOuter.Reader.Run(a, b);
        private static int Set(int a, int b) { if (b == 0) throw new ArgumentException("rhs"); return unchecked(a + b); }
        public static int FaultOrder(int a, int b)
        {
            AssemblyFieldHolder<int> holder = a < 0 ? null : new AssemblyFieldHolder<int>(a);
            try { holder.Number = Set(a, b); return holder.Number; }
            catch (ArgumentException) { return -17; }
            catch (NullReferenceException) { return -23; }
        }
    }
}
