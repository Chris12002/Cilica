using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class OrdinalSetCustom : IEqualityComparer<string>
    {
        public bool Equals(string first, string second) => first == second;
        public int GetHashCode(string value) => 0;
    }
    public static class OrdinalSetProbe
    {
        private static string Word(int index)
        {
            switch(index & 7) {
                case 0: return null; case 1: return ""; case 2: return "a"; case 3: return "A";
                case 4: return "a\0z"; case 5: return "a\u0301"; case 6: return "\ud83d\ude00"; default: return "\ud800";
            }
        }
        private static int Hash(HashSet<string> set)
        {
            int hash=set.Count;
            foreach(var value in set) {
                hash=unchecked(hash*31+(value==null?-1:value.Length));
                if(value!=null)for(int j=0;j<value.Length;j++)hash=unchecked(hash*31+value[j]);
            }
            return hash;
        }
        public static int Cases(int a,int b)
        {
            var set=new HashSet<string>(StringComparer.Ordinal);int trace=0;
            for(int i=0;i<(a&31);i++)trace=unchecked(trace*7+(set.Add(Word(i+b))?1:0));
            trace=unchecked(trace*7+(set.Contains(Word(b))?1:0));
            trace=unchecked(trace*7+(set.Remove(Word(a))?1:0));set.Add(Word(b+3));
            return unchecked(trace+Hash(set));
        }
        public static int Identity(int a,int b)
        {
            var first=new string(new char[]{'a',(char)(a&127)});
            var second=new string(new char[]{'a',(char)(a&127)});
            var set=new HashSet<string>(StringComparer.Ordinal);set.Add(first);set.Add(second);
            bool found=set.TryGetValue(second,out var stored);
            return (found?7:11)+(Object.ReferenceEquals(first,stored)?13:17)+set.Count;
        }
        public static int Version(int a,int b)
        {
            var set=new HashSet<string>(StringComparer.Ordinal);set.Add(Word(a));
            var cursor=set.GetEnumerator();cursor.MoveNext();set.Add(Word(a));int result=cursor.MoveNext()?7:11;
            set.Add(Word(a+1));try{cursor.MoveNext();return -997;}catch(InvalidOperationException){return result+Hash(set);}
        }
        public static int Copy(int a,int b)
        {
            var set=new HashSet<string>(StringComparer.Ordinal);for(int i=0;i<12;i++)set.Add(Word(i+a));
            set.Remove(Word(b));var copy=new HashSet<string>(set);set.Clear();copy.Add(Word(a));return Hash(copy)+set.Count;
        }
        public static int UnsupportedNull(int a,int b) => new HashSet<string>((IEqualityComparer<string>)null).Count;
        public static int UnsupportedAlias(int a,int b)
        { var comparer=StringComparer.Ordinal;int result=comparer.Compare(Word(a),Word(b));return result+new HashSet<string>(comparer).Count; }
        public static int UnsupportedCustom(int a,int b) => new HashSet<string>(new OrdinalSetCustom()).Count;
        public static int UnsupportedGetter(int a,int b) => new HashSet<string>(ForgedOrdinalComparer.Ordinal).Count;
        public static int UnsupportedJoin(int a,int b) => new HashSet<string>(a>0?StringComparer.Ordinal:null).Count;
        public static int UnsupportedInteger(int a,int b) => new HashSet<int>((IEqualityComparer<int>)null).Count;
    }
}
