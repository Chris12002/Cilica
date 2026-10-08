// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned Grouping/Lookup; see library/provenance.json.
using System;
using System.Collections;
using System.Collections.Generic;
namespace System.Linq
{
    public interface IGrouping<out TKey, out TElement> : IEnumerable<TElement> { TKey Key { get; } }
    public static partial class Enumerable
    {
        public static IEnumerable<IGrouping<TKey,TSource>> GroupBy<TSource,TKey>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(keySelector==null)throw new ArgumentNullException("keySelector");
            if(source is TSource[] array && NoElements(array.Length))return Array.Empty<IGrouping<TKey,TSource>>();
            return new Cilica.Runtime.GroupByIterator<TSource,TKey>(source,keySelector);
        }
    }
}
namespace Cilica.Runtime
{
    internal sealed class Grouping<TKey,TElement> : System.Linq.IGrouping<TKey,TElement>,IList<TElement>
    {
        public TKey KeyValue { get; }
        public int HashCode { get; }
        public Grouping<TKey,TElement> HashNext { get; set; }
        public Grouping<TKey,TElement> Next { get; set; }
        private TElement[] elements;
        private int count;
        internal Grouping(TKey key,int hash) { KeyValue=key;HashCode=hash;elements=new TElement[1]; }
        internal static Grouping<TKey,TElement>[] CreateTable(int length)=>new Grouping<TKey,TElement>[length];
        internal void Append(TElement element)
        {
            if(elements.Length==count)
            {
                var grown=new TElement[checked(count*2)];
                for(int i=0;i<count;i++)grown[i]=elements[i];
                elements=grown;
            }
            elements[count]=element;count++;
        }
        public TKey Key=>KeyValue;
        public int Count=>count;
        public bool IsReadOnly=>true;
        public TElement this[int index]
        {
            get { if((uint)index>=(uint)count)throw new ArgumentOutOfRangeException("index");return elements[index]; }
            set { throw new NotSupportedException(); }
        }
        public IEnumerator<TElement> GetEnumerator()=>new GroupArrayEnumerator<TElement>(elements,count);
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public bool Contains(TElement item)=>IndexOf(item)>=0;
        public int IndexOf(TElement item)=>KeyOperations<TElement>.IndexOf(elements,item,count);
        public void CopyTo(TElement[] array,int arrayIndex)=>ArrayInterface<TElement>.CopyPrefix(elements,count,array,arrayIndex);
        public void Add(TElement item) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Remove(TElement item) { throw new NotSupportedException(); }
        public void Insert(int index,TElement item) { throw new NotSupportedException(); }
        public void RemoveAt(int index) { throw new NotSupportedException(); }
    }
    internal sealed class GroupArrayEnumerator<T> : IEnumerator<T>
    {
        private readonly T[] array;
        private readonly int count;
        private int index=-1;
        internal GroupArrayEnumerator(T[] array,int count) { this.array=array;this.count=count; }
        public bool MoveNext() { if(index+1<count) { index++;return true; }return false; }
        public T Current=>array[index];
        object IEnumerator.Current=>Current;
        public void Dispose() { }
        public void Reset() { index=-1; }
    }
    internal sealed class GroupLookup<TKey,TElement>
    {
        private Grouping<TKey,TElement>[] groups=Grouping<TKey,TElement>.CreateTable(7);
        public Grouping<TKey,TElement> Last { get; private set; }
        public int Count { get; private set; }
        internal static GroupLookup<TKey,TElement> Create(IEnumerable<TElement> source,Func<TElement,TKey> selector)
        {
            var lookup=new GroupLookup<TKey,TElement>();
            foreach(TElement item in source)lookup.GetGroup(selector(item)).Append(item);
            return lookup;
        }
        private static int Hash(TKey key)=>KeyOperations<TKey>.IsNull(key)?0:KeyOperations<TKey>.Hash(ref key)&0x7fffffff;
        private Grouping<TKey,TElement> GetGroup(TKey key)
        {
            int hash=Hash(key);
            for(var group=groups[hash%groups.Length];group!=null;group=group.HashNext)
            {
                if(group.HashCode==hash)
                {
                    TKey stored=group.KeyValue;
                    if(KeyOperations<TKey>.Equal(ref stored,key))return group;
                }
            }
            if(Count==groups.Length)Resize();
            int index=hash%groups.Length;
            var created=new Grouping<TKey,TElement>(key,hash);
            created.HashNext=groups[index];groups[index]=created;
            if(Last==null)created.Next=created;
            else { created.Next=Last.Next;Last.Next=created; }
            Last=created;Count++;return created;
        }
        private void Resize()
        {
            int length=checked(Count*2+1);var resized=Grouping<TKey,TElement>.CreateTable(length);var group=Last;
            do { group=group.Next;int index=group.HashCode%length;group.HashNext=resized[index];resized[index]=group; }while(group!=Last);
            groups=resized;
        }
        internal System.Linq.IGrouping<TKey,TElement>[] ToArray()
        {
            if(Count==0)return Array.Empty<System.Linq.IGrouping<TKey,TElement>>();
            var result=new System.Linq.IGrouping<TKey,TElement>[Count];var group=Last;
            for(int i=0;i<Count;i++) { group=group.Next;result[i]=group; }return result;
        }
        internal List<System.Linq.IGrouping<TKey,TElement>> ToList()
        {
            var result=new List<System.Linq.IGrouping<TKey,TElement>>(Count);var group=Last;
            for(int i=0;i<Count;i++) { group=group.Next;result.Add(group); }return result;
        }
    }
    public sealed class GroupByIterator<TSource,TKey> : IEnumerable<System.Linq.IGrouping<TKey,TSource>>,IEnumerator<System.Linq.IGrouping<TKey,TSource>>,
        IQueryMaterialization<System.Linq.IGrouping<TKey,TSource>>,IQueryCardinality,IQueryCheapCount,IQueryTraversal<System.Linq.IGrouping<TKey,TSource>>,
        IQueryFirst<System.Linq.IGrouping<TKey,TSource>>,IQueryFirstProjection<System.Linq.IGrouping<TKey,TSource>>,IQuerySkip<System.Linq.IGrouping<TKey,TSource>>
    {
        private readonly IEnumerable<TSource> source;
        private readonly Func<TSource,TKey> selector;
        private readonly int threadId;
        private GroupLookup<TKey,TSource> lookup;
        private Grouping<TKey,TSource> group;
        private System.Linq.IGrouping<TKey,TSource> current;
        private int state;
        internal GroupByIterator(IEnumerable<TSource> source,Func<TSource,TKey> selector) { this.source=source;this.selector=selector;threadId=Environment.CurrentManagedThreadId; }
        public IEnumerator<System.Linq.IGrouping<TKey,TSource>> GetEnumerator()
        { var result=state==0&&threadId==Environment.CurrentManagedThreadId?this:new GroupByIterator<TSource,TKey>(source,selector);result.state=1;return result; }
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public System.Linq.IGrouping<TKey,TSource> Current=>current;
        object IEnumerator.Current=>current;
        public void Reset() { throw new NotSupportedException(); }
        // Pinned Iterator.Dispose clears Current/state; GroupBy retains lookup/group.
        public void Dispose() { current=null;state=-1; }
        public bool MoveNext()
        {
            if(state==1)
            {
                lookup=GroupLookup<TKey,TSource>.Create(source,selector);group=lookup.Last;
                if(group!=null) { state=2;group=group.Next;current=group;return true; }
            }
            else if(state==2&&group!=lookup.Last) { group=group.Next;current=group;return true; }
            Dispose();return false;
        }
        public System.Linq.IGrouping<TKey,TSource>[] ToArray()=>GroupLookup<TKey,TSource>.Create(source,selector).ToArray();
        public List<System.Linq.IGrouping<TKey,TSource>> ToList()=>GroupLookup<TKey,TSource>.Create(source,selector).ToList();
        public int Count()=>GroupLookup<TKey,TSource>.Create(source,selector).Count;
        public int GetCheapCount()=>-1;
        public bool Any() { TryFirst(out int found);return found!=0; }
        public System.Linq.IGrouping<TKey,TSource> TryFirst(out int found)
        {
            using(var e=GetEnumerator())if(e.MoveNext()) { found=1;return e.Current; }
            found=0;return null;
        }
        public IEnumerator<System.Linq.IGrouping<TKey,TSource>> GetQueryEnumerator(int terminal,out int count) { count=-1;return GetEnumerator(); }
        public IEnumerable<System.Linq.IGrouping<TKey,TSource>> Skip(int count)=>UnsupportedLibraryMember<IEnumerable<System.Linq.IGrouping<TKey,TSource>>>.Invoke();
    }
}
