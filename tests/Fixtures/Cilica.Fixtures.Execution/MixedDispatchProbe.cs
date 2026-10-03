#nullable enable
namespace Cilica.Fixtures.Execution
{
    public interface IMixedRead { int Read(int value); }
    public class MixedDispatchBase : IMixedRead
    {
        public virtual int Read(int value) => unchecked(100 + value);
        int IMixedRead.Read(int value) => unchecked(1000 + value);
    }
    public class MixedDispatchOverride : MixedDispatchBase
    {
        public override int Read(int value) => unchecked(200 + value);
        public int BaseRead(int value) => base.Read(value);
    }
    public class MixedDispatchHidden : MixedDispatchOverride
    {
        public new virtual int Read(int value) => unchecked(300 + value);
    }
    public class MixedDispatchLeaf : MixedDispatchHidden, IMixedRead
    {
        public sealed override int Read(int value) => unchecked(400 + value);
        int IMixedRead.Read(int value) => unchecked(4000 + value);
    }
    public sealed class MixedDispatchInherited : MixedDispatchLeaf { }
    public sealed class MixedDispatchReimplemented : MixedDispatchOverride, IMixedRead
    {
        public override int Read(int value) => unchecked(500 + value);
    }

    public interface IMixedFactory
    {
        ProbeNode Make(int value);
        void Set(int value);
    }
    public class MixedFactoryBase : IMixedFactory
    {
        public ProbeNode? Held;
        public virtual ProbeNode Make(int value) => new ProbeNode(value);
        public virtual void Set(int value) { Held = Make(value); }
        ProbeNode IMixedFactory.Make(int value) => Make(unchecked(value + 10));
        void IMixedFactory.Set(int value) => Set(unchecked(value + 20));
    }
    public sealed class MixedFactoryLeaf : MixedFactoryBase
    {
        public override ProbeNode Make(int value)
        {
            // Discovered only while translating a selected class override.
            MixedFactoryBase later = new MixedFactoryLate();
            return later.Make(value);
        }
        public override void Set(int value) { base.Set(unchecked(value + 100)); }
    }
    public sealed class MixedFactoryLate : MixedFactoryBase
    {
        public override ProbeNode Make(int value) => new ProbeNode(unchecked(value + 1000));
    }

    public static class MixedDispatchProbe
    {
        public static MixedDispatchBase? Create(int kind)
        {
            if (kind < 0) return null;
            if (kind == 0) return new MixedDispatchBase();
            if (kind == 1) return new MixedDispatchOverride();
            if (kind == 2) return new MixedDispatchHidden();
            if (kind == 3) return new MixedDispatchLeaf();
            if (kind == 4) return new MixedDispatchInherited();
            return new MixedDispatchReimplemented();
        }
        public static int ClassOnly(int kind, int value) => Create(kind)!.Read(value);
        public static int ClassFirst(int kind, int value)
        {
            var instance = Create(kind);
            int result = instance!.Read(12 / value);
            IMixedRead contract = instance;
            result = unchecked(result + contract.Read(value));
            if (kind > 0) result = unchecked(result + ((MixedDispatchOverride)instance).BaseRead(value));
            if (kind > 1 && kind < 5) result = unchecked(result + ((MixedDispatchHidden)instance).Read(value));
            return result;
        }
        public static int InterfaceFirst(int kind, int value)
        {
            var instance = Create(kind);
            IMixedRead contract = instance!;
            return unchecked(contract.Read(12 / value) + instance!.Read(value));
        }
        public static int Allocating(int kind, int value)
        {
            MixedFactoryBase instance;
            if (kind == 0) instance = new MixedFactoryBase();
            else instance = new MixedFactoryLeaf();
            var first = instance.Make(value);
            IMixedFactory contract = instance;
            contract.Set(first.Initial);
            var second = contract.Make(value);
            return unchecked(first.Initial + second.Initial + instance.Held!.Initial);
        }
    }
}
