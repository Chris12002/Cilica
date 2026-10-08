#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class RangeTrace
    {
        public int Trace,Fail=int.MinValue;
        public IEnumerator<int> Cursor;
        public int Select(int value) { Trace=unchecked(Trace*31+value);if(Cursor!=null)Cursor.Dispose();if(value==Fail)throw new InvalidOperationException("range selector");return unchecked(value*17); }
    }
    public static class RangeProbe
    {
        private static int Fold(IEnumerable<int> source,int h=17) { foreach(int value in source)h=unchecked(h*31+value);return h; }
        private static int Hash(string text) { if(text==null)return 13;int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        private static void WarmEmpty() { var e=((IEnumerable<int>)Array.Empty<int>()).GetEnumerator();e.Dispose(); }
        public static int Values(int a,int b) { var range=Enumerable.Range(a%97,(b&15)+1);return Fold(range.ToArray(),Fold(range.ToList(),Fold(range))); }
        public static int Edges(int a,int b) { int count=(b&15)+1;int start=a<0?int.MinValue:int.MaxValue-count+1;var range=Enumerable.Range(start,count);return Fold(range.ToArray(),Fold(range)); }
        public static int Validation(int a,int b)
        {
            try{var range=Enumerable.Range(a,b);return unchecked(range.Count()*31+(range.Any()?7:11));}
            catch(ArgumentOutOfRangeException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName));}
        }
        public static int Cursor(int a,int b)
        {
            var range=Enumerable.Range(a%97,(b&7)+1);var first=range.GetEnumerator();var second=range.GetEnumerator();int h=first.Current;
            while(first.MoveNext()){h=unchecked(h*31+first.Current);if(second.MoveNext())h=unchecked(h*17+second.Current);}
            h=unchecked(h*31+first.Current);first.Dispose();h=unchecked(h*31+first.Current);first.MoveNext();h=unchecked(h*31+first.Current);
            try{first.Reset();}catch(NotSupportedException e){h=unchecked(h+Hash(e.Message));}second.Dispose();return Fold(range,h);
        }
        public static int List(int a,int b)
        {
            var list=(IList<int>)Enumerable.Range(a%97,8);int h=list.Count+(list.IsReadOnly?7:11);h=unchecked(h*31+list.IndexOf(b)+(list.Contains(b)?17:19));
            try{h=unchecked(h*31+list[b]);}catch(ArgumentOutOfRangeException e){h=unchecked(h+Hash(e.Message)+Hash(e.ParamName));}
            try{list.Add(a);}catch(NotSupportedException e){h=unchecked(h+Hash(e.Message));}try{list.RemoveAt(-1);}catch(NotSupportedException e){h=unchecked(h+Hash(e.Message));}return h;
        }
        public static int Copy(int a,int b)
        {
            var list=(ICollection<int>)Enumerable.Range(a%97,5);int[] target=a<0?null:new int[8];try{list.CopyTo(target,b);return Fold(target);}
            catch(ArgumentOutOfRangeException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName));}
        }
        public static int Terminals(int a,int b)
        {
            var range=Enumerable.Range(a%97,(b&7)+1);int h=range.Count()+(range.Any()?7:11);h=unchecked(h*31+range.First()+range.FirstOrDefault()+(range.Contains(b)?13:17));
            try{h=unchecked(h+range.Single());}catch(InvalidOperationException e){h=unchecked(h+Hash(e.Message));}return h;
        }
        public static int Selection(int a,int b)
        {
            var trace=new RangeTrace();var range=Enumerable.Range(a%97,9);var query=range.Select(trace.Select);int h=query.Count()+(query.Any()?7:11)+query.First();h=Fold(query.ToArray(),h);h=Fold(query.ToList(),h);h=unchecked(h*31+(query.Contains(b)?13:17));return unchecked(h+trace.Trace);
        }
        public static int SelectionFaults(int a,int b)
        {
            var trace=new RangeTrace{Fail=2};var query=Enumerable.Range(-1,9).Select(trace.Select);int h=17;
            try{int mode=b&7;if(mode==0)h=query.Count();else if(mode==1)h=query.Any()?7:11;else if(mode==2)h=query.First();else if(mode==3)h=Fold(query.ToArray());else if(mode==4)h=Fold(query.ToList());else if(mode==5)h=query.Contains(a)?13:17;else h=Fold(query);}
            catch(InvalidOperationException e){h=Hash(e.Message);}return unchecked(h+trace.Trace);
        }
        public static int SourceCursor(int a,int b)
        {
            var range=Enumerable.Range(a%97,5);var source=(IEnumerator<int>)range;var trace=new RangeTrace{Cursor=source};var query=range.Select(trace.Select);var cursor=(IEnumerator<int>)query;int h=Fold(query.ToArray());h=unchecked(h*31+source.Current+cursor.Current);h=unchecked(h*31+(query.Contains(b)?7:11)+source.Current+cursor.Current);return unchecked(h+trace.Trace);
        }
        public static int Skip(int a,int b)
        {
            WarmEmpty();var range=Enumerable.Range(a%97,9);var query=range.Skip(b);int h=Fold(query)+(query==range?7:11);h=Fold(query.Skip(3),h);return h;
        }
        public static int Empty(int a,int b)
        {
            WarmEmpty();var range=Enumerable.Range(a,0);int h=Fold(range)+(range.Any()?7:11)+range.Count()+range.ToArray().Length+range.ToList().Count;
            h=unchecked(h*31+range.FirstOrDefault());try{h=range.First();}catch(InvalidOperationException e){h=unchecked(h+Hash(e.Message));}return h;
        }
    }
}
