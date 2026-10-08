using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class CandidateTraceComparer : IEqualityComparer<string>
    {
        public int Mode, HashCalls, EqualCalls, Trace, Fault;
        public CandidateTraceComparer(int mode) { Mode=mode; }
        public int GetHashCode(string value)
        {
            HashCalls++; Trace=unchecked(Trace*31+value.Length+11);
            var pressure=new ValueVectorBox(17); if(Fault==1)throw new InvalidOperationException("hash");
            return pressure.Number-17;
        }
        public bool Equals(string left,string right)
        {
            EqualCalls++; Trace=unchecked(Trace*31+left.Length*7+right.Length+23);
            var pressure=new ValueVectorBox(19); if(Fault==2)throw new InvalidOperationException("equal");
            return Mode==0 ? left==right : left.Length==right.Length && pressure.Number==19;
        }
    }
    public sealed class CandidateIntegerComparer : IEqualityComparer<int>
    {
        public int GetHashCode(int value) => 0;
        public bool Equals(int first,int second) => (first & 3)==(second & 3);
    }
    public sealed class CandidateValueComparer : IEqualityComparer<CollisionKey>
    {
        public int GetHashCode(CollisionKey value) => value.GetHashCode();
        public bool Equals(CollisionKey first,CollisionKey second) => first.Equals(second);
    }
    public sealed class CandidateSubclassComparer : EqualityComparer<CollisionKey>
    {
        public override int GetHashCode(CollisionKey value) => value.GetHashCode();
        public override bool Equals(CollisionKey first,CollisionKey second) => first.Equals(second);
    }
    public static class CandidateDictionaryComparerProbe
    {
        private static string Word(int i)
        { if((i&3)==0)return "a";if((i&3)==1)return "B";if((i&3)==2)return "cc";return "d\0"; }
        public static int Selection(int a,int b)
        {
            var custom=new CandidateTraceComparer(a&1);
            IEqualityComparer<string> selected=a<0 ? null : custom;
            var map=new Dictionary<string,int>(selected);
            for(int i=0;i<12;i++)map[Word(i)]=i+b;
            int result=map.Count;
            for(int i=0;i<4;i++)result=unchecked(result*31+map[Word(i)]);
            map.Remove(Word(a));map.TryAdd(Word(a),a);
            foreach(var entry in map)result=unchecked(result*31+entry.Value+entry.Key.Length);
            return unchecked(result+custom.Trace+custom.HashCalls*101+custom.EqualCalls*1009);
        }
        public static int Alias(int a,int b)
        {
            IEqualityComparer<string> selected=StringComparer.Ordinal;
            if(a>0)selected=new CandidateTraceComparer(1);
            var map=new Dictionary<string,int>(b&7,selected);map.Add("a",a);bool added=map.TryAdd("B",b);
            return map.Count*100+(added?10:0)+map["a"];
        }
        public static int Collisions(int a,int b)
        {
            var comparer=new CandidateTraceComparer(0);var map=new Dictionary<string,int>(comparer);
            for(int i=0;i<4;i++)map.Add(Word(i),i+a);
            int old;map.Remove(Word(b),out old);map.Add(Word(b),b);
            var cursor=map.GetEnumerator();cursor.MoveNext();map[Word(a)]=b;
            int result=old+map.Count;
            while(cursor.MoveNext())result=unchecked(result*31+cursor.Current.Value);
            try{map.Add(Word(b),a);}catch(ArgumentException e){if(e.ParamName!=null)return -1;result+=7;}
            return unchecked(result+comparer.Trace);
        }
        public static int Faults(int mode,int unused)
        {
            var comparer=new CandidateTraceComparer(0);var map=new Dictionary<string,int>(comparer);map.Add("a",1);
            comparer.Fault=(mode&1)+1;
            int value=31,result=0;
            try { if((mode&6)==0)map.Add("B",2);else if((mode&6)==2)map.TryGetValue("a",out value);
                else if((mode&6)==4)map.Remove("a",out value);else map["a"]=2; }
            catch(InvalidOperationException e) { result=e.Message=="hash"?3:e.Message=="equal"?5:-999; }
            comparer.Fault=0;
            return result*10000+value*100+map.Count*10+map["a"];
        }
        public static int NullKey(int a,int b)
        {
            var comparer=new CandidateTraceComparer(0);var map=new Dictionary<string,int>(comparer);int result=0;
            try{map.ContainsKey(null);}catch(ArgumentNullException e){if(e.ParamName=="key")result++;}
            try{map.Add(null,b);}catch(ArgumentNullException e){if(e.ParamName=="key")result+=2;}
            try{map.Remove(null);}catch(ArgumentNullException e){if(e.ParamName=="key")result+=4;}
            return result+comparer.HashCalls*10+comparer.EqualCalls*100;
        }
        private static int Capacity(ref int trace,int mode)
        {trace=trace*10+1;if(mode==1)throw new InvalidOperationException("capacity");return mode==2?-1:3;}
        private static IEqualityComparer<string> Comparer(ref int trace,int mode)
        {trace=trace*10+2;if(mode==3)throw new InvalidOperationException("comparer");return new CandidateTraceComparer(0);}
        public static int Construction(int mode,int b)
        {
            int trace=0;
            try{var map=new Dictionary<string,int>(Capacity(ref trace,mode),Comparer(ref trace,mode));return trace*100+map.Count;}
            catch(ArgumentOutOfRangeException e){return e.ParamName=="capacity"?trace*100+7:-1;}
            catch(InvalidOperationException e){return trace*100+(e.Message=="capacity"?3:5);}
        }
        public static int Roots(int a,int b)
        {
            var comparer=new CandidateTraceComparer(0);var map=new Dictionary<string,ValueVectorBox>(comparer);
            var original=new ValueVectorBox(a);map.Add("a",original);
            var replacement=new ValueVectorBox(b);map["a"]=replacement;
            comparer=null;var pressure=new ValueVectorBox(47);map.TryGetValue("a",out var value);map.Clear();
            return unchecked(value.Number+pressure.Number+(ReferenceEquals(value,replacement)?101:103));
        }
        public static int Integers(int a,int b)
        {
            var map=new Dictionary<int,int>(new CandidateIntegerComparer());map.Add(a,b);map[a+4]=a;
            return unchecked(map.Count*31+map[a]+(map.ContainsKey(a+8)?1:0));
        }
        public static int ValueCopies(int a,int b)
        {
            var trace=new KeyTrace();var key=new CollisionKey(a,trace);
            IEqualityComparer<CollisionKey> selected=a>0 ? (IEqualityComparer<CollisionKey>)new CandidateValueComparer() : new CandidateSubclassComparer();
            var map=new Dictionary<CollisionKey,int>(selected);
            map.TryAdd(key,b);map.TryAdd(key,a);map[key]=a;
            var cursor=map.GetEnumerator();cursor.MoveNext();int stored=cursor.Current.Key.Number;
            bool found=map.TryGetValue(key,out var value);bool removed=map.Remove(key,out var old);
            return unchecked(key.Number+stored*31+value*101+old*1009+trace.Hashes*7+trace.Equalities*11+
                (found?13:17)+(removed?19:23)+map.Count);
        }
        public static int DefaultValueKey(int a,int b)
        {
            var trace=new KeyTrace();var key=new CollisionKey(a,trace);
            var selected=EqualityComparer<CollisionKey>.Default;
            var map=(b&1)==0 ? new Dictionary<CollisionKey,int>(selected) : new Dictionary<CollisionKey,int>(b&7,selected);
            map.TryAdd(key,b);map.TryGetValue(key,out var value);map.Remove(key,out var old);
            var cursor=map.GetEnumerator();int stored=cursor.MoveNext()?cursor.Current.Key.Number:0;
            return unchecked(key.Number+stored*31+value*101+old*1009+trace.Hashes*7+trace.Equalities*11+map.Count);
        }
        public static int UnsupportedIgnoreCase(int a,int b)
        {var map=new Dictionary<string,int>(a>0?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);map.Add("a",a);return map["A"];}
        public static int UnsupportedCulture(int a,int b)
        {var map=new Dictionary<string,int>(StringComparer.CurrentCulture);return map.Count;}
    }
}
