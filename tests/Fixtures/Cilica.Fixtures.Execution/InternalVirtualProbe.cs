#nullable enable
using System;
namespace Cilica.Fixtures.Execution
{
    
    internal abstract class InternalVirtualRead
    {
        public int State;
        internal abstract int Read(int value);
        internal abstract long Wide(long value);
        internal abstract int Value { get; }
        internal abstract void Set(int value);
        internal abstract ProbeNode Make(ProbeNode previous);
    }
    internal class InternalVirtualConcrete : InternalVirtualRead
    {
        internal override int Read(int value) => unchecked(value + 17);
        internal override long Wide(long value) => unchecked(value * 4294967297L);
        internal override int Value => State;
        internal override void Set(int value) { State = value; }
        internal override ProbeNode Make(ProbeNode previous)
        {
            var later = new ProbeNode(unchecked(previous.Initial + 7));
            return new ProbeNode(unchecked(previous.Initial + later.Initial));
        }
    }
    internal sealed class InternalVirtualThrow : InternalVirtualConcrete { internal override int Read(int value) => 20 / value; }
    internal sealed class InternalVirtualInherited : InternalVirtualConcrete { }
    internal abstract class InternalVirtualAgain : InternalVirtualConcrete { internal abstract override int Read(int value); }
    internal sealed class InternalVirtualFinal : InternalVirtualAgain { internal override int Read(int value) => unchecked(value - 23); }
    internal class InternalVirtualHidden : InternalVirtualConcrete { internal new virtual int Read(int value) => unchecked(value + 51); }
    internal sealed class InternalVirtualHiddenLeaf : InternalVirtualHidden { internal override int Read(int value) => unchecked(value - 61); }
    internal abstract class InternalVirtualGeneric<T> { internal abstract T Echo(T value); }
    internal sealed class InternalVirtualGenericLeaf<T> : InternalVirtualGeneric<T> { internal override T Echo(T value) => value; }
    internal abstract class InternalVirtualUnused { internal abstract int Read(int value); }
    internal abstract class InternalVirtualInline { internal abstract ValueVectorCell Echo(ValueVectorCell value); }
    public static class InternalVirtualProbe
    {
        internal static InternalVirtualRead Create(int kind)
        {

            if (kind == 0) return new InternalVirtualConcrete();
            if (kind == 1) return new InternalVirtualInherited();
            if (kind == 2) return new InternalVirtualFinal();
            return new InternalVirtualHiddenLeaf();
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
        public static int Hidden(int a, int b)
        {
            InternalVirtualHidden receiver = new InternalVirtualHiddenLeaf();
            InternalVirtualRead original = receiver;
            return unchecked(receiver.Read(a) + original.Read(b));
        }
        public static int Reabstract(int a, int b)
        {
            InternalVirtualAgain receiver = new InternalVirtualFinal(); return unchecked(receiver.Read(a) + receiver.Read(b));
        }
        public static int Generic(int a, int b)
        {
            InternalVirtualGeneric<int> scalar = new InternalVirtualGenericLeaf<int>();
            InternalVirtualGeneric<ProbeNode> reference = new InternalVirtualGenericLeaf<ProbeNode>();
            var first = new ProbeNode(a); var second = reference.Echo(first); var pressure = new ProbeNode(b);
            return unchecked(scalar.Echo(b) + second.Initial + pressure.Initial + (first == second ? 1 : 0));
        }
        public static int NullOnly(int a, int b) { InternalVirtualUnused receiver = null!; try { return receiver.Read(a); } catch (NullReferenceException) { return 13; } }
        public static int ArgumentFault(int a, int b) { InternalVirtualUnused receiver = null!; try { return receiver.Read((a & 127) / b); } catch (DivideByZeroException) { return 11; } catch (NullReferenceException) { return 13; } }
        public static int CapturedFault(int a, int b)
        {
            InternalVirtualRead receiver = null!; if (a >= 0) receiver = new InternalVirtualThrow();
            try { return receiver.Read(10 / b); }
            catch (DivideByZeroException) { return 11; }
            catch (NullReferenceException) { return 13; }
        }
        public static int UnsupportedInline(int a, int b) { InternalVirtualInline receiver = null!; return receiver.Echo(default).Number; }
    }
}
