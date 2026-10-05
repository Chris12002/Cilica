using System;

namespace Cilica.Fixtures.Execution
{
    public interface IDirectEquatable<T> { bool Equals(T value); }
    public struct DirectEquatableValue : IDirectEquatable<DirectEquatableValue>
    {
        public long Number;
        public object Reference;
        public bool Equals(DirectEquatableValue other)
        {
            _ = new WideCell(Number);
            return Number == other.Number && Reference == other.Reference;
        }
        public int DirectHash() => unchecked((int)(Number ^ (Number >> 32)));
        public override int GetHashCode() => DirectHash();
    }
    public struct RuntimeEquatableValue : IEquatable<RuntimeEquatableValue>
    {
        public int Number;
        public bool Equals(RuntimeEquatableValue other) => Number == other.Number;
    }
    public interface IDirectValue<T> { T Echo(T value); }
    public struct DirectGenericValue<T> : IDirectValue<T>
    {
        public T Value;
        public T Echo(T value) { var before = Value; Value = value; _ = new WideCell(13); return before; }
    }
    public static class DirectInterfaceValues
    {
        public static int Equality(int input, int mode)
        {
            var value = new DirectEquatableValue { Number = ((long)input << 32) | (uint)mode, Reference = new WideCell(input) };
            var copy = value;
            bool first = value.Equals(copy);
            copy.Number = unchecked(copy.Number + mode);
            bool second = value.Equals(copy);
            return value.DirectHash() + (first ? 3 : 7) + (second ? 11 : 13);
        }
        public static int Generic(int input, int mode)
        {
            var value = new DirectGenericValue<WidePayload> { Value = new WidePayload { Number = input, Reference = new WideCell(input) } };
            var original = value.Value;
            var replacement = new WidePayload { Number = mode, Reference = new WideCell(mode) };
            var returned = value.Echo(replacement);
            return unchecked((int)(returned.Number + value.Value.Number)) + (returned.Reference == original.Reference && value.Value.Reference == replacement.Reference ? 17 : 19);
        }
        public static int Boxed(int input, int mode)
        {
            object value = new DirectEquatableValue { Number = input };
            return value is IDirectEquatable<DirectEquatableValue> ? mode : 23;
        }
        public static int Constrained(int input, int mode) => new DirectEquatableValue { Number = input }.GetHashCode() ^ mode;
        public static int RuntimeContract(int input, int mode) => new RuntimeEquatableValue { Number = input }.Equals(new RuntimeEquatableValue { Number = mode }) ? 29 : 31;
    }
}
