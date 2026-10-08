#nullable enable
using System;
namespace Cilica.Fixtures.Execution
{
    internal abstract class ProtectedVirtualRead
    {
        public int State;
        protected abstract int Read(int value);
        protected abstract long Wide(long value);
        protected abstract int Value { get; }
        protected abstract void Set(int value);
        protected abstract ProbeNode Make(ProbeNode previous);
        public int Run(int a,int b) { Set(b); return unchecked(Read(b)+Value+(int)Wide((long)b*4294967296L+a)); }
        public int ReadThrough(int value) => Read(value);
        public ProbeNode MakeThrough(ProbeNode previous) => Make(previous);
        public static int NullCall(int a,int b)
        {
            ProtectedVirtualRead receiver=null!;
            try { return receiver.Read((a&127)/b); }
            catch(DivideByZeroException) { return 11; }
            catch(NullReferenceException) { return 13; }
        }
    }
    internal class ProtectedVirtualConcrete : ProtectedVirtualRead
    {
        protected override int Read(int value) => unchecked(value+17);
        protected override long Wide(long value) => unchecked(value*4294967297L);
        protected override int Value => State;
        protected override void Set(int value) { State=value; }
        protected override ProbeNode Make(ProbeNode previous)
        { var later=new ProbeNode(unchecked(previous.Initial+7));return new ProbeNode(unchecked(previous.Initial+later.Initial)); }
    }
    internal sealed class ProtectedVirtualInherited : ProtectedVirtualConcrete { }
    internal abstract class ProtectedVirtualAgain : ProtectedVirtualConcrete { protected abstract override int Read(int value); }
    internal sealed class ProtectedVirtualFinal : ProtectedVirtualAgain { protected sealed override int Read(int value) => unchecked(value-23); }
    internal class ProtectedVirtualHidden : ProtectedVirtualConcrete
    { protected new virtual int Read(int value) => unchecked(value+51);public int HiddenThrough(int value) => Read(value); }
    internal sealed class ProtectedVirtualHiddenLeaf : ProtectedVirtualHidden { protected override int Read(int value) => unchecked(value-61); }
    internal sealed class ProtectedVirtualThrow : ProtectedVirtualConcrete { protected override int Read(int value) => 20/value; }
    internal abstract class ProtectedVirtualGeneric<T> { protected abstract T Echo(T value);public T Through(T value)=>Echo(value); }
    internal sealed class ProtectedVirtualGenericLeaf<T> : ProtectedVirtualGeneric<T> { protected override T Echo(T value)=>value; }
    internal abstract class ProtectedVirtualInline { protected abstract ValueVectorCell Echo(ValueVectorCell value);public ValueVectorCell Through(ValueVectorCell value)=>Echo(value); }
    public static class ProtectedVirtualProbe
    {
        private static ProtectedVirtualRead Create(int kind)
        { if(kind==0)return new ProtectedVirtualConcrete();if(kind==1)return new ProtectedVirtualInherited();if(kind==2)return new ProtectedVirtualFinal();return new ProtectedVirtualHiddenLeaf(); }
        public static int Scalars(int a,int b) => Create(a).Run(a,b);
        public static int References(int a,int b)
        { var receiver=Create(a);var first=new ProbeNode(b);var second=receiver.MakeThrough(first);var third=receiver.MakeThrough(second);return unchecked(first.Initial+second.Initial+third.Initial); }
        public static int Hidden(int a,int b)
        { var receiver=new ProtectedVirtualHiddenLeaf();return unchecked(receiver.HiddenThrough(a)+receiver.ReadThrough(b)); }
        public static int Reabstract(int a,int b)
        { ProtectedVirtualAgain receiver=new ProtectedVirtualFinal();return unchecked(receiver.ReadThrough(a)+receiver.ReadThrough(b)); }
        public static int Generic(int a,int b)
        { ProtectedVirtualGeneric<int> scalar=new ProtectedVirtualGenericLeaf<int>();ProtectedVirtualGeneric<ProbeNode> reference=new ProtectedVirtualGenericLeaf<ProbeNode>();var first=new ProbeNode(a);var second=reference.Through(first);var pressure=new ProbeNode(b);return unchecked(scalar.Through(b)+second.Initial+pressure.Initial+(first==second?1:0)); }
        public static int NullOnly(int a,int b)=>ProtectedVirtualRead.NullCall(a,1);
        public static int ArgumentFault(int a,int b)=>ProtectedVirtualRead.NullCall(a,b);
        public static int CapturedFault(int a,int b)
        { ProtectedVirtualRead receiver=null!;if(a>=0)receiver=new ProtectedVirtualThrow();try{return receiver.ReadThrough(10/b);}catch(DivideByZeroException){return 11;}catch(NullReferenceException){return 13;} }
        public static int UnsupportedInline(int a,int b) { ProtectedVirtualInline receiver=null!;return receiver.Through(default).Number; }
    }
}
