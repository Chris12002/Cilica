using System;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public interface IParameterNode { int Read(); }
    public class ParameterNode : IParameterNode
    {
        public int Value;
        public ParameterNode(int value) { Value = value; }
        public int Read() => Value;
    }
    public sealed class ParameterChild : ParameterNode { public ParameterChild(int value) : base(value) { } }
    public struct ParameterPayload { public int Value; public ValueVectorBox Node; }
    public sealed class ParameterReceiver
    {
        public int Bias;
        public int Read(IParameterNode node) { var pressure = new ValueVectorBox(Bias); return unchecked(node.Read() + pressure.Number); }
    }
    public static class DelegateParameterProbe
    {
        public static int ReadInterface(IParameterNode node) { var pressure = new ValueVectorBox(node.Read()); return pressure.Number; }
        public static int ReadClass(ParameterNode node) { if (node == null) return 23; var pressure = new ValueVectorBox(node.Value); return pressure.Number; }
        public static int Narrow(ParameterChild node) => node.Value;
        public static bool Positive(IParameterNode node) => node.Read() > 0;
        public static void Change(ParameterNode node) { var pressure = new ValueVectorBox(7); node.Value = unchecked(node.Value + pressure.Number); }
        public static int Store(ParameterNode[] nodes, int value) { nodes[0] = new ParameterNode(value); return nodes[0].Value; }
        public static int ReadPayload(IParameterNode node, ParameterPayload payload) { payload.Value = unchecked(payload.Value + node.Read()); payload.Node.Number++; return unchecked(payload.Value + payload.Node.Number); }
        public static int Interface(int a, int b)
        { var node = new ParameterNode(a); var call = new Func<ParameterNode, int>(ReadInterface); return unchecked(call(node) + Narrow(new ParameterChild(b))); }
        public static int Class(int a, int b)
        { var node = new ParameterChild(a); var call = new Func<ParameterChild, int>(ReadClass); return unchecked(call(node) + b); }
        public static int Boolean(int a, int b)
        { var nodes = new ParameterNode[] { new ParameterNode(a), new ParameterChild(b) }; return nodes.Any(new Func<ParameterNode, bool>(Positive)) ? 17 : 31; }
        public static int Action(int a, int b)
        { var node = new ParameterChild(a); var call = new Action<ParameterChild>(Change); call(node); return unchecked(node.Value + b); }
        public static int Instance(int a, int b)
        { var receiver = new ParameterReceiver(); receiver.Bias = b; var call = new Func<ParameterChild, int>(receiver.Read); return call(new ParameterChild(a)); }
        public static int Null(int a, int b)
        { var call = new Func<ParameterChild, int>(ReadClass); return unchecked(call((a & 1) == 0 ? null : new ParameterChild(a)) + b); }
        public static int ArrayStore(int a, int b)
        {
            var nodes = new ParameterChild[] { new ParameterChild(a) }; var call = new Func<ParameterChild[], int, int>(Store);
            try { return call(nodes, b); } catch (ArrayTypeMismatchException) { return unchecked(nodes[0].Value + b); }
        }
        public static int Inline(int a, int b)
        {
            var payload = new ParameterPayload(); payload.Value = a; payload.Node = new ValueVectorBox(b);
            var call = new Func<ParameterChild, ParameterPayload, int>(ReadPayload); var result = call(new ParameterChild(b), payload);
            return unchecked(result + payload.Value + payload.Node.Number);
        }
    }
}
