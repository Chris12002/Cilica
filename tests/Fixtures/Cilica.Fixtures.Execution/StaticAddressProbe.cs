using System;
namespace Cilica.Fixtures.Execution
{
    public struct StaticBorrowValue
    {
        public long Wide;
        public HeapPayload Payload;
        public StaticBorrowValue(int a, int b) { Wide = ((long)a << 32) | (uint)b; Payload = new HeapPayload(a); }
        public readonly int Read(int value) => unchecked((int)Wide + (int)(Wide >> 32) + Payload.Number + value);
        public int Mutate(int value) { Payload = new HeapPayload(value); Wide = unchecked(Wide + value); return Read(value); }
    }
    public struct StaticBorrowEmpty { public int Read(int a) => a; }
    internal static class StaticBorrowFields
    {
        internal static int Number;
        internal static long Wide;
        internal static HeapPayload Payload;
        internal static StaticBorrowValue Value;
        internal static StaticBorrowEmpty Empty;
    }
    internal static class StaticBorrowReadonlyFields
    {
        internal static readonly StaticBorrowValue ReadonlyValue = new StaticBorrowValue(3, 7);
    }
    internal static class StaticBorrowDefaultFields
    {
        internal static readonly ReadOnlyContainer<int> DefaultId;
    }
    internal static class StaticBorrowGeneric<T> { internal static T Value; }
    public static class StaticAddressProbe
    {
        private static int Mutate(ref int left, ref int right, int a, int b)
        { left = a; var pressure = new HeapPayload(b); right = unchecked(right + pressure.Number); return unchecked(left + right); }
        private static void Wide(ref long value, int a, int b) { value = ((long)a << 32) | (uint)b; }
        private static int Reference(ref HeapPayload value, int a, int b)
        { value = new HeapPayload(a); var pressure = new HeapPayload(b); return unchecked(value.Number + pressure.Number); }
        public static int Aliased(int a, int b) => Mutate(ref StaticBorrowFields.Number, ref StaticBorrowFields.Number, a, b);
        public static int Long(int a, int b) { Wide(ref StaticBorrowFields.Wide, a, b); return unchecked((int)StaticBorrowFields.Wide + (int)(StaticBorrowFields.Wide >> 32)); }
        public static int References(int a, int b) { int result = Reference(ref StaticBorrowFields.Payload, a, b); StaticBorrowFields.Payload = null; return result; }
        public static int Value(int a, int b) { StaticBorrowFields.Value = new StaticBorrowValue(a, b); int value = StaticBorrowFields.Value.Mutate(b); StaticBorrowFields.Value = default; return value; }
        public static int Nested(int a, int b) { StaticBorrowFields.Value = default; Wide(ref StaticBorrowFields.Value.Wide, a, b); return unchecked((int)(StaticBorrowFields.Value.Wide >> 32)); }
        public static int Readonly(int a, int b) => StaticBorrowReadonlyFields.ReadonlyValue.Read(unchecked(a + b));
        public static int Empty(int a, int b) => StaticBorrowFields.Empty.Read(unchecked(a + b));
        public static int Default(int a, int b) => unchecked(StaticBorrowDefaultFields.DefaultId.Read() + a + b);
        public static int Generic(int a, int b) { int r = Reference(ref StaticBorrowGeneric<HeapPayload>.Value, a, b); StaticBorrowGeneric<HeapPayload>.Value = null; return r; }
        public static int All(int a, int b) => unchecked(Aliased(a,b) + Long(a,b) + References(a,b) + Readonly(a,b));
        public static int FailedInitialization(int a, int b)
        { try { return AssemblyFailedInitialization.Value.CompareTo(a / b); } catch (TypeInitializationException e) { return e.InnerException is InvalidOperationException ? 23 : 29; } catch (DivideByZeroException) { return 31; } catch (OverflowException) { return 37; } }
    }
}
