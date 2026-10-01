#nullable enable
namespace Cilica.Fixtures.Execution
{
    public static class ObjectFlowProbe
    {
        public static int Chain(int count, int seed)
        {
            var first = new ProbeNode(seed);
            var tail = first;
            for (int i = 0; i < count; i++)
            {
                var next = new ProbeNode(unchecked(seed + i));
                tail.Connect(next); tail = next;
            }
            int total = 0; ProbeNode? current = first;
            while (current != null)
            {
                total = unchecked(total + current.Initial);
                current = current.Next;
            }
            return total;
        }

        public static int Choose(int flag, int seed) => ReadChosen(flag > 0 ? null : new ProbeNode(seed));
        private static int ReadChosen(ProbeNode? chosen) => chosen == null ? 19 : chosen.AllocateAndRead(7);

        public static int ChooseObject(int flag, int seed)
        {
            object chosen = flag == 0 ? new ProbeNode(seed) : new ProbeWrapper(seed);
            return HasObject(chosen);
        }
        private static int HasObject(object value) => value == null ? 0 : 1;

        public static int Identity(int flag, int seed)
        {
            var first = new ProbeNode(seed);
            var second = flag == 0 ? first : new ProbeNode(seed);
            return first == second ? 1 : 0;
        }

        public static int FaultLoop(int count, int seed)
        {
            var node = new ProbeNode(seed);
            int total = seed;
            for (int i = 0; i < count; i++)
            {
                var other = new ProbeNode(i);
                node.Connect(other);
                total = node.CheckedChange(total);
            }
            return total;
        }
    }
}
