using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class OrdinalSetSourceProbe
    {
        private static string Word(int index)
        {
            switch (index & 7) { case 0: return null; case 1: return ""; case 2: return "a"; case 3: return "A";
                case 4: return "a\0z"; case 5: return "a\u0301"; case 6: return "\ud83d\ude00"; default: return "\ud800"; }
        }
        private static int Trace(HashSet<string> set)
        {
            int result = set.Count;
            foreach (var word in set) { result = unchecked(result*31 + (word == null ? -1 : word.Length));
                if (word != null) for (int i=0;i<word.Length;i++) result=unchecked(result*31+word[i]); }
            return result;
        }
        public static int Cases(int a,int b)
        {
            string[] source = { Word(a), Word(b), Word(a), null, "", "A", "a" };
            var set = new HashSet<string>(source, StringComparer.Ordinal);
            set.Remove(Word(b)); set.Add(Word(b+1)); return Trace(set);
        }
        public static int Identity(int a,int b)
        {
            var first=new string(new[]{'a',(char)a}); var equal=new string(new[]{'a',(char)a});
            var set=new HashSet<string>(new[]{first,equal},StringComparer.Ordinal);
            set.TryGetValue(equal,out var stored); return unchecked((ReferenceEquals(first,stored)?3:7)+set.Count+b);
        }
        public static int SameComparer(int a,int b)
        {
            var source=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<8;i++) source.Add(Word(i)); source.Remove(Word(a)); source.Remove(Word(b));
            var copy=new HashSet<string>(source,StringComparer.Ordinal); source.Clear(); copy.Add(Word(a)); return Trace(copy);
        }
        public static int DifferentComparer(int a,int b)
        {
            var source=new HashSet<string>();
            for(int i=0;i<8;i++) source.Add(Word(i)); source.Remove(Word(a)); source.Remove(Word(b));
            var copy=new HashSet<string>(source,StringComparer.Ordinal); source.Clear(); copy.Add(Word(a)); return Trace(copy);
        }
        public static int Null(int a,int b)
        {
            try { return new HashSet<string>((IEnumerable<string>)null,StringComparer.Ordinal).Count; }
            catch(ArgumentNullException error) { return unchecked(error.ParamName.Length+error.Message.Length+a+b); }
        }
        private static IEnumerable<string> Source(int a,int b)
        { if(a<0) throw new InvalidOperationException("source"); return new[]{Word(a),Word(b)}; }
        public static int Order(int a,int b)
        {
            try { return Trace(new HashSet<string>(Source(a,b),StringComparer.Ordinal)); }
            catch(InvalidOperationException error) { return error.Message.Length; }
        }
        public static int DeferredFault(int a,int b)
        {
            int calls=0;
            var source=new[]{Word(a),Word(b),"last"}.Select(value=> { if(++calls==2) throw new InvalidOperationException("iteration"); return value; });
            try { return new HashSet<string>(source,StringComparer.Ordinal).Count; }
            catch(InvalidOperationException error) { return unchecked(calls+error.Message.Length+a+b); }
        }
        public static int UnsupportedNull(int a,int b)=>new HashSet<string>(new[]{"a"},(IEqualityComparer<string>)null).Count;
        public static int UnsupportedAlias(int a,int b) { var comparer=StringComparer.Ordinal; int result=comparer.Compare("a","b"); return result+new HashSet<string>(new[]{"a"},comparer).Count; }
        public static int UnsupportedCustom(int a,int b)=>new HashSet<string>(new[]{"a"},new OrdinalSetCustom()).Count;
        public static int UnsupportedGetter(int a,int b)=>new HashSet<string>(new[]{"a"},ForgedOrdinalComparer.Ordinal).Count;
        public static int UnsupportedJoin(int a,int b)=>new HashSet<string>(new[]{"a"},a<0?StringComparer.Ordinal:null).Count;
        public static int UnsupportedInteger(int a,int b)=>new HashSet<int>(new[]{a},(IEqualityComparer<int>)null).Count;
    }
}
