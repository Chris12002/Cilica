using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class ValueResultReceiver
    {
        public ValueVectorBox Reference;
        public ValueVectorCell Make(int a)
        {
            ValueVectorCell value = default; value.Number = a; value.Wide = ((long)a << 32) | (uint)Reference.Number; value.Reference = Reference;
            var pressure = new ValueVectorBox(a); value.Number = pressure.Number; return value;
        }
    }
    public static class ValueResultDelegateProbe
    {
        private static int Read(ValueVectorCell value) => unchecked(value.Number + (int)value.Wide + (int)(value.Wide >> 32) + (value.Reference == null ? 0 : value.Reference.Number));
        private static ValueVectorCell Make(int a, int b)
        {
            ValueVectorCell value = default; value.Number = a; value.Wide = ((long)a << 32) | (uint)b; value.Reference = new ValueVectorBox(b); return value;
        }
        private static ValueVectorCell Echo(ValueVectorCell value) { var pressure = new ValueVectorBox(value.Number); value.Number = unchecked(pressure.Number + 1); return value; }
        private static ValueVectorBucket<ValueVectorCell> Nested(ValueVectorCell value) { ValueVectorBucket<ValueVectorCell> result = default; result.Value = value; result.Marker = value.Number; return result; }
        private static ValueVectorEmpty Empty(int value) { var pressure = new ValueVectorBox(value); if (pressure.Number < 0) throw new InvalidOperationException("empty"); return default; }
        private static ValueVectorCell Fault(int a, int b) { var result = Make(a,b); if (a < 0) throw new InvalidOperationException("result"); return result; }
        private static ValueVectorCell? Nullable(int a, int b) { if (a < 0) return null; return Make(a,b); }
        public static int Static(int a, int b) { Func<int,int,ValueVectorCell> callback = Make; return Read(callback(a,b)); }
        public static int Captured(int a, int b) { var owner = new ValueResultReceiver(); owner.Reference = new ValueVectorBox(b); Func<int,ValueVectorCell> callback = owner.Make; var result = callback(a); return result.Reference == owner.Reference ? Read(result) : -100; }
        public static int RoundTrip(int a, int b) { Func<ValueVectorCell,ValueVectorCell> callback = Echo; var original = Make(a,b); var result = callback(original); return unchecked(Read(result) + original.Number + (original.Reference == result.Reference ? 17 : 0)); }
        public static int NestedResult(int a, int b) { Func<ValueVectorCell,ValueVectorBucket<ValueVectorCell>> callback = Nested; var result = callback(Make(a,b)); return unchecked(Read(result.Value) + result.Marker); }
        public static int EmptyResult(int a, int b) { Func<int,ValueVectorEmpty> callback = Empty; try { callback(a); return b; } catch (InvalidOperationException e) { return e.Message == "empty" ? 19 : -100; } }
        public static int Null(int a, int b) { Func<int,int,ValueVectorCell> callback = null; try { return Read(callback(a,b)); } catch (NullReferenceException) { return 23; } }
        public static int Throw(int a, int b) { Func<int,int,ValueVectorCell> callback = Fault; try { return Read(callback(a,b)); } catch (InvalidOperationException e) { return e.Message == "result" ? 29 : -100; } }
        public static int NullableResult(int a, int b) { Func<int,int,ValueVectorCell?> callback = Nullable; var result = callback(a,b); return result.HasValue ? Read(result.Value) : 31; }
        public static int Repeated(int a, int b) { Func<int,int,ValueVectorCell> callback = Make; var first = callback(a,b); var second = callback(b,a); first.Number = unchecked(first.Number + 1); return unchecked(Read(first) + Read(second)); }
        public static int Named(int a, int b) => ReferenceResultDelegateProbe.ValueResult(a,b);
        public static int Factory(int a, int b) => ValueDelegateProbe.ValueResult(a,b);
    }
}
