using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class SingleProbeList : IList<int>
    {
        public ManyTrace Trace;
        public int Length,Value,Fault;
        public int Count { get { Trace.Hit(1);if(Fault==1)throw new InvalidOperationException("count");return Length; } }
        public int this[int index] { get { Trace.Hit(2);Trace.Hit(index);if(Fault==2)throw new InvalidOperationException("index");return Value; } set { throw new NotSupportedException(); } }
        public bool IsReadOnly => true;
        public IEnumerator<int> GetEnumerator() { Trace.Hit(3);throw new NotSupportedException(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int IndexOf(int value) { throw new NotSupportedException(); }
        public void Insert(int index,int value) { throw new NotSupportedException(); }
        public void RemoveAt(int index) { throw new NotSupportedException(); }
        public bool Contains(int value) { throw new NotSupportedException(); }
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Remove(int value) { throw new NotSupportedException(); }
        public void CopyTo(int[] array,int index) { throw new NotSupportedException(); }
    }
    public static class LinqSingleSourceProbe
    {
        private static int Hash(string value)
        { int result=0;for(int i=0;i<value.Length;++i)result=unchecked(result*31+value[i]);return result; }
        private static int Read(IEnumerable<int> source,bool useDefault)
        {
            try { int value=useDefault?source.SingleOrDefault():source.Single();return unchecked(value*17+3); }
            catch(ArgumentNullException e){return unchecked(Hash(e.ParamName)+Hash(e.Message)*31+5);}
            catch(InvalidOperationException e){return unchecked(Hash(e.Message)*31+7);}
        }
        public static int Arrays(int a,int b)
        {
            int mode=b&3;int[] values=mode==0?Array.Empty<int>():mode==1?new[]{a}:mode==2?new[]{a,b}:null;
            return unchecked(Read(values,false)+Read(values,true)*17);
        }
        public static int Generic(int a,int b)
        {
            var trace=new ManyTrace();var source=new ManySequence(trace,a&3,unchecked(a+b),10,b);
            int first=Read(source,false),second=Read(source,true);
            return unchecked(first+second*17+trace.Code*31);
        }
        public static int Lists(int a,int b)
        {
            var trace=new ManyTrace();var source=new SingleProbeList {Trace=trace,Length=a,Value=17,Fault=b};
            int first=Read(source,false),second=Read(source,true);
            return unchecked(first+second*17+trace.Code*31);
        }
        public static int CollectionOnly(int a,int b)
        {
            var trace=new ManyTrace();var source=new ManyCollection(trace,a&3,b,10,5);
            int first=Read(source,false),second=Read(source,true);
            return unchecked(first+second*17+trace.Code*31);
        }
        public static int Nullable(int a,int b)
        {
            int?[] values=new int?[]{a<0?(int?)null:b};int? value=values.Single();
            int? empty=Array.Empty<int?>().SingleOrDefault();
            return unchecked((value.HasValue?value.Value:17)+(empty.HasValue?31:37));
        }
        public static int References(int a,int b)
        {
            string value=new[]{b<0?null:a.ToString()}.Single();
            string empty=Array.Empty<string>().SingleOrDefault();
            return (value==null?17:value.Length)+(empty==null?31:37);
        }
        public static int Values(int a,int b)
        {
            var value=new[]{new ManyPayload {Text=a.ToString(),Number=(long)b*17}}.Single();
            var empty=Array.Empty<ManyPayload>().SingleOrDefault();
            return unchecked(value.Text.Length+(int)value.Number+(empty.Text==null?31:37)+(int)empty.Number);
        }
        public static int Composition(int a,int b)
        {
            var query=new[]{a,b}.Where(x=>x==a);
            return unchecked(Read(query,false)+Read(query,true)*17);
        }
        public static int ReadOnly(int a,int b)
        {
            var trace=new ManyTrace();var source=new SingleProbeList {Trace=trace,Length=a&3,Value=b,Fault=0};
            var readOnly=new ReadOnlyCollection<int>(source);
            int first=Read(readOnly,false),second=Read(readOnly,true);
            return unchecked(first+second*17+trace.Code*31);
        }
    }
}
