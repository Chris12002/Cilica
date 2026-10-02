#nullable enable
namespace Cilica.Fixtures.Execution
{
    public class ExactDispatchBase
    {
        public virtual int Read(int value) => unchecked(100 + value);
        public virtual int Read(int left, int right) => unchecked(1000 + left + right);
        public virtual int Value => 11;
    }
    public class ExactDispatchMiddle : ExactDispatchBase
    {
        public override int Read(int value) => unchecked(200 + value);
        public override int Value => 22;
    }
    public sealed class ExactDispatchLeaf : ExactDispatchMiddle
    {
        public sealed override int Read(int value) => unchecked(300 + value);
        public int BaseRead(int value) => base.Read(value);
    }
    public sealed class ExactDispatchInherited : ExactDispatchMiddle { }
    public class ExactDispatchHidden : ExactDispatchMiddle
    {
        public new virtual int Read(int value) => unchecked(400 + value);
    }
    public sealed class ExactDispatchHiddenLeaf : ExactDispatchHidden
    {
        public override int Read(int value) => unchecked(500 + value);
    }
    public class ExactDispatchNonVirtual : ExactDispatchMiddle
    {
        public new int Read(int value) => unchecked(600 + value);
    }
    public sealed class ExactDispatchSibling : ExactDispatchBase
    {
        public override int Read(int value) => unchecked(700 + value);
    }

    public class VirtualNodeBase
    {
        public int Value;
        public ProbeNode? Held;
        public VirtualNodeBase(int value) { Value = value; }
        public virtual ProbeNode Make(int value) => new ProbeNode(value);
        public virtual void Adjust(int value) { Value = unchecked(Value + value); }
        public virtual VirtualNodeBase Identity() => this;
        public virtual int Recurse(int depth)
        {
            if (depth <= 0) return Value;
            return unchecked(Recurse(depth - 1) + 1);
        }
    }
    public sealed class VirtualNodeLeaf : VirtualNodeBase
    {
        public VirtualNodeLeaf(int value) : base(value) { }
        public override ProbeNode Make(int value)
        {
            var first = base.Make(value);
            VirtualNodeBase later = new VirtualNodeLate(first.Initial);
            return later.Make(value);
        }
        public override void Adjust(int value)
        {
            Held = new ProbeNode(value);
            base.Adjust(Held.Initial);
        }
        public override int Recurse(int depth)
        {
            if (depth <= 0) return Value;
            return unchecked(Recurse(depth - 1) + 2);
        }
    }
    // Discovered only by an override body, after the first dispatch closure pass.
    public sealed class VirtualNodeLate : VirtualNodeBase
    {
        public VirtualNodeLate(int value) : base(value) { }
        public override ProbeNode Make(int value) => new ProbeNode(unchecked(value + 1000));
    }

    public static class ClassDispatchProbe
    {
        public static ExactDispatchBase? Create(int kind)
        {
            if (kind < 0) return null;
            if (kind == 0) return new ExactDispatchBase();
            if (kind == 1) return new ExactDispatchMiddle();
            if (kind == 2) return new ExactDispatchLeaf();
            if (kind == 3) return new ExactDispatchInherited();
            if (kind == 4) return new ExactDispatchHidden();
            if (kind == 5) return new ExactDispatchHiddenLeaf();
            if (kind == 6) return new ExactDispatchNonVirtual();
            return new ExactDispatchSibling();
        }
        public static int ThroughMiddle(ExactDispatchMiddle receiver, int value) => receiver.Read(value);
        public static int Run(int kind, int value)
        {
            var receiver = Create(kind);
            int result = unchecked(receiver!.Read(value) + receiver.Read(value, 3) + receiver.Value);
            if (kind == 2) result = unchecked(result + ((ExactDispatchLeaf)receiver).BaseRead(value));
            if (kind == 4 || kind == 5) result = unchecked(result + ((ExactDispatchHidden)receiver).Read(value));
            if (kind > 0 && kind < 7) result = unchecked(result + ThroughMiddle((ExactDispatchMiddle)receiver, value));
            return result;
        }
        public static int ArgumentOrder(int kind, int value) => Create(kind)!.Read(10 / value);
        public static int Allocating(int kind, int value)
        {
            // Store each concrete branch into the declared local: this profile
            // conservatively joins distinct evaluation-stack classes as Object.
            VirtualNodeBase receiver;
            if (kind == 0) receiver = new VirtualNodeBase(value);
            else receiver = new VirtualNodeLeaf(value);
            var first = receiver.Make(value);
            receiver.Adjust(first.Initial);
            var second = receiver.Make(receiver.Value);
            if (receiver.Identity() != receiver) return -1;
            return unchecked(first.Initial + second.Initial + receiver.Value);
        }
        public static int Recursive(int kind, int depth)
        {
            VirtualNodeBase receiver;
            if (kind == 0) receiver = new VirtualNodeBase(7);
            else receiver = new VirtualNodeLeaf(7);
            return receiver.Recurse(depth);
        }
    }
}
