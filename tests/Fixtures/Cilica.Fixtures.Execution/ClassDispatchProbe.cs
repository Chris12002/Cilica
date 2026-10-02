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
}
