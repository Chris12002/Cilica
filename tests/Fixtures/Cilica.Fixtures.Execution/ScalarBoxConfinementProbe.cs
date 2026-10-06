using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class ScalarEscapeHolder { public object Value; }
    public static class ScalarBoxConfinementProbe
    {
        public static int Read(object value) => (int)value;
        private static int Forward(object value) => Read(value);
        private static int ReadByte(object value) => (byte)value;
        private static int ReadWide(object value) => unchecked((int)((long)value >> 32));
        private static int IsInteger(object value) => value is int ? 1 : 0;
        private static int IsNullable(object value) => value is int? ? 1 : 0;
        private static int CopyNullable(object value) => (value as int?).GetValueOrDefault();
        private static int IsComparable(object value) => value is IComparable ? 1 : 0;
        private static int CastComparable(object value) => (IComparable)value == null ? 0 : 1;
        private static object Make(int value) => value;
        private static object MakeNullable(int? value) => value;
        private static int RecursiveRead(object value, int depth) => depth == 0 ? (int)value : RecursiveRead(value, depth - 1);
        private static void Clear(ref object value) { value = null; }
        private static int AddressedRead(object value) { Clear(ref value); return value == null ? 1 : 0; }

        private static int ReadAfterAllocation(object value)
        {
            var pressure = new NullableNode(17);
            return unchecked((int)value + pressure.Value);
        }
        public static int AllocationLifetime(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(ReadAfterAllocation(a) + other.Read());
        }
        public static int Direct(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(Read(a) + other.Read());
        }
        public static int Forwarded(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(Forward(a) + other.Read());
        }
        public static int Narrow(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(ReadByte(unchecked((byte)a)) + other.Read());
        }
        public static int Wide(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(ReadWide(((long)a << 32) | (uint)b) + other.Read());
        }
        public static int EnumCopy(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(Read((BoxEnum)a) + other.Read());
        }
        public static int MixedNominal(int a, int b)
        {
            object other = new BoxedCallClass(b);
            return unchecked(IsInteger(a) + (other is IBoxCall ? b : 0));
        }
        public static int NominalTest(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(IsInteger(a) + IsInteger((BoxEnum)b) + other.Read());
        }
        public static int NullableTest(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(IsNullable(a) + IsNullable((BoxEnum)b) + other.Read());
        }
        public static int NullableCopy(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(CopyNullable(a) + CopyNullable((BoxEnum)b) + other.Read());
        }
        public static int UnrelatedCast(int a, int b)
        {
            object other = new BoxedCallClass(b);
            return unchecked(Read(a) + ((IBoxCall)other).Read());
        }
        public static int UnrelatedTest(int a, int b)
        {
            object other = new BoxedCallClass(b);
            return unchecked(Read(a) + (other is IBoxCall ? b : 0));
        }
        public static int TargetOnly(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(IsInteger(other) + other.Read());
        }
        public static int Fault(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            try { return unchecked(Read((long)a) + other.Read()); }
            catch (InvalidCastException) { return other.Read(); }
        }
        public static int UnsupportedInterfaceTest(int a, int b) => IsComparable(a) + b;
        public static int UnsupportedInterfaceCast(int a, int b) => CastComparable(a) + b;
        public static int Returned(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked((int)Make(a) + other.Read());
        }
        public static int NullableBox(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(Read(MakeNullable(a)) + other.Read());
        }
        public static int Local(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b); object value = a;
            int read = other.Read();
            return unchecked((int)value + (int)value + read);
        }
        public static int Array(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b); var values = new object[1]; values[0] = a;
            return unchecked((int)values[0] + other.Read());
        }
        public static int Field(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b); var holder = new ScalarEscapeHolder(); holder.Value = a;
            return unchecked((int)holder.Value + other.Read());
        }
        public static int Recursive(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(RecursiveRead(a, 1) + other.Read());
        }
        public static int ParameterAddress(int a, int b)
        {
            IBoxCall other = new BoxedCallClass(b);
            return unchecked(AddressedRead(a) + other.Read());
        }
    }
}
