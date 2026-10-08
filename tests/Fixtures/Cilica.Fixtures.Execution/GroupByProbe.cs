#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class GroupTrace
    {
        public int Trace,Mod=5,Fail=int.MinValue;
        public KeyTrace Keys=new KeyTrace();
        public int Key(int value) { Trace=unchecked(Trace*31+value);if(value==Fail)throw new InvalidOperationException("selector");return value%Mod; }
        public int Identity(int value) { Trace=unchecked(Trace*31+value);return value; }
        public long WideKey(int value) { Trace=unchecked(Trace*31+value);return unchecked(((long)(value%Mod)<<32)|(uint)value); }
        public CollisionKey MutableKey(int value) { Trace=unchecked(Trace*31+value);return new CollisionKey(value%Mod,Keys); }
        public ValueTuple<int,int> ValueKey(int value)=>new ValueTuple<int,int>(value%3,value%5);
        public string StringKey(string value) { Trace=unchecked(Trace*31+1);return value; }
        public int All(int value) { Trace=unchecked(Trace*31+value);return 0; }
        public int NodeKey(ProbeNode value)=>value.Initial%3;
        public int TupleKey(ValueTuple<ProbeNode,int> value) { Trace=unchecked(Trace*31+value.Item1.Initial);return value.Item2%3; }
    }
    public static class GroupByProbe
    {
        private static int[] Values(int a,int b) { var values=new int[48];for(int i=0;i<values.Length;i++)values[i]=unchecked(a+i+b%3);return values; }
        private static int Hash(string text) { if(text==null)return 13;int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        private static int Fold(IEnumerable<IGrouping<int,int>> groups,int seed)
        { foreach(var group in groups) { seed=unchecked(seed*31+group.Key);foreach(int value in group)seed=unchecked(seed*17+value); }return seed; }
        private static void WarmEmpty()
        { var first=((IEnumerable<int>)Array.Empty<int>()).GetEnumerator();first.Dispose();var second=((IEnumerable<IGrouping<int,int>>)Array.Empty<IGrouping<int,int>>()).GetEnumerator();second.Dispose(); }
        public static int Stable(int a,int b) { var trace=new GroupTrace();var groups=Values(a,b).GroupBy(trace.Key);return unchecked(Fold(groups,17)+trace.Trace); }
        public static int Resize(int a,int b) { var trace=new GroupTrace();var groups=Values(a,b).GroupBy(trace.Identity);return unchecked(Fold(groups.ToArray(),17)+trace.Trace); }
        public static int Wide(int a,int b) { var trace=new GroupTrace();int h=17;foreach(var group in Values(a,b).GroupBy(trace.WideKey)) { h=unchecked(h*31+(int)group.Key+(int)(group.Key>>32));foreach(int value in group)h=unchecked(h*17+value); }return unchecked(h+trace.Trace); }
        public static int ValuesAsKeys(int a,int b) { var trace=new GroupTrace();int h=17;foreach(var group in Values(a,b).GroupBy(trace.ValueKey))h=unchecked(h*31+group.Key.Item1+group.Key.Item2+group.Count());return h; }
        public static int Strings(int a,int b)
        {
            string first=string.Concat("v",a.ToString()),equal=string.Concat("","v",a.ToString());var source=new[]{null,first,equal,"x",null,"x",""};var trace=new GroupTrace();int h=17;
            foreach(var group in source.GroupBy(trace.StringKey).ToList())
            { h=unchecked(h*31+Hash(group.Key)+((object)group.Key==first?7:0));foreach(string value in group.ToArray())h=unchecked(h*17+Hash(value)); }return unchecked(h+trace.Trace);
        }
        public static int Repeated(int a,int b) { var trace=new GroupTrace();var values=Values(a,b);var query=values.GroupBy(trace.Key);int h=Fold(query,17);values[0]=b;h=Fold(query.ToList(),h);return unchecked(h+trace.Trace); }
        public static int Terminals(int a,int b) { var trace=new GroupTrace();var query=Values(a,b).GroupBy(trace.Key);int h=query.Count();h=unchecked(h*31+(query.Any()?1:0));h=unchecked(h*31+query.First().Key+query.FirstOrDefault().Key);h=Fold(query.ToArray(),h);return unchecked(h+trace.Trace); }
        public static int Cursor(int a,int b)
        {
            var trace=new GroupTrace();var query=Values(a,b).GroupBy(trace.Key);var first=query.GetEnumerator();var second=query.GetEnumerator();int h=first.Current==null?7:11;
            while(first.MoveNext()){h=unchecked(h*31+first.Current.Key);if(second.MoveNext())h=unchecked(h*17+second.Current.Key);}h=unchecked(h*31+(first.Current==null?7:11));first.Dispose();second.Dispose();return unchecked(h+trace.Trace);
        }
        public static int GroupCursor(int a,int b)
        {
            var trace=new GroupTrace();var group=Values(a,b).GroupBy(trace.All).First();var e=group.GetEnumerator();int h=0;
            try { h=e.Current; }catch(IndexOutOfRangeException ex){h=Hash(ex.Message);}
            while(e.MoveNext())h=unchecked(h*31+e.Current);h=unchecked(h*17+e.Current);e.Dispose();h=unchecked(h*17+e.Current);
            e.Reset();try{h=unchecked(h+e.Current);}catch(IndexOutOfRangeException){h=unchecked(h+73);}e.MoveNext();h=unchecked(h*31+e.Current);return h;
        }
        public static int GroupList(int a,int b)
        {
            var trace=new GroupTrace();var group=(IList<int>)Values(a,b).GroupBy(trace.All).First();int h=group.Count+(group.IsReadOnly?7:11);h=unchecked(h*31+group.IndexOf(a)+(group.Contains(b)?1:0));var array=new int[52];group.CopyTo(array,2);for(int i=0;i<array.Length;i++)h=unchecked(h*17+array[i]);
            try{group.Add(b);}catch(NotSupportedException e){h=unchecked(h+Hash(e.Message));}try{group.RemoveAt(-1);}catch(NotSupportedException e){h=unchecked(h+Hash(e.Message));}try{group[-1]=b;}catch(NotSupportedException e){h=unchecked(h+Hash(e.Message));}
            try{h=unchecked(h+group[b&63]);}catch(ArgumentOutOfRangeException e){h=unchecked(h+Hash(e.ParamName)+Hash(e.Message));}return h;
        }
        public static int SourceFault(int a,int b) { var trace=new GroupTrace();var source=new FirstSequence(b&7,a&7,a);int h=0;try{var e=source.GroupBy(trace.Key).GetEnumerator();try{while(e.MoveNext())h=unchecked(h*31+e.Current.Key);}finally{e.Dispose();}}catch(InvalidOperationException e){h=Hash(e.Message);}return unchecked(h+trace.Trace+source.Trace*37); }
        public static int SelectorRetry(int a,int b)
        {
            var trace=new GroupTrace{Fail=unchecked(a+2+b%3)};var e=Values(a,b).GroupBy(trace.Key).GetEnumerator();int h=0;
            try{e.MoveNext();}catch(InvalidOperationException ex){h=Hash(ex.Message);}trace.Fail=int.MinValue;
            while(e.MoveNext())h=unchecked(h*31+e.Current.Key);e.Dispose();return unchecked(h+trace.Trace);
        }
        public static int MutableKeys(int a,int b)
        {
            var trace=new GroupTrace();trace.Keys.HashFailure=b==1?1:0;trace.Keys.EqualityFailure=b==2?1:0;int h=17;
            try{foreach(var group in Values(a,b).GroupBy(trace.MutableKey)){h=unchecked(h*31+group.Key.Number);foreach(int item in group)h=unchecked(h*17+item);}}
            catch(InvalidOperationException e){h=Hash(e.Message);}return unchecked(h+trace.Trace+trace.Keys.Hashes*37+trace.Keys.Equalities*53);
        }
        public static int Empty(int a,int b)
        {
            WarmEmpty();var trace=new GroupTrace();IEnumerable<int> source=(a&1)==0?Array.Empty<int>():new List<int>();var query=source.GroupBy(trace.Key);int h=Fold(query,17);h=Fold(query.ToArray(),h);h=Fold(query.ToList(),h);h=unchecked(h*31+query.Count()+(query.Any()?1:0)+(query.FirstOrDefault()==null?7:0));return unchecked(h+trace.Trace);
        }
        public static int InlineElements(int a,int b)
        {
            var trace=new GroupTrace();var rows=new ValueTuple<ProbeNode,int>[16];for(int i=0;i<rows.Length;i++)rows[i]=new ValueTuple<ProbeNode,int>(new ProbeNode(a+i),b+i);
            int h=17;foreach(var group in rows.GroupBy(trace.TupleKey)) { h=unchecked(h*31+group.Key);foreach(var row in group.ToArray())h=unchecked(h*17+row.Item1.Initial+row.Item2); }return unchecked(h+trace.Trace);
        }
        public static IEnumerator<IGrouping<int,ProbeNode>> MakeTracked()
        {
            var rows=new[]{new ProbeNode(0),new ProbeNode(1),new ProbeNode(2),new ProbeNode(3),new ProbeNode(4),new ProbeNode(5)};
            var trace=new GroupTrace();var iterator=rows.GroupBy(trace.NodeKey).GetEnumerator();iterator.MoveNext();return iterator;
        }
        public static IGrouping<int,ProbeNode> ReadTracked(IEnumerator<IGrouping<int,ProbeNode>> iterator)=>iterator.Current;
        public static void StopTracked(IEnumerator<IGrouping<int,ProbeNode>> iterator)=>iterator.Dispose();
        public static int Lifetime(int a,int b) { var iterator=MakeTracked();var group=ReadTracked(iterator);StopTracked(iterator);return group.Key; }
        public static int Validation(int a,int b)
        {
            WarmEmpty();var trace=new GroupTrace();IEnumerable<int> source=a<0?null:Array.Empty<int>();Func<int,int> selector=b<0?null:new Func<int,int>(trace.Key);try{return Fold(source.GroupBy(selector),17);}catch(ArgumentNullException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName));}
        }
    }
}
