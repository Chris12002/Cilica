using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class DispatchIdentityKey { public int Number; }
    public class DispatchHashBase { public int Number; }
    public sealed class DispatchHashLeaf : DispatchHashBase
    {
        public override int GetHashCode() => Number;
        public override bool Equals(object other) => other is DispatchHashLeaf leaf && Number == leaf.Number;
    }
    public class DispatchVoidBase
    {
        public int Number;
        public virtual void Write() { Number=unchecked(Number+3); }
    }
    public sealed class DispatchVoidLeaf : DispatchVoidBase
    {
        public override void Write() { var pressure=new int[1];pressure[0]=Number;Number=unchecked(pressure[0]+7); }
    }
    public static class DispatchReceiverProbe
    {
        public static int HashKey(int a,int b)
        {
            var key = new DispatchIdentityKey { Number=a }; var other = new DispatchIdentityKey { Number=b };
            var table = new Dictionary<DispatchIdentityKey,int>(); table.TryAdd(key,a); table.TryAdd(other,b);
            table.TryGetValue(key,out int first); table.TryGetValue(other,out int second);
            return unchecked(first*3+second*7+table.Count+StringComparer.Ordinal.Compare("a","b"));
        }
        public static int EqualityKey(int a,int b)
        {
            var key = new DispatchIdentityKey { Number=a }; var other = new DispatchIdentityKey { Number=a };
            var list = new List<DispatchIdentityKey>(); list.Add(key); list.Add(null);
            int result = (list.Contains(key)?3:5)+(list.Contains(other)?7:11)+(list.Contains(null)?13:17);
            return result+StringComparer.Ordinal.Compare("a","b");
        }
        public static int Polymorphic(int a,int b)
        {
            DispatchHashBase key = new DispatchHashLeaf { Number=a }; DispatchHashBase copy = new DispatchHashLeaf { Number=a };
            var identity = new DispatchHashBase { Number=b };
            var table = new Dictionary<DispatchHashBase,int>(); table.TryAdd(key,a); table.TryAdd(identity,b);
            bool added = table.TryAdd(copy,17); table.TryGetValue(copy,out int first); table.TryGetValue(identity,out int second);
            return unchecked(first*3+second*7+table.Count+(added?11:13)+StringComparer.Ordinal.Compare("a","b"));
        }
        private static int Hash(DispatchHashBase value) => value.GetHashCode();
        public static int Direct(int a,int b)
        {
            DispatchHashBase value = b < 0 ? null : new DispatchHashLeaf { Number=a };
            int other = StringComparer.Ordinal.Compare("a","b");
            try { return unchecked(Hash(value)+other); } catch (NullReferenceException) { return 31; }
        }
        private static void Write(DispatchVoidBase value) => value.Write();
        public static int Void(int a,int b)
        {
            DispatchVoidBase value=b<0 ? new DispatchVoidBase() : new DispatchVoidLeaf();
            value.Number=a;Write(value);return unchecked(value.Number+StringComparer.Ordinal.Compare("a","b"));
        }
        private static int UnknownHash(object value) => value.GetHashCode();
        public static int Unbounded(int a,int b)
        {
            var key=new DispatchIdentityKey();var table=new Dictionary<DispatchIdentityKey,int>();table.TryAdd(key,a);
            return UnknownHash(key)+StringComparer.Ordinal.Compare("a","b");
        }
        private static int GenericHash<T>(ref T value) where T:class => value.GetHashCode();
        public static int SeparateBounds(int a,int b)
        {
            var first = new DispatchIdentityKey { Number=a }; var second = new PlainReferenceKey();
            int x = GenericHash(ref first), y = GenericHash(ref second);
            return (x==GenericHash(ref first)?3:5)+(y==GenericHash(ref second)?7:11)+StringComparer.Ordinal.Compare("a","b");
        }
    }
}
