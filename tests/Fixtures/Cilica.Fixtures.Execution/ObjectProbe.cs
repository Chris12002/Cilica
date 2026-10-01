#nullable enable
namespace Cilica.Fixtures.Execution
{
    public sealed class ProbeNode
    {
        private int _value;
        public readonly int Initial;
        public ProbeNode? Next;

        public ProbeNode(int value) { _value = value; Initial = value; }
        public int Change(int delta) => _value = unchecked(_value + delta);
        public void Connect(ProbeNode? next) => Next = next;
        public int ReadConnected() => Next!._value;
        public int Constant() => 17;
    }

    public static class ObjectProbe
    {
        public static int ReadNull(int value)
        {
            var first = new ProbeNode(value);
            first.Connect(null);
            return first.ReadConnected();
        }

        public static int Run(int left, int right)
        {
            var first = new ProbeNode(left);
            var second = new ProbeNode(right);
            first.Connect(second);
            return unchecked(first.Change(right) + first.ReadConnected() + first.Initial);
        }
    }
}
