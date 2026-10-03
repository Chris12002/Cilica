#nullable enable
namespace Cilica.Fixtures.Execution
{
    public interface IJoinRead { int Read(int value); }
    public interface IJoinLeft : IJoinRead { }
    public interface IJoinRight : IJoinRead { }
    public class JoinBase : IJoinRead
    {
        public int Value;
        public JoinBase(int value) { Value = value; }
        public virtual int Read(int value) => unchecked(Value + value);
    }
    public sealed class JoinLeft : JoinBase
    {
        public JoinLeft(int value) : base(value) { }
        public override int Read(int value) => unchecked(Value + value + 10);
    }
    public sealed class JoinRight : JoinBase
    {
        public JoinRight(int value) : base(value) { }
        public override int Read(int value) => unchecked(Value + value + 20);
    }
    public sealed class JoinContractLeft : IJoinLeft
    {
        public int Value;
        public JoinContractLeft(int value) { Value = value; }
        int IJoinRead.Read(int value) => unchecked(Value + value + 100);
    }
    public sealed class JoinContractRight : IJoinRight
    {
        public int Value;
        public JoinContractRight(int value) { Value = value; }
        public int Read(int value) => unchecked(Value + value + 200);
    }
    public static class ReferenceJoinProbe
    {
        public static IJoinLeft Left(int value) => new JoinContractLeft(value);
        public static IJoinRight Right(int value) => new JoinContractRight(value);
        public static int Class(int kind, int value) =>
            (kind == 0 ? new JoinLeft(value) : (JoinBase)new JoinRight(value)).Read(12 / value);
        public static int Interface(int kind, int value) =>
            (kind == 0 ? new JoinContractLeft(value) : (IJoinRead)new JoinContractRight(value)).Read(12 / value);
        public static int Parents(int kind, int value) =>
            (kind == 0 ? Left(value) : (IJoinRead)Right(value)).Read(value);
        public static int Nullable(int kind, int value) =>
            (kind < 0 ? null : kind == 0 ? Left(value) : (IJoinRead)Right(value))!.Read(12 / value);
        public static int Consume(IJoinRead reader, ProbeNode allocation) => reader.Read(allocation.Initial);
        public static int Rooted(int kind, int value) => Consume(
            kind == 0 ? new JoinContractLeft(value) : (IJoinRead)new JoinContractRight(value), new ProbeNode(value));
        public static int Field(int kind, int value) =>
            (kind == 0 ? new JoinLeft(value) : (JoinBase)new JoinRight(value)).Value;
    }
}
