using System;
namespace Cilica.Fixtures.Execution
{
    public interface IReferenceCall { int Call(int next); }
    public class ReferenceCallBase : IReferenceCall
    {
        public int Number;
        public ReferenceCallBase(int number) { Number=number; }
        public virtual int Call(int next) { var pressure=new ValueVectorBox(next);Number=unchecked(Number+pressure.Number);return Number; }
        public override int GetHashCode() { var pressure=new ValueVectorBox(Number);return pressure.Number; }
    }
    public sealed class ReferenceCallDerived : ReferenceCallBase
    {
        public ReferenceCallDerived(int number):base(number) { }
        public override int Call(int next)=>unchecked(base.Call(next)+31);
    }
    public sealed class ExplicitReferenceCall : IReferenceCall
    {
        public int Number;
        public ExplicitReferenceCall(int number) { Number=number; }
        int IReferenceCall.Call(int next)=>unchecked(Number+next+47);
    }
    public sealed class ReferenceCallHolder { public ReferenceCallBase Value; }
    public struct ReferenceCallValue : IReferenceCall
    { public int Number;public int Call(int next) { Number=unchecked(Number+next);return Number; } }
    public static class ConstrainedReferenceProbe
    {
        private static int Invoke<T>(ref T value,int next) where T:IReferenceCall=>value.Call(next);
        private static int Hash<T>(ref T value)=>value.GetHashCode();
        private static int Change<T>(ref T value,T replacement,int next) { value=replacement;return next; }
        private static int Replace<T>(ref T value,T replacement,int next) where T:IReferenceCall=>value.Call(Change(ref value,replacement,next));
        public static int Class(int a,int b) { var value=new ReferenceCallBase(a);return unchecked(Invoke(ref value,b)+value.Number); }
        public static int Virtual(int a,int b) { ReferenceCallBase value=new ReferenceCallDerived(a);return unchecked(Invoke(ref value,b)+Hash(ref value)); }
        public static int Interface(int a,int b) { IReferenceCall value=a<0?(IReferenceCall)new ExplicitReferenceCall(a):new ReferenceCallDerived(a);return Invoke(ref value,b); }
        public static int Locations(int a,int b)
        {
            var holder=new ReferenceCallHolder();holder.Value=new ReferenceCallBase(a);
            var array=new[]{new ReferenceCallDerived(b)};
            return unchecked(Invoke(ref holder.Value,b)+Invoke(ref array[0],a)+holder.Value.Number+array[0].Number);
        }
        private static int ReplaceHolder(ReferenceCallHolder holder,ReferenceCallBase next,int value)
        { holder.Value=next;var pressure=new ValueVectorBox(value);return pressure.Number; }
        public static int HeapReplacement(int a,int b)
        {
            var holder=new ReferenceCallHolder();holder.Value=new ReferenceCallBase(a);
            ReferenceCallBase next=b<0?null:new ReferenceCallDerived(b);
            try{return unchecked(Invoke(ref holder.Value,ReplaceHolder(holder,next,a))+holder.Value.Number);}
            catch(NullReferenceException){return holder.Value==null?41:-100;}
        }
        public static int Replacement(int a,int b)
        {
            ReferenceCallBase value=new ReferenceCallBase(a);ReferenceCallBase replacement=new ReferenceCallDerived(b);
            return unchecked(Replace(ref value,replacement,a)+value.Number+Hash(ref value));
        }
        public static int NullToObject(int a,int b)
        { ReferenceCallBase value=null;try{return Replace(ref value,new ReferenceCallBase(a),b);}catch(NullReferenceException){return unchecked(value.Number+17);} }
        public static int ObjectToNull(int a,int b)
        {
            ReferenceCallBase value=new ReferenceCallBase(a);
            try{return Replace(ref value,(ReferenceCallBase)null,b);}catch(NullReferenceException){return value==null?17:-100;}
        }
        public static int NullOrder(int a,int b)
        {
            ReferenceCallBase value=null;
            try{return Invoke(ref value,a/b);}catch(DivideByZeroException){return 19;}catch(OverflowException){return 23;}catch(NullReferenceException){return 29;}
        }
        public static int ValueAndReference(int a,int b)
        {
            var value=new ReferenceCallValue();value.Number=a;IReferenceCall reference=new ExplicitReferenceCall(b);
            return unchecked(Invoke(ref value,b)+Invoke(ref reference,a)+value.Number);
        }
    }
}
