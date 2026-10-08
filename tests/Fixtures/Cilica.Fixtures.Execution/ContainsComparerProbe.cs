using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
namespace Cilica.Fixtures.Execution
{
    public sealed class ContainsStorageComparer : IEqualityComparer<int>
    {
        private readonly List<int> list;private readonly int[] array;private readonly int mode,target;
        public int Calls;
        public ContainsStorageComparer(List<int> list,int[] array,int mode,int target){this.list=list;this.array=array;this.mode=mode;this.target=target;}
        public bool Equals(int left,int right)
        {
            if(Calls++==0)
            {
                if(array!=null)array[1]=target;
                else if((mode&1)==0){list.Add(91);list[1]=unchecked(target+1);}
                else list.Clear();
            }
            return new SequenceBox(left).Number==right;
        }
        public int GetHashCode(int value)=>value;
    }
    public sealed class ContainsCollection : ICollection<string>
    {
        public int Count {get{throw new NotSupportedException();}}
        public bool IsReadOnly=>true;
        public bool Contains(string value)=>StringComparer.OrdinalIgnoreCase.Equals(value,"alpha");
        public IEnumerator<string> GetEnumerator()=>((IEnumerable<string>)new[]{"alpha"}).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public void Add(string value){throw new NotSupportedException();}
        public void Clear(){throw new NotSupportedException();}
        public void CopyTo(string[] array,int index){throw new NotSupportedException();}
        public bool Remove(string value){throw new NotSupportedException();}
    }
    public static class ContainsComparerProbe
    {
        private static int Hash(string text){int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h;}
        public static int Strings(int a,int b)
        {
            var source=new[]{"alpha",null,"BETA"};string value=a==0?null:(a&1)==0?"ALPHA":"alpha";
            return (source.Contains(value,StringComparer.Ordinal)?1:0)+(source.Contains(value,StringComparer.OrdinalIgnoreCase)?7:0);
        }
        public static int CollectionDefaults(int a,int b)
        {
            var set=new ContainsCollection();
            return (Enumerable.Contains(set,"ALPHA")?1:0)+(Enumerable.Contains(set,"ALPHA",null)?7:0)+(Enumerable.Contains(set,"ALPHA",StringComparer.Ordinal)?31:0);
        }
        public static int CustomCollection(int a,int b)
        {
            var trace=new SequenceTrace();bool found=new SequenceList(trace,1,a&3).Contains(b&3,(IEqualityComparer<int>)null);
            return unchecked(trace.Value*17+(found?1:0));
        }
        public static int Traversal(int a,int b)
        {
            var trace=new SequenceTrace();int result;
            try{result=new SequenceSource(trace,1+(b&1),a&3,(a>>2)&7).Contains(b&3,(IEqualityComparer<int>)null)?1:0;}
            catch(InvalidOperationException){result=2;}
            return unchecked(trace.Value*17+result);
        }
        public static int Comparer(int a,int b)
        {
            var trace=new SequenceTrace();int result;
            try{result=new SequenceSource(trace,1,a&3,0).Contains(b,new SequenceComparer(trace,(a>>2)&1))?1:0;}
            catch(InvalidOperationException){result=2;}
            return unchecked(trace.Value*17+result);
        }
        public static int Grow(int a,int b)
        {
            var source=new List<int>(2){a,b};var comparer=new ContainsStorageComparer(source,null,a,b);
            bool found=source.Contains(b,comparer);return (found?1:0)+source.Count*7+comparer.Calls*31;
        }
        public static int ArrayMutation(int a,int b)
        {
            var source=new[]{a,unchecked(b+1)};var comparer=new ContainsStorageComparer(null,source,0,b);
            bool found=source.Contains(b,comparer);return unchecked((found?1:0)+source[1]*7+comparer.Calls*31);
        }
        public static int Values(int a,int b)
        {
            var source=new[]{new SequenceValue(a),new SequenceValue(b)};
            bool found=source.Contains(new SequenceValue(b),(IEqualityComparer<SequenceValue>)null);
            return unchecked((found?1:0)+source[0].Number*7+source[1].Number*31);
        }
        public static int Wide(int a,int b)
        {long value=unchecked(((long)a<<32)|(uint)b);return new[]{value,7L}.Contains(value,(IEqualityComparer<long>)null)?1:0;}
        public static int Null(int a,int b)
        {
            var trace=new SequenceTrace();
            try{return Enumerable.Contains(a==0?null:new int[0],b,new SequenceComparer(trace,1))?1:0;}
            catch(ArgumentNullException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName));}
        }
        public static int UnsupportedIgnoreCaseSet(int a,int b)=>new HashSet<string>(StringComparer.OrdinalIgnoreCase).Count;
        public static int UnsupportedNullable(int a,int b)=>new int?[]{a}.Contains((int?)b,(IEqualityComparer<int?>)null)?1:0;
    }
}
