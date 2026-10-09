using System;
namespace Cilica.Fixtures.Execution
{
    public readonly struct LocationReadValue
    {
        public readonly int Number;
        public LocationReadValue(int number) { Number = number; }
        public LocationReadValue(int a, int b) { Number = a < b ? b : a; }
        public int Read(int amount) { var pressure = new HeapPayload(amount); return unchecked(Number + pressure.Number); }
    }
    public static class LocationJoinProbe
    {
        public static int Constructor(int a, int b) => new LocationReadValue(a, b).Read(a < 0 ? b : a);
        private static void Add(ref int value, int amount) { value = unchecked(value + amount); }
        private static int Amount(int a, int b) => a / b;
        public static int Local(int a, int b) { int value = a; Add(ref value, a < b ? b : ~b); return value; }
        public static int Parameter(int a, int b) { Add(ref a, a < b ? b : ~b); return a; }
        private static int Borrow(ref int value, int b) { Add(ref value, b < 0 ? b : ~b); return value; }
        public static int BorrowedParameter(int a, int b) => Borrow(ref a, b);
        private static void Update(ref HeapPair value, int amount)
        {
            var node = new HeapPayload(amount);
            for (int i = 0; i < 8; i++) { var pressure = new HeapPayload(i); pressure.Number++; }
            value.Left.Number = unchecked(value.Left.Number + amount); value.Right.Payload = node;
        }
        public static int ReferenceValue(int a, int b)
        {
            var pair = new HeapPair(); pair.Left.Number = a; pair.Left.Payload = new HeapPayload(a); pair.Right = pair.Left;
            Update(ref pair, a < b ? b : ~b);
            return unchecked((int)pair.Left.Number + pair.Left.Payload.Number + pair.Right.Payload.Number);
        }
        public static int HeapAddress(int a, int b) { var holder = new HeapPayload(a); Add(ref holder.Number, a < b ? b : ~b); return holder.Number; }
        public static int ArrayAddress(int a, int b) { var values = new int[1]; Add(ref values[0], a < b ? b : ~b); return values[0]; }
        private static int Replace(ref int value, int amount) { value = amount; return ~amount; }
        public static int UpdatedContents(int a, int b)
        { int value = a; Add(ref value, a < b ? Replace(ref value, b) : Replace(ref value, a)); return value; }
        public static int Faults(int a, int b)
        {
            int value = a;
            try { Add(ref value, a < 0 ? Amount(a, b) : Amount(b, a)); }
            catch (DivideByZeroException) { return unchecked(value + 17); }
            catch (OverflowException) { return unchecked(value + 31); }
            return value;
        }

        private static void Pressure() { for (int i = 0; i < 12; i++) { var item = new HeapPayload(i); item.Number++; } }
        private static int Increment(ref int value, int amount) { Pressure(); value = unchecked(value + amount); return value; }
        public static int SelectLocal(int a, int b)
        { int x = a, y = b; Increment(ref (a < b ? ref x : ref y), b); return unchecked(x * 3 + y); }
        public static int SelectHeap(int a, int b)
        {
            var x = new HeapPayload(a); var y = new HeapPayload(b);
            Increment(ref (a < b ? ref x.Number : ref y.Number), b);
            return unchecked(x.Number * 3 + y.Number);
        }
        public static int SelectArray(int a, int b)
        {
            var x = new[] { a, b }; var y = new[] { b, a };
            Increment(ref (a < b ? ref x[1] : ref y[0]), b);
            return unchecked(x[1] * 3 + y[0]);
        }
        public static int SelectMixed(int a, int b)
        {
            var x = new HeapPayload(a); var y = new[] { b };
            Increment(ref (a < b ? ref x.Number : ref y[0]), b);
            return unchecked(x.Number * 3 + y[0]);
        }
        public static int SelectValue(int a, int b)
        {
            var x = new HeapPair(); var y = new HeapPair();
            x.Left.Payload = new HeapPayload(a); y.Left.Payload = new HeapPayload(b);
            Update(ref (a < b ? ref x : ref y), b);
            return unchecked(x.Left.Payload.Number + y.Left.Payload.Number + (int)x.Left.Number + (int)y.Left.Number);
        }
        public static int Readonly(int a, int b)
        {
            var holder = new HeapHolder<LocationReadValue>(new LocationReadValue(a)); var local = new LocationReadValue(b);
            return (a < b ? ref holder.Initial : ref local).Read(a < 0 ? a : b);
        }
        private static int WideAdd(ref long number, int amount) { Pressure(); number = unchecked(number + amount); return unchecked((int)(number >> 32) + (int)number); }
        public static int Wide(int a, int b)
        { long x = (long)a * 2147483647, y = (long)b * 2147483649; return WideAdd(ref (a < b ? ref x : ref y), b); }
        private static int ReplacePayload(ref HeapPayload value, int amount) { Pressure(); int prior = value.Number; value = new HeapPayload(amount); return prior; }
        public static int ReferenceSlot(int a, int b)
        {
            var x = new HeapPayload(a); var y = new HeapPayload(b);
            int prior = ReplacePayload(ref (a < b ? ref x : ref y), unchecked(a + b));
            return unchecked(prior + x.Number * 3 + y.Number);
        }
        public static int FieldFaults(int a, int b)
        {
            var x = a < 0 ? null : new HeapPayload(a); var y = b < 0 ? null : new HeapPayload(b);
            try { return Increment(ref (a < b ? ref x.Number : ref y.Number), a / b); }
            catch (NullReferenceException) { return 11; }
            catch (DivideByZeroException) { return 17; }
            catch (OverflowException) { return 23; }
        }
        public static int ArrayFaults(int a, int b)
        {
            int[] x = a < 0 ? null : new int[1]; int[] y = b < 0 ? null : new int[1];
            try { return Increment(ref (a < b ? ref x[a & 1] : ref y[b & 1]), a / b); }
            catch (NullReferenceException) { return 11; }
            catch (IndexOutOfRangeException) { return 13; }
            catch (DivideByZeroException) { return 17; }
            catch (OverflowException) { return 23; }
        }
        private static int staticValue;
        public static int Static(int a, int b)
        { staticValue = a; return Increment(ref (a < b ? ref staticValue : ref b), b); }
        public static int Empty(int a, int b)
        { var x = new HeapEmpty(); var y = new HeapEmpty(); return EmptyCall(ref (a < b ? ref x : ref y), b); }
        private static int EmptyCall(ref HeapEmpty empty, int number) { Pressure(); empty = default; return number; }
    }
}
