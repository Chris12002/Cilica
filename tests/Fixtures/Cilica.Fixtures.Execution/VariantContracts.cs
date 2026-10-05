namespace Cilica.Fixtures.Execution
{
    public interface IVariantContract<out T> { }
    public sealed class VariantContract<T> : IVariantContract<T> { }
    public static class VariantContracts
    {
        public static int Test(int input, int other)
        {
            object value = new VariantContract<WideCell>();
            return value is IVariantContract<object> ? input : other;
        }
    }
}
