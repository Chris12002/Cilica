using System;
namespace Cilica.Fixtures.Execution
{
    public abstract class ProtectedRoot
    {
        public int Number;
        public ValueVectorBox Reference;
        protected ProtectedRoot(int number, ValueVectorBox reference)
        {
            Number = number; Reference = reference;
            if (number < 0) throw new InvalidOperationException("base");
            var allocation = new ValueVectorBox(reference.Number); Reference.Number = unchecked(allocation.Number + 1);
        }
    }
    public sealed class ProtectedLeaf : ProtectedRoot
    {
        public int Extra;
        public ProtectedLeaf(int a, int b) : base(a, new ValueVectorBox(b)) { Extra = unchecked(a + b); }
    }
    public class ProtectedGenericRoot<T>
    {
        public T Value;
        protected ProtectedGenericRoot(T value) { Value = value; }
    }
    public sealed class ProtectedGenericLeaf<T> : ProtectedGenericRoot<T> { public ProtectedGenericLeaf(T value) : base(value) { } }
    public class ProtectedMiddle : ProtectedRoot
    {
        protected ProtectedMiddle(int a, ValueVectorBox b) : base(a,b) { Number = unchecked(Number + 1); }
    }
    public sealed class ProtectedDeep : ProtectedMiddle { public ProtectedDeep(int a, int b) : base(a,new ValueVectorBox(b)) { } }
    public class AssemblyProtectedRoot { public int Number; private protected AssemblyProtectedRoot(int value) { Number = value; } }
    public sealed class AssemblyProtectedLeaf : AssemblyProtectedRoot { public AssemblyProtectedLeaf(int value) : base(value) { } }
    public class EitherProtectedRoot { public int Number; protected internal EitherProtectedRoot(int value) { Number = value; } }
    public sealed class EitherProtectedLeaf : EitherProtectedRoot { public EitherProtectedLeaf(int value) : base(value) { } }
    public static class ProtectedConstructorProbe
    {
        public static int Flow(int a, int b)
        {
            try { var value = new ProtectedLeaf(a,b); return unchecked(value.Number + value.Extra + value.Reference.Number); }
            catch (InvalidOperationException e) { return e.Message == "base" ? 17 : -100; }
        }
        public static int Deep(int a, int b)
        {
            try { var value = new ProtectedDeep(a,b); return unchecked(value.Number + value.Reference.Number); }
            catch (InvalidOperationException e) { return e.Message == "base" ? 23 : -100; }
        }
        public static int Generic(int a, int b)
        {
            ValueVectorCell cell = default; cell.Number = a; cell.Wide = ((long)a << 32) | (uint)b; cell.Reference = new ValueVectorBox(b);
            var value = new ProtectedGenericLeaf<ValueVectorCell>(cell); return unchecked(value.Value.Number + (int)value.Value.Wide + value.Value.Reference.Number);
        }
        public static int Reference(int a, int b)
        {
            var reference = new ValueVectorBox(a); var value = new ProtectedGenericLeaf<ValueVectorBox>(reference);
            return value.Value == reference ? unchecked(reference.Number + b) : -100;
        }
        public static int Intersection(int a, int b) => unchecked(new AssemblyProtectedLeaf(a).Number + b);
        public static int Union(int a, int b) => unchecked(new EitherProtectedLeaf(a).Number + b);
    }
}
