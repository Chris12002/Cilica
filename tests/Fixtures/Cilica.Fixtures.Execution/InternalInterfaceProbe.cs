using System;
namespace Cilica.Fixtures.Execution
{
    internal interface ILocalValue { int Read(int amount); }
    internal interface ILocalMutable : ILocalValue { int Number { get; set; } void Set(int value); }
    internal interface ILocalGeneric<T> { T Get(T value); }
    public interface IPublicValue { int Read(int amount); }
    public class LocalValueImplementation : ILocalMutable, IPublicValue
    {
        public int Number { get; set; }
        public virtual int Read(int amount) => unchecked(Number + amount);
        public void Set(int value) { Number = value; }
        int ILocalValue.Read(int amount) { var allocation = new ValueVectorBox(amount); return unchecked(Number - allocation.Number); }
    }
    public sealed class LocalValueDerived : LocalValueImplementation { public override int Read(int amount) => unchecked(Number * amount); }
    internal sealed class LocalGenericValue<T> : ILocalGeneric<T> { public T Get(T value) => value; }
    public static class InternalInterfaceProbe
    {
        public static int Internal(int a, int b) { var owner = new LocalValueImplementation(); owner.Number = a; ILocalValue value = owner; return value.Read(b); }
        public static int Public(int a, int b) { var owner = new LocalValueImplementation(); owner.Number = a; IPublicValue value = owner; return value.Read(b); }
        public static int Inherited(int a, int b) { var owner = new LocalValueDerived(); owner.Number = a; ILocalMutable value = owner; value.Set(b); return unchecked(value.Read(a) + value.Number); }
        public static int ClassSlot(int a, int b) { LocalValueImplementation value = new LocalValueDerived(); value.Number = a; return value.Read(b); }
        public static int Generic(int a, int b) { ILocalGeneric<ValueVectorCell> values = new LocalGenericValue<ValueVectorCell>(); ValueVectorCell cell = default; cell.Number = a; cell.Wide = ((long)a << 32) | (uint)b; cell.Reference = new ValueVectorBox(b); var result = values.Get(cell); return unchecked(result.Number + (int)result.Wide + result.Reference.Number); }
        public static int TypeChecks(int a, int b)
        {
            object owner = new LocalValueImplementation();
            if (!(owner is ILocalMutable)) return -100;
            var value = (ILocalMutable)owner; value.Number = a;
            var values = new ILocalMutable[1]; values[0] = value; return unchecked(values[0].Number + b);
        }
        public static int Null(int a, int b) { ILocalValue value = null; try { return value.Read(a); } catch (NullReferenceException) { return b; } }
    }
}
