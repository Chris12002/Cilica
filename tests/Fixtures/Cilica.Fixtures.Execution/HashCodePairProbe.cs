using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class HashPairTrace { public int Value; }
    public struct HashPairPart
    {
        public int Value; public int Tag; public HashPairTrace Trace; public bool Fail;
        public override int GetHashCode()
        { Trace.Value = unchecked(Trace.Value * 7 + Tag); Value = unchecked(Value + 1); if(Fail) throw new InvalidOperationException("hash"); return Value; }
    }
    public sealed class HashPairKey : IEquatable<HashPairKey>
    {
        public string First; public string Second;
        public HashPairKey(string first,string second) { First=first; Second=second; }
        public bool Equals(HashPairKey other) => other != null && First == other.First && Second == other.Second;
        public override bool Equals(object other) => other is HashPairKey key && Equals(key);
        public override int GetHashCode() => HashCode.Combine(First,Second);
    }
    public static class HashCodePairProbe
    {
        public static int Raw(int a,int b) => HashCode.Combine(a,b);
        public static int Stable(int a,int b)
        { int first=HashCode.Combine(a,b); int second=HashCode.Combine(a,b); return first==second ? 1 : 0; }
        public static int NullAndText(int a,int b)
        {
            string first=a<0 ? null : a==0 ? "" : "a\0\ud800";
            string same=a<0 ? null : a==0 ? "" : string.Concat("a", "\0\ud800");
            string second=b<0 ? null : "b";
            return HashCode.Combine(first,second)==HashCode.Combine(same,second) ? 11 : 13;
        }
        public static int Wide(int a,int b)
        {
            long value=((long)a<<32)|(uint)b;
            return HashCode.Combine(value,a)==HashCode.Combine(value,a) ? 17 : 19;
        }
        public static int CallbackCopies(int a,int b)
        {
            var trace=new HashPairTrace();
            var first=new HashPairPart { Value=a,Tag=1,Trace=trace,Fail=a<0 };
            var second=new HashPairPart { Value=b,Tag=2,Trace=trace,Fail=b<0 };
            int outcome=0;
            try { HashCode.Combine(first,second); outcome=3; } catch(InvalidOperationException) { outcome=5; }
            return unchecked(trace.Value*11+first.Value*13+second.Value*17+outcome);
        }
        public static int ReferenceCallbacks(int a,int b)
        {
            var first=new HashPairKey("a", "b"); var second=new HashPairKey("a", string.Concat("", "b"));
            return HashCode.Combine(first,a)==HashCode.Combine(second,a) ? b : a;
        }
        public static int Dictionary(int a,int b)
        {
            var values=new Dictionary<HashPairKey,int>();
            var key=new HashPairKey("a\0\ud800", "b"); values.Add(key,a);
            for(int i=0;i<20;i++) values[new HashPairKey(i.ToString(),"other")]=i;
            var equal=new HashPairKey(string.Concat("a", "\0\ud800"),"b");
            bool added=values.TryAdd(equal,b); int read=values[equal]; bool removed=values.Remove(equal);
            return unchecked(read+values.Count*31+(added?37:41)+(removed?43:47));
        }
        public static int UnsupportedVector(int a,int b) => HashCode.Combine(new int[]{a},b);
        public static int UnsupportedArity(int a,int b) => HashCode.Combine(a,b,a);
        public static int UnsupportedNullable(int a,int b) => HashCode.Combine((int?)a,b);
        public static int UnsupportedBuilder(int a,int b) { var value=new HashCode(); value.Add(a); return value.ToHashCode(); }
    }
}
