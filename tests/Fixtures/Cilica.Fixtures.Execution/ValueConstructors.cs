using System;

namespace Cilica.Fixtures.Execution
{
    public struct ConstructedValue
    {
        public readonly long First;
        public readonly WidePayload Nested;
        public int Tail;
        public ConstructedValue(int input, int mode)
        {
            First = ((long)input << 32) | 31L;
            Nested = new WidePayload { Number = First, Reference = new WideCell(First) };
            Tail = mode;
            if (mode == 0) throw new ArgumentException("constructor probe");
            Tail = checked(input + mode);
        }
    }
    public readonly struct GenericConstructed<T>
    {
        public readonly T Value;
        public readonly int Marker;
        public GenericConstructed(T value, int marker) { Value = value; Marker = marker; }
    }
    public struct BorrowConstructed
    {
        public readonly long Number;
        public BorrowConstructed(ref BorrowConstructed other, long value) { Number = value; }
    }
    public struct EmptyConstructed
    {
        public EmptyConstructed(int mode)
        {
            _ = new WideCell(mode);
            if (mode == 0) throw new ArgumentException("empty constructor probe");
        }
    }
    public static class ValueConstructors
    {
        private static ConstructedValue Create(int input, int mode) => new ConstructedValue(input, mode);
        public static int Boxed(int input, int mode)
        {
            try
            {
                object boxed = Create(input, mode);
                var copy = (ConstructedValue)boxed;
                return unchecked((int)(copy.First >> 32) ^ copy.Tail ^ (int)copy.Nested.Number)
                    + (copy.Nested.Reference == null ? 3 : 7);
            }
            catch (OverflowException) { return 21; }
            catch (ArgumentException) { return 31; }
        }
        public static int Generic(int input, int mode)
        {
            try
            {
                var value = new GenericConstructed<ConstructedValue>(Create(input, mode), 23);
                var copy = new GenericConstructed<ConstructedValue>(value.Value, mode);
                return unchecked((int)(copy.Value.First >> 32) + copy.Marker + value.Marker)
                    + (value.Value.Nested.Reference == copy.Value.Nested.Reference ? 13 : 17);
            }
            catch (OverflowException) { return 41; }
            catch (ArgumentException) { return 53; }
        }
        public static int Existing(int input, int mode)
        {
            ConstructedValue value = default;
            try { value = new ConstructedValue(input, mode); }
            catch (OverflowException) { return value.Tail + 61; }
            catch (ArgumentException) { return value.Tail + 73; }
            return unchecked(value.Tail ^ (int)(value.First >> 32));
        }
        public static int Borrow(int input, int mode)
        {
            BorrowConstructed other = default;
            var value = new BorrowConstructed(ref other, ((long)input << 32) | (uint)mode);
            return unchecked((int)((value.Number >> 32) ^ other.Number));
        }
        public static int Empty(int input, int mode)
        {
            try { _ = new EmptyConstructed(mode); return input; }
            catch (ArgumentException) { return 89; }
        }
    }
}
