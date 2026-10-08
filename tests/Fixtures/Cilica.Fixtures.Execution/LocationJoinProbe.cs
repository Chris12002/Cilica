using System;
namespace Cilica.Fixtures.Execution
{
    public static class LocationJoinProbe
    {
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
    }
}
