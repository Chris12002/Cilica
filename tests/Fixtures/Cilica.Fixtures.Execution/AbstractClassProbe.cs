#nullable enable
using System;
namespace Cilica.Fixtures.Execution
{
    public interface IAbstractRead { int Read(int value); }
    public abstract class AbstractRead : IAbstractRead
    {
        public int State;
        public abstract int Read(int value);
        public abstract long Wide(long value);
        public abstract int Value { get; }
        public abstract void Set(int value);
        public abstract ProbeNode Make(ProbeNode previous);
    }
    public class AbstractConcrete : AbstractRead
    {
        public override int Read(int value) => unchecked(value + 17);
        public override long Wide(long value) => unchecked(value * 4294967297L);
        public override int Value => State;
        public override void Set(int value) { State = value; }
        public override ProbeNode Make(ProbeNode previous)
        {
            var later = new ProbeNode(unchecked(previous.Initial + 7));
            return new ProbeNode(unchecked(previous.Initial + later.Initial));
        }
    }
    public sealed class AbstractThrow : AbstractConcrete { public override int Read(int value) => 20 / value; }
    public sealed class AbstractInherited : AbstractConcrete { }
    public abstract class AbstractAgain : AbstractConcrete { public abstract override int Read(int value); }
    public sealed class AbstractFinal : AbstractAgain { public override int Read(int value) => unchecked(value - 23); }
    public class AbstractHidden : AbstractConcrete { public new virtual int Read(int value) => unchecked(value + 51); }
    public sealed class AbstractHiddenLeaf : AbstractHidden { public override int Read(int value) => unchecked(value - 61); }
    public abstract class AbstractGeneric<T> { public abstract T Echo(T value); }
    public sealed class AbstractGenericLeaf<T> : AbstractGeneric<T> { public override T Echo(T value) => value; }
    public abstract class AbstractUnused { public abstract int Read(int value); }
    public abstract class AbstractInline { public abstract ValueVectorCell Echo(ValueVectorCell value); }
    public static class AbstractClassProbe
    {
        public static AbstractRead Create(int kind)
        {

            if (kind == 0) return new AbstractConcrete();
            if (kind == 1) return new AbstractInherited();
            if (kind == 2) return new AbstractFinal();
            return new AbstractHiddenLeaf();
        }
        public static int Scalars(int a, int b)
        {
            var receiver = Create(a); receiver.Set(b);
            return unchecked(receiver.Read(b) + receiver.Value + (int)receiver.Wide((long)b * 4294967296L + a));
        }
        public static int References(int a, int b)
        {
            var receiver = Create(a); var first = new ProbeNode(b);
            var second = receiver.Make(first); var third = receiver.Make(second);
            return unchecked(first.Initial + second.Initial + third.Initial);
        }
        public static int ThroughInterface(int a, int b)
        {
            IAbstractRead receiver = Create(a); return receiver.Read(b);
        }
        public static int Hidden(int a, int b)
        {
            AbstractHidden receiver = new AbstractHiddenLeaf();
            AbstractRead original = receiver;
            return unchecked(receiver.Read(a) + original.Read(b));
        }
        public static int Reabstract(int a, int b)
        {
            AbstractAgain receiver = new AbstractFinal(); return unchecked(receiver.Read(a) + receiver.Read(b));
        }
        public static int Generic(int a, int b)
        {
            AbstractGeneric<int> scalar = new AbstractGenericLeaf<int>();
            AbstractGeneric<ProbeNode> reference = new AbstractGenericLeaf<ProbeNode>();
            var first = new ProbeNode(a); var second = reference.Echo(first); var pressure = new ProbeNode(b);
            return unchecked(scalar.Echo(b) + second.Initial + pressure.Initial + (first == second ? 1 : 0));
        }
        public static int NullOnly(int a, int b) { AbstractUnused receiver = null!; try { return receiver.Read(a); } catch (NullReferenceException) { return 13; } }
        public static int ArgumentFault(int a, int b) { AbstractUnused receiver = null!; try { return receiver.Read((a & 127) / b); } catch (DivideByZeroException) { return 11; } catch (NullReferenceException) { return 13; } }
        public static int CapturedFault(int a, int b)
        {
            AbstractRead receiver = null!; if (a >= 0) receiver = new AbstractThrow();
            try { return receiver.Read(10 / b); }
            catch (DivideByZeroException) { return 11; }
            catch (NullReferenceException) { return 13; }
        }
        public static int UnsupportedInline(int a, int b) { AbstractInline receiver = null!; return receiver.Echo(default).Number; }
    }
}
