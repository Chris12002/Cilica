namespace Cilica.Fixtures.Execution
{

    public interface IExactRead
    {
        int Read(int value);
        int Read(int left, int right);
        int Value { get; }
    }
    public interface IExactSecond { int Read(int value); }
    public interface IExactDerived : IExactRead { }
    public interface IExactLeft : IExactRead { }
    public interface IExactRight : IExactRead { }
    public interface IExactDiamond : IExactLeft, IExactRight { }

    public class ExactInterfaceBase : IExactRead, IExactSecond
    {
        public virtual int Read(int value) => unchecked(100 + value);
        public int Read(int left, int right) => unchecked(1000 + left + right);
        public virtual int Value => 11;
    }
    public class ExactInterfaceOverride : ExactInterfaceBase
    {
        public override int Read(int value) => unchecked(200 + value);
        public override int Value => 22;
    }
    public class ExactInterfaceHidden : ExactInterfaceOverride
    {
        public new virtual int Read(int value) => unchecked(300 + value);
    }
    public sealed class ExactInterfaceReimplemented : ExactInterfaceHidden, IExactRead
    {
        public override int Read(int value) => unchecked(400 + value);
    }
    public sealed class ExactInterfaceNonVirtualHidden : ExactInterfaceOverride
    {
        public new int Read(int value) => unchecked(500 + value);
    }
    public sealed class ExactInterfaceInherited : ExactInterfaceOverride { }
    public class ExactInterfaceExplicit : IExactRead, IExactSecond
    {
        int IExactRead.Read(int value) => unchecked(600 + value);
        int IExactRead.Read(int left, int right) => unchecked(6000 + left + right);
        int IExactRead.Value => 66;
        int IExactSecond.Read(int value) => unchecked(700 + value);
        public int Read(int value) => unchecked(800 + value);
    }
    public sealed class ExactInterfaceExplicitInherited : ExactInterfaceExplicit
    {
        public new int Read(int value) => unchecked(900 + value);
    }
    public sealed class ExactInterfaceExplicitReimplemented : ExactInterfaceExplicit, IExactRead
    {
        public new int Read(int value) => unchecked(1000 + value);
        public int Read(int left, int right) => unchecked(10000 + left + right);
        public int Value => 101;
    }
    public sealed class ExactInterfaceReexplicit : ExactInterfaceBase, IExactRead
    {
        int IExactRead.Read(int value) => unchecked(1100 + value);
        int IExactRead.Read(int left, int right) => unchecked(11000 + left + right);
        int IExactRead.Value => 111;
    }
    public class ExactInterfacePlainBase
    {
        public int Read(int value) => unchecked(1200 + value);
        public int Read(int left, int right) => unchecked(12000 + left + right);
        public int Value => 121;
    }
    public sealed class ExactInterfaceFromPlain : ExactInterfacePlainBase, IExactRead { }
    public sealed class ExactInterfaceDiamond : ExactInterfaceBase, IExactDiamond { }
    public sealed class ExactInterfaceUnrelated
    {
        public int Read(int value) => value;
    }
}
