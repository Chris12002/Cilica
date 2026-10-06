namespace Cilica.Fixtures.Execution
{
    public class ConstantBase
    {
        private const string Kind = "base";
        internal const int Offset = 17;
        public int Value;
        public ConstantBase(int value) { Value = value; }
        public int Read() => unchecked(Value + Offset + Kind.Length);
    }
    public sealed class ConstantDerived : ConstantBase
    {
        public const long Wide = long.MinValue;
        public const bool Flag = true;
        public const string Empty = null;
        public int Other;
        public ConstantDerived(int value, int other) : base(value) { Other = other; }
    }
    public struct ConstantValue
    {
        public const string Kind = "value";
        public const double UnusedFloatingLiteral = 1.5;
        private const byte Byte = 255;
        public ConstantBase Reference;
        public int Value;
        public ConstantValue(int value) { Reference = new ConstantBase(value); Value = value; }
        public int Read() => unchecked(Reference.Read() + Value + Kind.Length + Byte);
    }
    public struct ConstantOnly { public const int Number = 11; public const string Text = "empty"; }
    public sealed class ConstantGeneric<T>
    {
        public const char Letter = 'z';
        public const uint Mask = uint.MaxValue;
        public T Value;
    }
    public static class MetadataConstantProbe
    {
        public static int Classes(int a, int b) { var value = new ConstantDerived(a, b); return unchecked(value.Read() + value.Other); }
        public static int Values(int a, int b) { var value = new ConstantValue(a); var copy = value; value.Value = b; return unchecked(value.Read() + copy.Read()); }
        public static int Arrays(int a, int b) { var values = new ConstantValue[2]; values[0] = new ConstantValue(a); values[1] = new ConstantValue(b); return unchecked(values[0].Read() + values[1].Read()); }
        public static int EmptyValues(int a, int b) { var values = new ConstantOnly[1]; var copy = values[0]; values[0] = copy; return unchecked(a + b + ConstantOnly.Number + ConstantOnly.Text.Length); }
        public static int ClosedGeneric(int a, int b) { var integer = new ConstantGeneric<int>(); integer.Value = a; var reference = new ConstantGeneric<ConstantBase>(); reference.Value = new ConstantBase(b); return unchecked(integer.Value + reference.Value.Read() + ConstantGeneric<int>.Letter); }
    }
}
