#nullable enable
namespace Cilica.Fixtures.Execution
{
    public static class ArrayProbe
    {
        public static int Integers(int length, int index, int value)
        {
            int[] values = new int[length];
            int[] alias = Echo(values);
            alias[index] = value;
            return unchecked(values[index] + values.Length);
        }
        private static int[] Echo(int[] values) => values;

        public static int Zeroes(int length) => (new int[length])[0];
        public static int NullLength() => ((int[])null!).Length;
        public static int NullLoad(int index) => ((int[])null!)[index];
        public static int NullStore(int index)
        {
            ((int[])null!)[index] = 7;
            return 0;
        }
        public static int References()
        {
            var holder = new ArrayHolder();
            holder.Values = new object?[2];
            var value = new object();
            object?[] alias = holder.Values;
            alias[0] = value;
            return (holder.Values[0] == value ? 1 : 0) + (holder.Values[1] == null ? 1 : 0);
        }
        public static int Dispatch(int length)
        {
            IArrayEcho echo = new ArrayEcho();
            return echo.Echo(new int[length]).Length;
        }
        public static int Virtual(int length)
        {
            ArrayBase echo = new ArrayDerived();
            return echo.Echo(new int[length]).Length;
        }
        public static int Covariance(int length)
        {
            IArrayEcho[] contracts = new ArrayEcho[length];
            return contracts.Length;
        }
    }
    public class ArrayBase { public virtual int[] Echo(int[] values) => values; }
    public sealed class ArrayDerived : ArrayBase { public override int[] Echo(int[] values) => values; }
    public sealed class ArrayHolder { public object?[]? Values; }
    public interface IArrayEcho { int[] Echo(int[] values); }
    public sealed class ArrayEcho : IArrayEcho { public int[] Echo(int[] values) => values; }
}
