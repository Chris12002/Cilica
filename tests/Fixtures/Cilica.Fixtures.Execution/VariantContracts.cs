namespace Cilica.Fixtures.Execution
{
    public interface IVariantContract<out T> { }
    public sealed class VariantContract<T> : IVariantContract<T> { }
    public interface IVariantChild<out T> : IVariantContract<T> { }
    public interface IContravariant<in T> { }
    public interface IInvariant<T> { }
    public class VarianceBase { }
    public sealed class VarianceDerived : VarianceBase { }
    public sealed class VariantChild<T> : IVariantChild<T> { }
    public sealed class Contravariant<T> : IContravariant<T> { }
    public sealed class Invariant<T> : IInvariant<T> { }
    public static class VariantContracts
    {
        public static int Test(int input, int other)
        {
            object value = new VariantContract<WideCell>();
            return value is IVariantContract<object> ? input : other;
        }
        private static int Covariant(IVariantContract<VarianceBase> value) => value == null ? 0 : 3;
        private static int Contravariant(IContravariant<VarianceDerived> value) => value == null ? 0 : 5;
        public static int Assignments(int input, int other)
        {
            object value = other == 0 ? (object)new VariantChild<int>() : new VariantChild<VarianceDerived>();
            int bits = value is IVariantContract<VarianceBase> ? 1 : 0;
            bits += value is IVariantContract<object> ? 2 : 0;
            bits += value is IVariantContract<int> ? 4 : 0;
            object invariant = new Invariant<VarianceDerived>();
            bits += invariant is IInvariant<VarianceBase> ? 8 : 0;
            object contra = new Contravariant<VarianceDerived>();
            bits += contra is IContravariant<VarianceBase> ? 16 : 0;
            return unchecked(input + bits + Covariant(new VariantChild<VarianceDerived>()) + Contravariant(new Contravariant<VarianceBase>()));
        }
        public static int Nested(int input, int other)
        {
            object value = new VariantChild<IVariantContract<VarianceDerived>>();
            int bits = value is IVariantContract<IVariantContract<VarianceBase>> ? 1 : 0;
            object vector = new VariantChild<VarianceDerived>[1];
            bits += vector is IVariantContract<VarianceBase>[] ? 2 : 0;
            object arrayArgument = new VariantChild<VarianceDerived[]>();
            bits += arrayArgument is IVariantContract<VarianceBase[]> ? 4 : 0;
            return unchecked(input + other + bits);
        }
        public static int Strings(int input, int other)
        {
            object value = new VariantContract<string>();
            return value is IVariantContract<object> ? input : other;
        }
        public static int StringInterface(int input, int other)
        {
            object value = "text";
            return value is System.Collections.Generic.IEnumerable<char> ? input : other;
        }
        public static int ArrayInterface(int input, int other)
        {
            object value = new int[1];
            return value is System.Collections.IList ? input : other;
        }
    }
}
