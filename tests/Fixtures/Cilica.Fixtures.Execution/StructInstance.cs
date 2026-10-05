using System;

namespace Cilica.Fixtures.Execution
{
    public struct MutableInstanceValue
    {
        public long Number;
        public object Reference;
        public long Current => Number;
        public void Add(int delta)
        {
            Number = unchecked(Number + delta);
            Reference = new WideCell(Number);
        }
        public MutableInstanceValue Change(ref long extra, int delta)
        {
            Add(delta == 0 ? 7 : delta);
            extra ^= Number;
            if (delta == 0) throw new ArgumentException("mutated");
            return this;
        }
        public void Replace(MutableInstanceValue value) { this = value; }
    }
    public struct GenericInstanceValue<T>
    {
        public T Value;
        public T Swap(T replacement)
        {
            var previous = Value;
            Value = replacement;
            _ = new WideCell(17);
            return previous;
        }
    }
    public struct EmptyInstanceValue
    {
        public int Observe(int input, int mode)
        {
            _ = new WideCell(input);
            if (mode == 0) throw new ArgumentException("empty");
            return input ^ mode;
        }
    }
    public static class StructInstance
    {
        public static int Flow(int input, int mode)
        {
            var original = new MutableInstanceValue { Number = ((long)input << 32) | (uint)mode, Reference = new WideCell(input) };
            var copy = original;
            copy.Add(mode);
            original.Replace(copy);
            return unchecked((int)original.Current + (int)(copy.Number >> 32)) + (original.Reference == copy.Reference ? 3 : 7);
        }
        public static int Borrow(int input, int mode)
        {
            var value = new MutableInstanceValue { Number = input, Reference = new WideCell(mode) };
            var original = value;
            try
            {
                var result = value.Change(ref value.Number, mode);
                return unchecked((int)(result.Number ^ value.Number ^ original.Number)) + (result.Reference == value.Reference ? 11 : 13);
            }
            catch (ArgumentException) { return unchecked((int)value.Number + (value.Reference == original.Reference ? 17 : 19)); }
        }
        public static int Generic(int input, int mode)
        {
            var before = new MutableInstanceValue { Number = input, Reference = new WideCell(input) };
            var after = new MutableInstanceValue { Number = mode, Reference = new WideCell(mode) };
            var holder = new GenericInstanceValue<MutableInstanceValue> { Value = before };
            var returned = holder.Swap(after);
            return unchecked((int)(returned.Number + holder.Value.Number)) + (returned.Reference == before.Reference && holder.Value.Reference == after.Reference ? 23 : 29);
        }
        public static int Empty(int input, int mode)
        {
            EmptyInstanceValue value = default;
            try { return value.Observe(input, mode); }
            catch (ArgumentException) { return 31; }
        }
    }
}
