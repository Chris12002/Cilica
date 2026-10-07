using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public class KnownReceiverBase
    {
        public int Number;
        public override string ToString() => "base";
    }
    public sealed class KnownReceiverLeaf : KnownReceiverBase
    {
        public override string ToString() => "derived-value";
    }
    public class KnownReceiverNewSlot : KnownReceiverBase
    {
        public new virtual string ToString() => "hidden-slot";
    }
    public sealed class KnownReceiverNewSlotLeaf : KnownReceiverNewSlot
    {
        public override string ToString() => "hidden-override";
    }
    public abstract class KnownReceiverAbstract
    {
        public abstract override string ToString();
    }
    public sealed class KnownReceiverConcrete : KnownReceiverAbstract
    {
        public override string ToString() => "concrete";
    }
    public class KnownReceiverGeneric<T> { public override string ToString() => "generic"; }
    public sealed class KnownReceiverGenericLeaf<T> : KnownReceiverGeneric<T> { public override string ToString() => "generic-leaf"; }
    public sealed class UnrelatedReceiver
    {
        public int Number;
        public override string ToString() => DateTime.Now.ToString();
    }
    public sealed class KnownReceiverHolder { public KnownReceiverBase Item; }
    public static class KnownReceiverProbe
    {
        private static int Read(KnownReceiverBase value) => value.ToString().Length;
        private static int ReadRef(ref KnownReceiverBase value) => value.ToString().Length;
        private static KnownReceiverBase Create(int mode) => mode < 0 ? new KnownReceiverBase() : new KnownReceiverLeaf();
        public static int Parameters(int a,int b)
        {
            var unrelated = new UnrelatedReceiver(); unrelated.Number = a;
            return unchecked(Read(Create(b)) + Read(new KnownReceiverNewSlotLeaf()) + unrelated.Number);
        }
        public static int References(int a,int b)
        {
            var unrelated = new UnrelatedReceiver(); unrelated.Number = a;
            KnownReceiverBase value = b < 0 ? null : Create(a);
            try { return unchecked(ReadRef(ref value) + unrelated.Number); }
            catch (NullReferenceException) { return 17; }
        }
        public static int LocalsAndFields(int a,int b)
        {
            var unrelated = new UnrelatedReceiver(); unrelated.Number = a;
            KnownReceiverBase local = Create(b); var holder = new KnownReceiverHolder(); holder.Item = local;
            int result = local.ToString().Length + holder.Item.ToString().Length;
            local = new KnownReceiverLeaf(); return unchecked(result + local.ToString().Length + unrelated.Number);
        }
        private static int AbstractRead(KnownReceiverAbstract value) => value.ToString().Length;
        public static int Abstract(int a,int b)
        {
            var unrelated = new UnrelatedReceiver(); unrelated.Number = a;
            KnownReceiverAbstract value = b < 0 ? null : new KnownReceiverConcrete();
            try { return unchecked(AbstractRead(value) + unrelated.Number); }
            catch (NullReferenceException) { return 23; }
        }
        private static int GenericRead<T>(KnownReceiverGeneric<T> value) => value.ToString().Length;
        public static int Generic(int a,int b)
        {
            var unrelated = new UnrelatedReceiver(); unrelated.Number = a;
            KnownReceiverGeneric<int> first = b < 0 ? new KnownReceiverGeneric<int>() : new KnownReceiverGenericLeaf<int>();
            KnownReceiverGeneric<string> second = new KnownReceiverGenericLeaf<string>();
            return unchecked(GenericRead(first) + GenericRead(second) + unrelated.Number);
        }
        private static int ObjectRead(ref object value) => value.ToString().Length;
        public static int UnprovenReference(int a, int b)
        {
            object value = new KnownReceiverLeaf();
            return ObjectRead(ref value);
        }
        public static int TypeKeys(int a,int b)
        {
            var dictionary = new Dictionary<Type,int>(); dictionary.TryAdd(typeof(KnownReceiverBase),a); dictionary.TryAdd(typeof(KnownReceiverLeaf),b);
            int result = StringComparer.Ordinal.Compare("first","second");
            dictionary.TryGetValue(typeof(KnownReceiverBase),out int first); dictionary.TryGetValue(typeof(KnownReceiverLeaf),out int second);
            return unchecked(result + first * 7 + second * 11);
        }
    }
}
