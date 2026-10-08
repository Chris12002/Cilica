// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded Int32 adaptation of pinned Range.cs/Range.SpeedOpt.cs.
using System;
using System.Collections;
using System.Collections.Generic;
namespace System.Linq
{
    public static partial class Enumerable
    {
        public static IEnumerable<int> Range(int start,int count)
        {
            long last=(long)start+count-1;
            if(count<0||last>2147483647)throw new ArgumentOutOfRangeException("count");
            if(count==0)return Array.Empty<int>();
            return new Cilica.Runtime.RangeIterator(start,unchecked(start+count));
        }
    }
}
namespace Cilica.Runtime
{
    public sealed class RangeIterator : IEnumerable<int>,IEnumerator<int>,IList<int>,IReadOnlyList<int>,
        IQueryCardinality,IQueryCheapCount,IQueryMaterialization<int>,IQueryTraversal<int>,
        IQueryFirst<int>,IQueryFirstProjection<int>,IQueryContains<int>,IQueryContainsTraversal<int>,IQuerySkip<int>
    {
        private readonly int start,end,threadId;
        private int state,current;
        internal RangeIterator(int start,int end) { this.start=start;this.end=end;threadId=Environment.CurrentManagedThreadId; }
        public IEnumerator<int> GetEnumerator()
        {
            var iterator=state==0&&threadId==Environment.CurrentManagedThreadId?this:new RangeIterator(start,end);
            iterator.state=1;return iterator;
        }
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public int Current=>current;
        object IEnumerator.Current=>current;
        public bool MoveNext()
        {
            if(state==1) { current=start;state=2;return true; }
            if(state==2) { current=unchecked(current+1);if(current!=end)return true; }
            state=-1;return false;
        }
        // Pinned Range retains Current both after exhaustion and disposal.
        public void Dispose() { state=-1; }
        public void Reset() { throw new NotSupportedException(); }
        public int Count=>unchecked(end-start);
        int IQueryCardinality.Count()=>Count;
        public int GetCheapCount()=>Count;
        public bool Any()=>true;
        public int TryFirst(out int found) { found=1;return start; }
        public bool Contains(int item)=>(uint)unchecked(item-start)<(uint)Count;
        public int IndexOf(int item) { uint index=(uint)unchecked(item-start);return index<(uint)Count?(int)index:-1; }
        public bool IsReadOnly=>true;
        public int this[int index]
        {
            get { if((uint)index>=(uint)Count)throw new ArgumentOutOfRangeException("index");return unchecked(start+index); }
            set { throw new NotSupportedException(); }
        }
        public void CopyTo(int[] array,int arrayIndex)
        {
            // The pinned Span(array, start, length) bounds failure has no parameter name.
            int count=Count;
            if(array==null||(uint)arrayIndex>(uint)array.Length||(uint)count>(uint)(array.Length-arrayIndex))
                throw new ArgumentOutOfRangeException();
            for(int i=0;i<count;i++)array[arrayIndex+i]=unchecked(start+i);
        }
        public int[] ToArray()
        {
            int count=Count;var result=new int[count];for(int i=0;i<count;i++)result[i]=unchecked(start+i);return result;
        }
        public List<int> ToList()
        {
            int count=Count;var result=new List<int>(count);for(int i=0;i<count;i++)result.Add(unchecked(start+i));return result;
        }
        // RangeSelect's optimized paths must not advance a retained source cursor.
        public IEnumerator<int> GetQueryEnumerator(int terminal,out int count)
        { count=Count;return new RangeIterator(start,end).GetEnumerator(); }
        public IEnumerator<int> GetContainsEnumerator()=>new RangeIterator(start,end).GetEnumerator();
        public IEnumerable<int> Skip(int count)=>count>=Count?Array.Empty<int>():new RangeIterator(unchecked(start+count),end);
        public void Add(int item) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public void Insert(int index,int item) { throw new NotSupportedException(); }
        public bool Remove(int item) { throw new NotSupportedException(); }
        public void RemoveAt(int index) { throw new NotSupportedException(); }
    }
}
