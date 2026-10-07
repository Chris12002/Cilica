using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class MaterializationCountList : IList<int>
    {
        public int Reported, Trace;
        public int Count { get { Trace = unchecked(Trace * 31 + 1); return Reported; } }
        public int this[int i] { get { Trace = unchecked(Trace * 31 + i + 2); return i + 11; } set { throw new NotSupportedException(); } }
        public bool IsReadOnly => true;
        public IEnumerator<int> GetEnumerator() { throw new NotSupportedException(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(int x) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int x) { throw new NotSupportedException(); }
        public bool Remove(int x) { throw new NotSupportedException(); }
        public int IndexOf(int x) { throw new NotSupportedException(); }
        public void Insert(int i, int x) { throw new NotSupportedException(); }
        public void RemoveAt(int i) { throw new NotSupportedException(); }
        public void CopyTo(int[] a, int i) { throw new NotSupportedException(); }
    }
    public sealed class MaterializationCountCollection : ConcatSequence, ICollection<int>
    {
        public int Reported;
        public MaterializationCountCollection(ConcatTrace trace, int id, int count) : base(trace, id, 2, 0) { Reported = count; }
        public int Count { get { Trace.Add(Id * 100 + 5); return Reported; } }
        public bool IsReadOnly => true;
        public void CopyTo(int[] a, int i) { throw new NotSupportedException(); }
        public void Add(int x) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int x) { throw new NotSupportedException(); }
        public bool Remove(int x) { throw new NotSupportedException(); }
    }
    public static class LinqToListProbe
    {
        private static int Hash(string s) { int h = 17; for (int i=0; i<s.Length; i++) h=unchecked(h*31+s[i]); return h; }
        private static int Sum(List<int> x) { int h = x.Capacity + x.Count; for(int i=0;i<x.Count;i++) h=unchecked(h*7+x[i]); return h; }
        private static int Read(IEnumerable<int> source)
        {
            try { return Sum(source.ToList()); }
            catch (ArgumentException e) { return unchecked(Hash(e.Message)+Hash(e.ParamName)); }
            catch (InvalidOperationException e) { return Hash(e.Message); }
            catch (OverflowException e) { return Hash(e.Message); }
            catch (NullReferenceException e) { return Hash(e.Message); }
            catch (IndexOutOfRangeException e) { return Hash(e.Message); }
        }
        public static int Basic(int mode,int count)
        {
            if(mode==0) return Read(null);
            var source=new int[count]; for(int i=0;i<count;i++) source[i]=i+11;
            IEnumerable<int> query=source;
            if(mode==2) query=new List<int>(source);
            if(mode==3) { var c=new ConstructionSequence<int>(); c.Data=source; query=c; }
            var result=query.ToList(); if(count>0) source[0]=37;
            return Sum(result)+(ReferenceEquals(result,query)?-100:13);
        }
        public static int Empty(int mode,int unused)
        {
            IEnumerable<int> query=mode==0?new int[0]:new List<int>();
            var first=query.ToList();var second=query.ToList();first.Add(19);
            return first.Count*31+second.Count+second.Capacity+(ReferenceEquals(first,second)?-100:7);
        }
        public static int FallbackFaults(int mode,int index)
        {
            var c=new ConstructionSequence<int>();c.Data=new int[3];c.Data[0]=11;c.Data[1]=13;c.Data[2]=17;c.Mode=mode;c.FaultIndex=index;
            return unchecked(Read(c)+c.Trace);
        }
        public static int Collection(int mode,int unused)
        {
            var c=new ConstructionCollection();c.Mode=mode;c.First=11;c.Second=13;
            return unchecked(Read(c)+c.Reads*31+c.Copies);
        }
        public static int Concat(int shape,int mode)
        {
            var trace=new ConcatTrace();IEnumerable<int> a,b;
            a=(shape&1)==0?(IEnumerable<int>)new ConcatSequence(trace,1,2,mode):new ConcatCollection(trace,1,2,mode);
            b=(shape&2)==0?(IEnumerable<int>)new ConcatSequence(trace,2,1,0):new ConcatCollection(trace,2,1,0);
            var query=a.Concat(b);
            if((shape&4)!=0) query=query.Concat(new ConcatCollection(trace,3,2,0)).Concat(new ConcatCollection(trace,4,1,0));
            return unchecked(Read(query)+trace.Value);
        }
        public static int Queries(int kind,int count)
        {
            var trace=new ConcatTrace();var values=new int[count];for(int i=0;i<count;i++) values[i]=i%3;
            IEnumerable<int> query=values;
            if(kind==0) query=values.Where(trace.Keep);
            if(kind==1) query=values.Select(trace.Project);
            if(kind==2) query=values.Where(trace.Keep).Select(trace.Project);
            if(kind==3) query=values.Distinct();
            if(kind==4) query=values.OrderBy(trace.Project);
            if(kind==5) query=((IEnumerable)values).OfType<int>();
            if(kind==6) query=values.OrderBy(trace.Project).ThenByDescending(trace.Project).Select(trace.Project);
            int result=Read(query);return unchecked(result+trace.Value);
        }
        public static int OfTypeReferences(int count,int unused)
        {
            var data=new object[count];
            for(int i=0;i<count;i++) data[i]=i%3==0?(object)"kept":i%3==1?new ValueVectorBox(i):null;
            var result=((IEnumerable)data).OfType<string>().ToList();
            int hash=result.Capacity+result.Count;
            for(int i=0;i<result.Count;i++) hash=unchecked(hash*7+result[i].Length);
            return hash;
        }
        public static int NegativeListCount(int count,int kind)
        {
            var c=new MaterializationCountList();c.Reported=count;var trace=new ConcatTrace();var q=c.Select(trace.Project);
            if((kind&1)!=0) q=q.Select(trace.Project);
            int result;
            try { result=kind<2?Sum(q.ToList()):q.ToArray().Length; }
            catch(ArgumentException e){result=unchecked(Hash(e.Message)+Hash(e.ParamName));}
            catch(OverflowException e){result=Hash(e.Message);}
            return unchecked(result+c.Trace+trace.Value);
        }
        public static int ReportedQueryCount(int count,int kind)
        {
            var trace=new ConcatTrace();var a=new MaterializationCountCollection(trace,1,count);var b=new MaterializationCountCollection(trace,2,0);
            var q=a.Concat(b).Select(trace.Project);
            if((kind&1)!=0) q=q.Select(trace.Project);
            int result;
            try { result=kind<2?Sum(q.ToList()):q.ToArray().Length; }
            catch(ArgumentException e){result=unchecked(Hash(e.Message)+Hash(e.ParamName));}
            catch(OverflowException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+trace.Value);
        }
    }
}
