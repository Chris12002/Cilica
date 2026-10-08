using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class SequenceTrace { public int Value;public void Note(int value){Value=unchecked(Value*31+value);} }
    public sealed class SequenceSource : IEnumerable<int>
    {
        private readonly SequenceTrace trace;private readonly int id,length,fault;
        public SequenceSource(SequenceTrace trace,int id,int length,int fault){this.trace=trace;this.id=id;this.length=length;this.fault=fault;}
        public IEnumerator<int> GetEnumerator()
        {trace.Note(10+id);if(fault==1 && id==2)throw new InvalidOperationException();return new SequenceCursor(trace,id,length,fault);}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public sealed class SequenceCursor : IEnumerator<int>
    {
        private readonly SequenceTrace trace;private readonly int id,length,fault;private int index=-1;
        public SequenceCursor(SequenceTrace trace,int id,int length,int fault){this.trace=trace;this.id=id;this.length=length;this.fault=fault;}
        public int Current {get {trace.Note(30+id);if((fault==4 && id==1)||(fault==5 && id==2))throw new InvalidOperationException();return index;}}
        object IEnumerator.Current=>Current;
        public bool MoveNext(){trace.Note(20+id);if((fault==2 && id==1)||(fault==3 && id==2))throw new InvalidOperationException();return ++index<length;}
        public void Dispose(){trace.Note(40+id);if((fault==6 && id==2)||fault==7)throw new InvalidOperationException();}
        public void Reset(){index=-1;}
    }
    public sealed class SequenceList : IList<int>
    {
        private readonly SequenceTrace trace;private readonly int id,length;
        public SequenceList(SequenceTrace trace,int id,int length){this.trace=trace;this.id=id;this.length=length;}
        public int Count {get{trace.Note(50+id);return length;}}
        public int this[int index]{get{trace.Note(60+id+index);return index;}set{throw new NotSupportedException();}}
        public bool IsReadOnly=>true;
        public IEnumerator<int> GetEnumerator()=>new SequenceSource(trace,id,length,0).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public void Add(int v){throw new NotSupportedException();} public void Clear(){throw new NotSupportedException();}
        public bool Contains(int v){throw new NotSupportedException();} public void CopyTo(int[] a,int i){throw new NotSupportedException();}
        public bool Remove(int v){throw new NotSupportedException();} public int IndexOf(int v){throw new NotSupportedException();}
        public void Insert(int i,int v){throw new NotSupportedException();} public void RemoveAt(int i){throw new NotSupportedException();}
    }
    public struct SequenceValue : IEquatable<SequenceValue>
    {
        public int Number;
        public SequenceValue(int number){Number=number;}
        public bool Equals(SequenceValue other){int before=Number;Number++;return new SequenceBox(before).Number==other.Number;}
        public override int GetHashCode()=>Number;
    }
    public sealed class SequenceBox {public int Number;public SequenceBox(int number){Number=number;}}
    public sealed class SequenceComparer : IEqualityComparer<int>
    {
        private readonly SequenceTrace trace;private readonly int fault;
        public SequenceComparer(SequenceTrace trace,int fault){this.trace=trace;this.fault=fault;}
        public bool Equals(int a,int b){trace.Note(90);if(fault!=0)throw new InvalidOperationException();return new SequenceBox(a&7).Number==(b&7);}
        public int GetHashCode(int value)=>value&7;
    }
    public sealed class SequenceGrowComparer : IEqualityComparer<int>
    {
        private readonly List<int> left,right;private int calls;private readonly int mode;
        public SequenceGrowComparer(List<int> left,List<int> right,int mode){this.left=left;this.right=right;this.mode=mode;}
        public bool Equals(int a,int b)
        {
            if(calls++==0)
            {
                if((mode&1)==0){left.Add(99);right.Add(98);left[1]=99;}
                else {left.Clear();right.Clear();}
            }
            return a==b;
        }
        public int GetHashCode(int value)=>value;
    }
    public static class SequenceEqualProbe
    {
        public static int Arrays(int a,int b) => new[]{a,b,3}.SequenceEqual(new[]{a,b,3}) ? (new[]{a,b}.SequenceEqual(new[]{a,b,0})?100:1) : -1;
        public static int Unequal(int a,int b)=>new[]{a,2}.SequenceEqual(new[]{b,2})?1:0;
        public static int Wide(int a,int b)=>new long[]{(long)a*10000000000L,b}.SequenceEqual(new long[]{(long)a*10000000000L,a})?1:0;
        public static int Strings(int a,int b)=>new[]{"hello",a==0?null:"x"}.SequenceEqual(new[]{"he"+"llo",b==0?null:"x"})?1:0;
        public static int Values(int a,int b)
        {
            var left=new[]{new SequenceValue(a),new SequenceValue(b)};var right=new[]{new SequenceValue(a),new SequenceValue(b)};
            bool equal=left.SequenceEqual(right);return (equal?1:0)+left[0].Number*17+left[1].Number;
        }
        public static int Lists(int a,int b)
        {
            var first=new List<int>{a,b};var second=new List<int>{a,b};
            return (first.SequenceEqual(second)?1:0)+(first.AsReadOnly().SequenceEqual(second.AsReadOnly())?2:0);
        }
        public static int Indexed(int a,int b)
        {
            var trace=new SequenceTrace();bool equal=new SequenceList(trace,1,a&3).SequenceEqual(new SequenceList(trace,2,b&3));return unchecked(trace.Value*17+(equal?1:0));
        }
        public static int Traversal(int a,int b)
        {
            var trace=new SequenceTrace();int result;
            try{result=new SequenceSource(trace,1,a&3,(a>>2)&7).SequenceEqual(new SequenceSource(trace,2,b&3,(a>>2)&7))?1:0;}
            catch(InvalidOperationException){result=2;}
            return unchecked(trace.Value*17+result);
        }
        public static int Comparer(int a,int b)
        {
            var trace=new SequenceTrace();int result;
            try{result=new SequenceSource(trace,1,a&3,0).SequenceEqual(new SequenceSource(trace,2,b&3,0),new SequenceComparer(trace,(a>>2)&1))?1:0;}
            catch(InvalidOperationException){result=2;}
            return unchecked(trace.Value*17+result);
        }
        public static int Null(int a,int b)
        {
            try { return Enumerable.SequenceEqual(a==0?null:new int[0],b==0?null:new int[0],(IEqualityComparer<int>)null)?1:0; }
            catch(ArgumentNullException e){return e.ParamName=="first"?2:3;}
        }
        public static int Grow(int a,int b)
        {
            var left=new List<int>(2){a,b};var right=new List<int>(2){a,b};
            bool equal=left.SequenceEqual(right,new SequenceGrowComparer(left,right,a));
            return (equal?1:0)+left.Count*7+right.Count*31;
        }
        public static int UnsupportedNullable(int a,int b)=>new int?[]{a}.SequenceEqual(new int?[]{b})?1:0;
    }
}
