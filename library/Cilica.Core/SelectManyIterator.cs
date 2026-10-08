// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned SelectMany/Iterator/SegmentedArrayBuilder sources.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Runtime
{
    public sealed class SelectManyIterator<TSource,TResult> : IEnumerable<TResult>, IEnumerator<TResult>,
        IQueryCardinality, IQueryCheapCount, IQueryMaterialization<TResult>, IQueryContains<TResult>
    {
        private readonly IEnumerable<TSource> source;
        private readonly Func<TSource,IEnumerable<TResult>> selector;
        private readonly int threadId;
        private IEnumerator<TSource> outer;
        private IEnumerator<TResult> inner;
        private int state;
        private TResult current;
        internal SelectManyIterator(IEnumerable<TSource> source,Func<TSource,IEnumerable<TResult>> selector)
        { this.source=source;this.selector=selector;threadId=Environment.CurrentManagedThreadId; }
        public IEnumerator<TResult> GetEnumerator()
        {
            var result=state==0 && threadId==Environment.CurrentManagedThreadId ? this : new SelectManyIterator<TSource,TResult>(source,selector);
            result.state=1;return result;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public TResult Current => current;
        object IEnumerator.Current => current;
        private void SetCurrent(TResult value) { current=value; }
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose()
        {
            if (inner!=null) { inner.Dispose();inner=null; }
            if (outer!=null) { outer.Dispose();outer=null; }
            current=default;state=-1;
        }
        public bool MoveNext()
        {
            if (state==1) { outer=source.GetEnumerator();state=2; }
            while (state==2 || state==3)
            {
                if (state==2)
                {
                    if (!outer.MoveNext()) break;
                    TSource value=outer.Current;
                    inner=selector(value).GetEnumerator();state=3;
                }
                if (inner.MoveNext()) { SetCurrent(inner.Current);return true; }
                inner.Dispose();inner=null;state=2;
            }
            Dispose();return false;
        }
        public int GetCheapCount() => -1;
        public int Count()
        {
            int count=0;
            foreach (TSource value in source) count=checked(count+Enumerable.Count(selector(value)));
            return count;
        }
        public bool Any() { using (var e=GetEnumerator()) return e.MoveNext(); }
        public bool Contains(TResult value)
        {
            foreach (TSource item in source) if (Enumerable.Contains(selector(item),value)) return true;
            return false;
        }
        public TResult[] ToArray() => Materialize();
        public List<TResult> ToList() => List<TResult>.CopyEnumerable(Materialize());
        private TResult[] Materialize()
        {
            var buffer=new SelectManyBuffer<TResult>();
            foreach (TSource item in source) buffer.AddRange(selector(item));
            return buffer.ToArray();
        }
    }
    // Managed segments replace pooled arrays and an eight-element stack scratch
    // buffer. Segment sizes retain the collection Count/CopyTo callback choices.
    public sealed class SelectManySegment<T>
    {
        internal T[] Items { get; }
        internal SelectManySegment<T> Next { get; set; }
        internal SelectManySegment(int length) { Items=new T[length]; }
    }
    public sealed class SelectManyBuffer<T>
    {
        private readonly SelectManySegment<T> first;
        private SelectManySegment<T> current;
        private int count,finished;
        internal SelectManyBuffer() { first=new SelectManySegment<T>(8);current=first; }
        private void Expand(int minimum)
        {
            int oldLength=current.Items.Length;
            finished=checked(finished+oldLength);
            if (finished>0x7fffffc7) throw new OutOfMemoryException();
            long requested=Math.Min(Math.Max((long)minimum,Math.Max(16L,oldLength*2L)),0x7fffffc7L);
            int capacity=16;
            while (capacity<requested && capacity<0x40000000) capacity*=2;
            if (capacity<requested) capacity=(int)requested;
            var next=new SelectManySegment<T>(capacity);current.Next=next;current=next;
        }
        private void Add(T value)
        {
            if (count==current.Items.Length) { Expand(16);count=0; }
            current.Items[count]=value;count++;
        }
        private void CopySpan(T[] items,int length)
        {
            int available=current.Items.Length-count;
            int take=Math.Min(available,length);
            for (int i=0;i<take;++i) current.Items[count+i]=items[i];
            count+=take;
            if (take<length)
            {
                Expand(length-take);
                for (int i=take;i<length;++i) current.Items[i-take]=items[i];
                count=length-take;
            }
        }
        internal void AddRange(IEnumerable<T> source)
        {
            if (source is ICollection<T> collection)
            {
                int length=collection.Count;
                if (length==0) return;
                // TryGetSpan uses exact runtime shapes, not user subclasses.
                if (source is T[] array && source.GetType()==typeof(T[])) { CopySpan(array,array.Length);return; }
                if (source is List<T> list && source.GetType()==typeof(List<T>))
                { T[] items=list.QueryStorage(out int size);CopySpan(items,size);return; }
                if (current!=first || count==current.Items.Length)
                {
                    int remaining=current.Items.Length-count;
                    if (remaining==0)
                    { Expand(length);collection.CopyTo(current.Items,0);count=length;return; }
                    if (length<=remaining)
                    { collection.CopyTo(current.Items,count);count+=length;return; }
                }
            }
            foreach (T value in source) Add(value);
        }
        internal T[] ToArray()
        {
            int length=checked(finished+count);
            if (length==0) return Array.Empty<T>();
            var result=new T[length];int index=0;
            for (var segment=first;segment!=current;segment=segment.Next)
                for (int i=0;i<segment.Items.Length;++i) result[index++]=segment.Items[i];
            for (int i=0;i<count;++i) result[index++]=current.Items[i];
            return result;
        }
    }
}
