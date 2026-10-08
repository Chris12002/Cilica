using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class SetUnionSource
    {
        public HashSet<int> Target;
        public int Trace, Mode;
        public IEnumerable<int> Values(int value)
        {
            try
            {
                Trace++; yield return value;
                Trace++; yield return unchecked(value+1);
                if ((Mode&1)!=0) throw new InvalidOperationException("body");
                Trace++; yield return unchecked(value+2);
            }
            finally
            {
                Trace=unchecked(Trace*31+7);
                if (Mode==2) Target.Add(1234);
                if (Mode>=3) throw new InvalidOperationException("dispose");
            }
        }
    }
    public static class HashSetUnionProbe
    {
        private static int Hash(string text) { int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        private static int Snapshot(HashSet<int> set,int seed) { foreach(int value in set)seed=unchecked(seed*31+value);return unchecked(seed+set.Count*71); }
        private static void WarmEmpty() { var e=((IEnumerable<int>)new HashSet<int>()).GetEnumerator();e.Dispose(); }
        public static int Arrays(int a,int b)
        {
            var set=new HashSet<int>();for(int i=0;i<20;i++)set.Add(unchecked(a+i));
            for(int i=0;i<20;i+=2)set.Remove(unchecked(a+i));
            var source=new int[48];for(int i=0;i<source.Length;i++)source[i]=unchecked(a+i%29);
            set.UnionWith(source);return Snapshot(set,b);
        }
        public static int Wide(int a,int b)
        {
            long value=unchecked(((long)a<<32)|(uint)b);
            var set=new HashSet<long>();set.Add(value);set.UnionWith(new[]{value,~value,0L,long.MinValue,long.MaxValue});
            int result=set.Count;foreach(long item in set)result=unchecked(result*31+(int)item+(int)(item>>32));return result;
        }
        public static int Values(int a,int b)
        {
            var value=new ValueTuple<long,int>(unchecked(((long)a<<32)|(uint)b),a);
            var set=new HashSet<ValueTuple<long,int>>();set.Add(value);
            set.UnionWith(new[]{value,new ValueTuple<long,int>(b,a),value});
            return set.Count+(set.Contains(value)?7:11);
        }
        public static int Strings(int a,int b)
        {
            string first=string.Concat("ab",a.ToString()),equal=string.Concat("a","b",a.ToString());
            var set=new HashSet<string>(StringComparer.Ordinal);set.Add(first);
            set.UnionWith(new[]{equal,null,"ab",null,equal});set.TryGetValue(equal,out string actual);
            int result=set.Count+((object)actual==first?7:11);foreach(string value in set)result=unchecked(result*31+(value==null?13:Hash(value)));return result;
        }
        public static int Self(int a,int b)
        {
            WarmEmpty();var set=new HashSet<int>();for(int i=0;i<(a&7);i++)set.Add(i);
            var before=set.GetEnumerator();set.UnionWith(set);int result=0;
            while(before.MoveNext())result=unchecked(result*7+before.Current);before.Dispose();return Snapshot(set,result);
        }
        public static int Interface(int a,int b)
        {
            var set=new HashSet<int>();set.Add(a);ISet<int> view=set;
            view.UnionWith(new[]{a,b,a});return Snapshot(set,17);
        }
        public static int SourceFaults(int a,int b)
        {
            var source=new FirstSequence(b&7,a&7,a);var set=new HashSet<int>();set.Add(b);int result=0;
            try { set.UnionWith(source); } catch(InvalidOperationException e) { result=Hash(e.Message); }
            return Snapshot(set,unchecked(result+source.Trace*37));
        }
        public static int CollectionIgnoreCount(int a,int b)
        {
            var sequence=new FirstSequence(b&7,a&7,a);
            var source=new SetCollectionSource { Sequence=sequence,Mode=1,ReportedCount=-1 };
            var set=new HashSet<int>();set.Add(b);int result=0;
            try { set.UnionWith(source); } catch(InvalidOperationException e) { result=Hash(e.Message); }
            return Snapshot(set,unchecked(result+source.Trace*37+sequence.Trace*53));
        }
        public static int Partial(int a,int b)
        {
            var set=new HashSet<int>();set.Add(b);var source=new SetUnionSource {Target=set,Mode=b&7};int result=0;
            try { set.UnionWith(source.Values(a)); } catch(InvalidOperationException e) { result=Hash(e.Message); }
            return Snapshot(set,unchecked(result+source.Trace*37));
        }
        public static int NullOrder(int a,int b)
        {
            WarmEmpty();HashSet<int> set=a<0?null:new HashSet<int>();IEnumerable<int> source=b<0?null:new int[0];
            try { set.UnionWith(source);return 0; }
            catch(ArgumentNullException e) { return unchecked(Hash(e.Message)*31+Hash(e.ParamName)); }
            catch(NullReferenceException) { return 47; }
        }
        public static int SelfCallbacks(int a,int b)
        {
            var trace=new KeyTrace();var set=new HashSet<CollisionKey>();set.Add(new CollisionKey(a,trace));
            trace.HashFailure=b==1?1:0;trace.EqualityFailure=b==2?1:0;int result=0;
            try { set.UnionWith(set); } catch(InvalidOperationException e) { result=Hash(e.Message); }
            foreach(var item in set)result=unchecked(result*31+item.Number);
            return unchecked(result+trace.Hashes*37+trace.Equalities*53+set.Count*19);
        }
        public static int ValueCallbacks(int a,int b)
        {
            var trace=new KeyTrace();var set=new HashSet<CollisionKey>();set.Add(new CollisionKey(a,trace));
            var source=new SetYieldSource();trace.HashFailure=b==1?1:0;trace.EqualityFailure=b==2?1:0;int result=0;
            try { set.UnionWith(source.Values(a,trace)); } catch(InvalidOperationException e) { result=Hash(e.Message); }
            foreach(var item in set)result=unchecked(result*31+item.Number);
            return unchecked(result+trace.Hashes*37+trace.Equalities*53+source.Trace*71+set.Count*19);
        }
    }
}
