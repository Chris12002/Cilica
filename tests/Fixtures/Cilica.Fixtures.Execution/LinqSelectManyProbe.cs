using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class ManyTrace
    {
        public int Code;
        public void Hit(int value) { Code=unchecked(Code*31+value); }
    }
    public class ManySequence : IEnumerable<int>
    {
        public ManyTrace Trace;
        public int Length,Start,Tag,Fault;
        public ManySequence(ManyTrace trace,int length,int start,int tag,int fault)
        { Trace=trace;Length=length;Start=start;Tag=tag;Fault=fault; }
        public IEnumerator<int> GetEnumerator()
        { Trace.Hit(Tag+1);if(Fault==1)throw new InvalidOperationException("get");return new ManyCursor(this); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public sealed class ManyCursor : IEnumerator<int>
    {
        private readonly ManySequence source;
        private int index;
        public ManyCursor(ManySequence source) { this.source=source; }
        public int Current { get { source.Trace.Hit(source.Tag+3);if(source.Fault==3)throw new InvalidOperationException("current");return unchecked(source.Start+index-1); } }
        object IEnumerator.Current => Current;
        public bool MoveNext()
        { source.Trace.Hit(source.Tag+2);if(source.Fault==2 && index==1)throw new InvalidOperationException("move");index++;return index<=source.Length; }
        public void Dispose() { source.Trace.Hit(source.Tag+4);if(source.Fault==4)throw new InvalidOperationException("dispose"); }
        public void Reset() { throw new NotSupportedException(); }
    }
    public sealed class ManyCollection : ManySequence,ICollection<int>
    {
        public ManyCollection(ManyTrace trace,int length,int start,int tag,int fault):base(trace,length,start,tag,fault) { }
        public int Count { get { Trace.Hit(Tag+5);if(Fault==5)throw new InvalidOperationException("count");return Length; } }
        public bool IsReadOnly => true;
        public void CopyTo(int[] array,int index)
        {
            Trace.Hit(Tag+6);Trace.Hit(index);Trace.Hit(array.Length);
            if(Fault==6)throw new InvalidOperationException("copy");
            for(int i=0;i<Length;++i)array[index+i]=unchecked(Start+i);
        }
        public bool Contains(int value) { Trace.Hit(Tag+7);return value>=Start && value<unchecked(Start+Length); }
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Remove(int value) { throw new NotSupportedException(); }
    }
    public sealed class ManySelector
    {
        public ManyTrace Trace;
        public int Mode,First,Second;
        public IEnumerable<int> Select(int value)
        {
            Trace.Hit(7);
            if(Mode==4)throw new InvalidOperationException("select");
            if(Mode==10)return null;
            return new ManySequence(Trace,2,value,20,Mode>=5 && Mode<=8?Mode-4:0);
        }
        public IEnumerable<int> Collection(int value)
        {
            Trace.Hit(7);
            return new ManyCollection(Trace,value==0?First:Second,value*100,20+value*10,Mode);
        }
    }
    public struct ManyPayload { public string Text;public long Number; }
    public static class LinqSelectManyProbe
    {
        private static int Fold(IEnumerable<int> values)
        { int result=0;foreach(int value in values)result=unchecked(result*17+value);return result; }
        public static int Values(int a,int b)
        { return Fold(new[]{a,0,b}.SelectMany(x=>x==0?Array.Empty<int>():new[]{x,unchecked(x+1)})); }
        public static int Deferred(int a,int b)
        {
            var trace=new ManyTrace();var selector=new ManySelector {Trace=trace};
            var query=new ManySequence(trace,2,a,10,0).SelectMany(selector.Select);
            int before=trace.Code;var cursor=query.GetEnumerator();bool same=ReferenceEquals(query,cursor);
            bool moved=cursor.MoveNext();int first=cursor.Current;cursor.Dispose();
            int again=Fold(query);
            return unchecked(before+trace.Code*3+first*7+again*11+(same?13:17)+(moved?19:23));
        }
        public static int Cursors(int a,int b)
        {
            var query=new[]{a,b}.SelectMany(x=>new[]{x,unchecked(x+1)});
            var first=query.GetEnumerator();var second=query.GetEnumerator();
            int result=(ReferenceEquals(query,first)?3:5)+(ReferenceEquals(query,second)?7:11);
            result=unchecked(result+first.Current);first.MoveNext();result=unchecked(result*17+first.Current);
            second.MoveNext();result=unchecked(result*17+second.Current);first.Dispose();result=unchecked(result*17+first.Current);
            if(first.MoveNext())result+=31;
            while(second.MoveNext())result=unchecked(result*17+second.Current);
            result=unchecked(result*17+second.Current);second.Dispose();return result;
        }
        public static int Validation(int a,int b)
        {
            int result=0;IEnumerable<int> missing=null;Func<int,IEnumerable<int>> selector=null;
            try { missing.SelectMany(selector); }catch(ArgumentNullException e){result+=e.ParamName=="source"?3:5;}
            try { Array.Empty<int>().SelectMany(selector); }catch(ArgumentNullException e){result+=e.ParamName=="selector"?7:11;}
            var empty=Array.Empty<int>().SelectMany(x=>new[]{x});
            return result+(ReferenceEquals(empty,Array.Empty<int>())?13:17);
        }
        public static int Faults(int a,int b)
        {
            int mode=(b&15)%11;var trace=new ManyTrace();var selector=new ManySelector {Trace=trace,Mode=mode};
            int outerFault=mode>=1 && mode<=3?mode:mode==9?4:0;
            var cursor=new ManySequence(trace,2,a,10,outerFault).SelectMany(selector.Select).GetEnumerator();
            int result=0;
            try { while(cursor.MoveNext())result=unchecked(result*17+cursor.Current); }
            catch(InvalidOperationException e){result=unchecked(result*17+e.Message.Length+1);}
            catch(NullReferenceException){result=unchecked(result*17+19);}
            result=unchecked(result*17+cursor.Current);
            try { cursor.Dispose(); }catch(InvalidOperationException){result=unchecked(result*17+23);}
            try { cursor.Dispose(); }catch(InvalidOperationException){result=unchecked(result*17+29);}
            return unchecked(result+trace.Code*31);
        }
        public static int NullInner(int a,int b)
        {
            var query=new[]{a}.SelectMany(x=>(IEnumerable<int>)null);int result=0;
            try { query.ToArray(); }catch(NullReferenceException){result+=3;}
            try { query.Count(); }catch(ArgumentNullException e){result+=e.ParamName=="source"?7:11;}
            try { query.Contains(b); }catch(ArgumentNullException e){result+=e.ParamName=="source"?13:17;}
            try { query.Any(); }catch(NullReferenceException){result+=19;}
            return result;
        }
        public static int Terminals(int a,int b)
        {
            var trace=new ManyTrace();var selector=new ManySelector {Trace=trace,First=a&7,Second=b&7};
            var query=new[]{0,1,2}.SelectMany(selector.Collection);
            int count=query.Count();bool contains=query.Contains(100);bool any=query.Any();int first=query.FirstOrDefault();
            return unchecked(trace.Code+count*3+(contains?7:11)+(any?13:17)+first*19);
        }
        public static int MaterializeArray(int a,int b)
        {
            var trace=new ManyTrace();var selector=new ManySelector {Trace=trace,First=a&63,Second=b&63};
            var query=new[]{0,1,2}.SelectMany(selector.Collection);var values=query.ToArray();
            return unchecked(trace.Code+Fold(values)*3+values.Length*7);
        }
        public static int MaterializeList(int a,int b)
        {
            var trace=new ManyTrace();var selector=new ManySelector {Trace=trace,First=a&63,Second=b&63};
            var query=new[]{0,1,2}.SelectMany(selector.Collection);var values=query.ToList();
            return unchecked(trace.Code+Fold(values)*3+values.Count*7+values.Capacity*11);
        }
        public static int MaterializeFault(int a,int b)
        {
            var trace=new ManyTrace();var selector=new ManySelector {Trace=trace,First=8,Second=16,Mode=(b&1)==0?5:6};
            var query=new ManySequence(trace,2,0,10,0).SelectMany(selector.Collection);int result=0;
            try { query.ToArray(); }catch(InvalidOperationException e){result=e.Message.Length;}
            return unchecked(trace.Code+result*17);
        }
        public static int ArraysAndLists(int a,int b)
        {
            var lists=new List<int>(new[]{a,0,b});
            var values=lists.SelectMany(x=>x==0?Array.Empty<int>():new[]{x,unchecked(x+1)}).ToArray();
            var query=new[]{a,b}.SelectMany(x=>new List<int>(new[]{x,unchecked(x+1)})).ToList();
            return unchecked(Fold(values)+Fold(query)*3+query.Capacity*7);
        }
        public static int Payloads(int a,int b)
        {
            var values=new[]{a,b}.SelectMany(x=>new[]{new ManyPayload {Text=x.ToString(),Number=(long)x*17}}).ToArray();
            return unchecked(values[0].Text.Length+values[1].Text.Length+(int)values[0].Number+(int)values[1].Number);
        }
        public static int Composition(int a,int b)
        {
            var trace=new ManyTrace();var selector=new ManySelector {Trace=trace,First=a&7,Second=b&7};
            var query=new[]{0,1,2}.Where(x=>x!=1).SelectMany(selector.Collection);
            var cursor=query.GetEnumerator();cursor.MoveNext();int before=cursor.Current;
            int count=query.Select(x=>unchecked(x+3)).Count();var array=query.Select(x=>unchecked(x+5)).ToArray();
            bool moved=cursor.MoveNext();int after=cursor.Current;cursor.Dispose();
            return unchecked(trace.Code+count*3+Fold(array)*7+before*11+after*13+(moved?17:19));
        }
        public static int OverflowCount(int a,int b)
        {
            var trace=new ManyTrace();var selector=new ManySelector {Trace=trace,First=int.MaxValue,Second=1};
            var query=new ManySequence(trace,2,0,10,0).SelectMany(selector.Collection);
            try { return query.Count(); }catch(OverflowException){return unchecked(trace.Code+17);}
        }
        public static int LegacyCursor(int a,int b)
        {
            var query=new[]{a,b}.SelectMany(x=>new[]{x});
            var cursor=((IEnumerable)query).GetEnumerator();int result=(int)cursor.Current;
            cursor.MoveNext();result=unchecked(result*17+(int)cursor.Current);
            try { cursor.Reset(); }catch(NotSupportedException){result=unchecked(result+31);}
            ((IDisposable)cursor).Dispose();return unchecked(result*17+(int)cursor.Current);
        }
        public static int UnsupportedNestedVector(int a,int b)
        { var values=new List<int[]>();values.Add(new[]{a,b});return values.SelectMany(x=>x).Count(); }
        public static int UnsupportedIndexed(int a,int b) => new[]{a,b}.SelectMany((x,i)=>new[]{x,i}).Count();
        public static int UnsupportedResult(int a,int b) => new[]{a,b}.SelectMany(x=>new[]{x},(x,y)=>x+y).Count();
    }
}
