namespace Cilica.Fixtures.Execution
{
    public static class InterfaceCallProbe
    {
        public static int Run(int kind, int value)
        {
            IExactRead reader;
            if (kind == 0) reader = new ExactInterfaceBase();
            else if (kind == 1) reader = new ExactInterfaceOverride();
            else if (kind == 2) reader = new ExactInterfaceHidden();
            else if (kind == 3) reader = new ExactInterfaceReimplemented();
            else if (kind == 4) reader = new ExactInterfaceNonVirtualHidden();
            else if (kind == 5) reader = new ExactInterfaceInherited();
            else if (kind == 6) reader = new ExactInterfaceExplicit();
            else if (kind == 7) reader = new ExactInterfaceExplicitInherited();
            else if (kind == 8) reader = new ExactInterfaceExplicitReimplemented();
            else if (kind == 9) reader = new ExactInterfaceReexplicit();
            else if (kind == 10) reader = new ExactInterfaceFromPlain();
            else reader = new ExactInterfaceDiamond();
            return unchecked(reader.Read(value) + reader.Read(value, 3) + reader.Value);
        }

        public static int OtherContract(int value)
        {
            IExactSecond reader = new ExactInterfaceExplicit();
            return reader.Read(value);
        }

        public static int Diamond(int value)
        {
            IExactDiamond reader = new ExactInterfaceDiamond();
            return reader.Read(value);
        }

        public static int ReferenceFlow(int value)
        {
            IFrameBox box = new FirstFrameBox();
            box.Set(value);
            IFrameBox next = box.Next();
            next.Set(unchecked(value + 1));
            IFrameBox third = next.Next();
            third.Set(unchecked(value + 2));
            return unchecked(box.Read() + next.Read() + third.Read());
        }

        public static int NullCall(int divisor)
        {
            // The argument fault must precede the call's null-receiver fault.
            IExactRead reader = null;
            return reader.Read(12 / divisor);
        }

        public static int Recursive(int depth)
        {
            IFrameRecursion reader = new FrameRecursion();
            return reader.Count(depth);
        }

        public static int MetadataOnlySlot(int value)
        {
            IExactRead reader = new FrameSlotImplementation();
            return reader.Read(value);
        }

        public static int MixedClassCall(int value)
        {
            ExactInterfaceBase instance = new ExactInterfaceOverride();
            IExactRead reader = instance;
            return unchecked(reader.Read(value) + instance.Read(value));
        }

        public static int ThroughContract(IExactRead reader, int value) { return reader.Read(value); }
        public static int ContractBoundary(int value) { return ThroughContract(new ExactInterfaceDiamond(), value); }

        public static int ThroughBox(IFrameBox box, int value)
        {
            box.Set(value);
            return box.Read();
        }
        public static int BoxBoundary(int value) { return ThroughBox(new FirstFrameBox(), value); }

        public static int MutuallyRecursive(int depth)
        {
            IFrameRecursion reader = new MutualFrameRecursion();
            return reader.Count(depth);
        }
    }

    public interface IFrameBox
    {
        void Set(int value);
        int Read();
        IFrameBox Next();
    }
    public class FirstFrameBox : IFrameBox
    {
        public int Value;
        public IFrameBox Link;
        public void Set(int value) { Value = value; }
        public int Read() { return Value; }
        public IFrameBox Next()
        {
            IExactSecond reader = new ExactInterfaceExplicit();
            Link = new SecondFrameBox();
            Link.Set(reader.Read(Value));
            return Link;
        }
    }
    public sealed class SecondFrameBox : FirstFrameBox
    {
        // Both the interface mapping and the inherited nonvirtual body stay on FirstFrameBox.
    }

    public interface IFrameRecursion { int Count(int depth); }
    public sealed class FrameRecursion : IFrameRecursion
    {
        public int Count(int depth)
        {
            if (depth == 0) return 0;
            IFrameRecursion next = this;
            return unchecked(1 + next.Count(depth - 1));
        }
    }

    public sealed class OtherFrameRecursion : IFrameRecursion
    {
        public int Count(int depth)
        {
            if (depth == 0) return 0;
            IFrameRecursion next = new MutualFrameRecursion();
            return unchecked(1 + next.Count(depth - 1));
        }
    }

    public sealed class MutualFrameRecursion : IFrameRecursion
    {
        public int Count(int depth)
        {
            if (depth == 0) return 0;
            IFrameRecursion next = new OtherFrameRecursion();
            return unchecked(1 + next.Count(depth - 1));
        }
    }

    public class FrameSlotBase
    {
        // This body is outside the supported IL profile, but only its slot declaration is needed.
        public virtual int Read(int value) { return value.ToString().Length; }
    }
    public sealed class FrameSlotImplementation : FrameSlotBase, IExactRead
    {
        public override int Read(int value) { return unchecked(value + 31); }
        public int Read(int left, int right) { return unchecked(left + right); }
        public int Value { get { return 31; } }
    }
}
