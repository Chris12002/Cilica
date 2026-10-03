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
        public static int CheckedStore(int kind, int index)
        {
            object?[] values = kind == 0 ? null! : new ArrayEcho[2];
            object? element = kind == 2 ? new object() : kind == 3 ? null : new ArrayEcho();
            values[index] = element;
            return values[index] == element ? 1 : 0;
        }
        public static int FaultOrder(int kind, int divisor)
        {
            object?[] values = kind == 0 ? null! : new ArrayEcho[1];
            values[7 / divisor] = new object();
            return 0;
        }
        public static int Rooted(int count, int value)
        {
            var values = new ProbeNode?[2];
            values[0] = new ProbeNode(value);
            var loaded = values[0];
            for (int i = 0; i < count; i++)
            {
                int[] scratch = new int[1];
                values[1] = new ProbeNode(unchecked(i + scratch[0]));
            }
            return unchecked(loaded!.Initial + values[1]!.Initial);
        }
        public static int InterfaceElements(int value)
        {
            IArrayEcho?[] contracts = new IArrayEcho?[2];
            contracts[0] = new ArrayEcho();
            return contracts[0]!.Echo(new int[value]).Length;
        }
        public static int WidenAndNest(int value)
        {
            object?[] outer = new object?[2];
            int[] inner = new int[value];
            outer[0] = inner;
            outer[1] = outer;
            return (outer[0] == inner ? 1 : 0) + (outer[1] == outer ? 1 : 0);
        }
        public static int ArrayTypeTest(int length)
        {
            object value = new int[length];
            return value is ProbeNode ? 0 : 1;
        }
        public static int ArrayCast(int length)
        {
            object value = new int[length];
            return ((ProbeNode)value).Initial;
        }
        public static int EmptyIdentity()
        {
            var first = new int[0]; var second = new int[0];
            return first == second ? 0 : 1;
        }
        public static int ReadArray(int[] values, int index) => values[index];
        public static int WriteArray(object?[] values, int index, object? value)
        {
            values[index] = value; return 17;
        }
        public static int Boundaries(int value)
        {
            object?[] values = new ArrayEcho[2];
            WriteArray(values, 0, new ArrayEcho());
            return ReadArray(new int[2], value);
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
