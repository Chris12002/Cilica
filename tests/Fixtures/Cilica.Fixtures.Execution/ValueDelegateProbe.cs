using System;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public struct ValueDelegateTwin { public int Number; public long Wide; public ValueVectorBox Reference; }
    public delegate int ValueCallback(ValueVectorCell value, int other);
    public delegate void RefValueCallback(ref ValueVectorCell value);
    public sealed class ValueDelegateReceiver
    {
        public int Number;
        public int Accept(ValueVectorCell value, int other)
        {
            var allocation = new ValueVectorBox(other);
            value.Number = unchecked(value.Number + Number);
            if (value.Reference != null) value.Reference.Number = unchecked(value.Reference.Number + allocation.Number);
            return unchecked(value.Number + (int)value.Wide + (int)(value.Wide >> 32));
        }
        public void Set(ValueVectorCell value) { Number = unchecked(value.Number + value.Reference.Number + (int)value.Wide); }
        public bool Match(ValueVectorCell value) { var allocation = new ValueVectorBox(Number); return value.Number > allocation.Number; }
    }
    public static class ValueDelegateProbe
    {
        private static ValueVectorCell Cell(int a, int b)
        {
            ValueVectorCell result = default; result.Number = a; result.Wide = ((long)a << 32) | (uint)b;
            result.Reference = new ValueVectorBox(b); return result;
        }
        internal static int Read(ValueVectorCell value, int other)
        {
            var allocation = new ValueVectorBox(other);
            int result = unchecked(value.Number + value.Reference.Number + allocation.Number + (int)(value.Wide >> 32) + (int)value.Wide);
            value.Number = 100; value.Reference = null; return result;
        }
        private static int Pair(ValueVectorCell one, ValueVectorCell two) => unchecked(Read(one, two.Number) + Read(two, one.Number));
        private static int Nested(ValueVectorBucket<ValueVectorCell> value) => unchecked(Read(value.Value, value.Marker));
        private static int Empty(ValueVectorEmpty value, int number) => number;
        private static ValueVectorBox Reference(ValueVectorCell value) { var allocation = new ValueVectorBox(value.Number); return value.Reference; }
        private static int Throw(ValueVectorCell value) { if (value.Number < 0) throw new InvalidOperationException("value"); return Read(value, 1); }
        public static int Static(int a, int b) { ValueCallback callback = Read; var cell = Cell(a,b); return unchecked(callback(cell,b) + cell.Number + cell.Reference.Number); }
        public static int Captured(int a, int b)
        {
            var owner = new ValueDelegateReceiver(); owner.Number = b; ValueCallback callback = owner.Accept; var cell = Cell(a,b);
            return unchecked(callback(cell,b) + cell.Number + cell.Reference.Number);
        }
        public static int GenericPair(int a, int b) { Func<ValueVectorCell,ValueVectorCell,int> callback = Pair; return callback(Cell(a,b),Cell(b,a)); }
        public static int GenericNested(int a, int b)
        {
            Func<ValueVectorBucket<ValueVectorCell>,int> callback = Nested;
            ValueVectorBucket<ValueVectorCell> value = default; value.Value = Cell(a,b); value.Marker = b; return callback(value);
        }
        public static int EmptyValue(int a, int b) { Func<ValueVectorEmpty,int,int> callback = Empty; return callback(default,a); }
        public static int ReferenceResult(int a, int b)
        {
            Func<ValueVectorCell,ValueVectorBox> callback = Reference; var cell = Cell(a,b); var result = callback(cell);
            return result == cell.Reference ? result.Number : -100;
        }
        public static int Void(int a, int b)
        {
            var owner = new ValueDelegateReceiver(); Action<ValueVectorCell> callback = owner.Set; callback(Cell(a,b)); return owner.Number;
        }
        public static int Null(int a, int b)
        {
            Func<ValueVectorCell,int> callback = null;
            try { return callback(Cell(a,b)); } catch (NullReferenceException) { return 17; }
        }
        public static int Fault(int a, int b)
        {
            Func<ValueVectorCell,int> callback = Throw;
            try { return callback(Cell(a,b)); } catch (InvalidOperationException e) { return e.Message == "value" ? 19 : -100; }
        }
        public static int Predicate(int a, int b)
        {
            var owner = new ValueDelegateReceiver(); owner.Number = b; Func<ValueVectorCell,bool> callback = owner.Match;
            return callback(Cell(a,b)) ? 11 : 23;
        }
        public static int Query(int a, int b)
        {
            var owner = new ValueDelegateReceiver(); owner.Number = b; var values = new ValueVectorCell[2]; values[0] = Cell(a,b); values[1] = Cell(b,a);
            var value = values.FirstOrDefault(owner.Match); return unchecked(value.Number + (value.Reference == null ? 0 : value.Reference.Number));
        }
        public static int Closure(int a, int b) { Func<ValueVectorCell,int> callback = value => Read(value,b); return callback(Cell(a,b)); }
        private static int Twin(ValueDelegateTwin value) => unchecked(value.Number + (int)value.Wide + value.Reference.Number);
        public static int Nominal(int a, int b)
        {
            Func<ValueVectorCell,int,int> first = Read; Func<ValueDelegateTwin,int> second = Twin;
            var cell = Cell(a,b); ValueDelegateTwin twin = default; twin.Number = a; twin.Wide = cell.Wide; twin.Reference = cell.Reference;
            return unchecked(first(cell,b) + second(twin));
        }
        public static int Count(int a, int b) => LinqPredicateProbe.ValuePredicate(a,b);
        public static int DefaultQuery(int a, int b) => LinqFirstProbe.ValuePredicate(a,b);
        public static int UnsupportedResult(int a, int b) { Func<int,int,ValueVectorCell> callback = Cell; return callback(a,b).Number; }
        private static void Ref(ref ValueVectorCell value) { value.Number++; }
        public static int UnsupportedByRef(int a, int b) { RefValueCallback callback = Ref; var value = Cell(a,b); callback(ref value); return value.Number; }
    }
}
