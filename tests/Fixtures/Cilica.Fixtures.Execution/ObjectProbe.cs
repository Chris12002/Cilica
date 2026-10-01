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
        public int AllocateAndRead(int value)
        {
            var other = new ProbeNode(value);
            return unchecked(_value + other._value);
        }
        public static ProbeNode Create(int value) => new ProbeNode(value);
        public int CheckedChange(int delta) => checked(_value + delta);
    }

    public sealed class ProbeWrapper
    {
        public ProbeNode Child;
        public ProbeWrapper(int value) { Child = new ProbeNode(value); }
        public int Read(int delta) => Child.Change(delta);
    }

    public static class ObjectProbe
    {
        public static int Constructing(int left, int right)
        {
            var wrapper = new ProbeWrapper(left);
            var later = new ProbeNode(right);
            return wrapper.Read(later.Initial);
        }

        public static int AcrossCalls(int left, int right)
        {
            var first = ProbeNode.Create(left);
            var second = ProbeNode.Create(right);
            first.Connect(second);
            return unchecked(first.AllocateAndRead(right) + first.ReadConnected());
        }

        public static int Checked(int left, int right) => new ProbeNode(left).CheckedChange(right);

        public static int ArgumentBeforeNull(int left, int right)
        {
            ProbeNode? node = null;
            return node!.Change(checked(left + right));
        }

        public static int Division(int left, int right) => new ProbeNode(left / right).Constant();

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
