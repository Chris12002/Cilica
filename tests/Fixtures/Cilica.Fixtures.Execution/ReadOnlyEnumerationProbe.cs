using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cilica.Fixtures.Execution
{
    public sealed class ReadOnlyTrackingList : IList<int>
    {
        private readonly List<int> items = new List<int>();
        public int Trace;
        public int Fault;
        public ReadOnlyTrackingList(int count, int value, int fault)
        { if (count != 0) items.Add(value); Fault = fault; }
        public int Count { get { Trace = unchecked(Trace * 10 + 1); if (Fault == 1) throw new InvalidOperationException("count"); return items.Count; } }
        public int this[int index] { get => items[index]; set => items[index] = value; }
        public bool IsReadOnly => false;
        public void Add(int value) => items.Add(value);
        public void Clear() => items.Clear();
        public bool Contains(int value) => items.Contains(value);
        public int IndexOf(int value) => items.IndexOf(value);
        public void Insert(int index, int value) => items.Insert(index, value);
        public bool Remove(int value) => items.Remove(value);
        public void RemoveAt(int index) => items.RemoveAt(index);
        public void CopyTo(int[] array, int index) => items.CopyTo(array, index);
        public IEnumerator<int> GetEnumerator()
        { Trace = unchecked(Trace * 10 + 2); if (Fault == 2) throw new InvalidOperationException("factory"); return new Cursor(this, ((IEnumerable<int>)items).GetEnumerator()); }
        IEnumerator IEnumerable.GetEnumerator()
        { Trace = unchecked(Trace * 10 + 3); return new Cursor(this, ((IEnumerable<int>)items).GetEnumerator()); }
        private sealed class Cursor : IEnumerator<int>
        {
            private readonly ReadOnlyTrackingList owner;
            private readonly IEnumerator<int> inner;
            public Cursor(ReadOnlyTrackingList owner, IEnumerator<int> inner) { this.owner = owner; this.inner = inner; }
            public int Current { get { owner.Trace = unchecked(owner.Trace * 10 + 5); if (owner.Fault == 3) throw new InvalidOperationException("current"); return inner.Current; } }
            object IEnumerator.Current => Current;
            public bool MoveNext() { owner.Trace = unchecked(owner.Trace * 10 + 4); if (owner.Fault == 4) throw new InvalidOperationException("move"); return inner.MoveNext(); }
            public void Reset() { owner.Trace = unchecked(owner.Trace * 10 + 7); ((IEnumerator)inner).Reset(); }
            public void Dispose() { owner.Trace = unchecked(owner.Trace * 10 + 6); inner.Dispose(); }
        }
    }

    public static class ReadOnlyEnumerationProbe
    {
        public static int Lists(int a, int b)
        {
            var list = new List<int>(); list.Add(a); list.Add(b);
            var view = new ReadOnlyCollection<int>(list);
            int result = 0;
            foreach (int value in view) result = unchecked(result * 31 + value);
            var legacy = ((IEnumerable)view).GetEnumerator();
            while (legacy.MoveNext()) result = unchecked(result * 17 + (int)legacy.Current);
            return result;
        }
        public static int Arrays(int a, int b)
        {
            var array = new[] { a, b }; var view = new ReadOnlyCollection<int>(array);
            var cursor = view.GetEnumerator(); var legacy = ((IEnumerable)view).GetEnumerator();
            int result = 0; cursor.MoveNext(); array[0] = b;
            result = unchecked(result + cursor.Current);
            while (legacy.MoveNext()) result = unchecked(result * 13 + (int)legacy.Current);
            cursor.Dispose(); return result;
        }
        public static int Mutation(int a, int b)
        {
            var list = new List<int>(); list.Add(a); list.Add(b);
            var view = new ReadOnlyCollection<int>(list); var cursor = view.GetEnumerator();
            cursor.MoveNext(); list[0] = b; int result = cursor.Current;
            try { cursor.MoveNext(); return -100; }
            catch (InvalidOperationException e) { result = unchecked(result + (e.Message == "Collection was modified; enumeration operation may not execute." ? 7 : -101)); }
            try { ((IEnumerator)cursor).Reset(); return -102; }
            catch (InvalidOperationException) { return unchecked(result + 11); }
        }
        public static int References(int a, int b)
        {
            var node = new ValueVectorBox(a); var list = new List<ValueVectorBox>(); list.Add(node);
            var view = new ReadOnlyCollection<ValueVectorBox>(list); var cursor = view.GetEnumerator();
            cursor.MoveNext(); list.Clear(); node.Number = b;
            string pressure = string.Concat("read-only", "cursor");
            return unchecked(cursor.Current.Number + pressure.Length + (ReferenceEquals(((IEnumerator)cursor).Current, node) ? 7 : -100));
        }
        public static int Values(int a, int b)
        {
            var value = new ValueVectorCell(); value.Number = a; value.Reference = new ValueVectorBox(b);
            var array = new[] { value }; var view = new ReadOnlyCollection<ValueVectorCell>(array);
            var cursor = view.GetEnumerator(); cursor.MoveNext(); var copy = cursor.Current;
            copy.Number = b; copy.Reference.Number = a;
            var legacy = ((IEnumerable)view).GetEnumerator(); legacy.MoveNext(); var boxed = (ValueVectorCell)legacy.Current;
            string pressure = string.Concat("value", "copy");
            return unchecked(cursor.Current.Number + boxed.Number * 3 + boxed.Reference.Number * 5 + copy.Number * 7 + pressure.Length);
        }
        public static int Empty(int a, int b)
        {
            var list = new ReadOnlyTrackingList(0, a, 0); var view = new ReadOnlyCollection<int>(list);
            var first = view.GetEnumerator(); var second = view.GetEnumerator();
            var array = ((IEnumerable<int>)new int[0]).GetEnumerator();
            int result = list.Trace + (ReferenceEquals(first, second) && ReferenceEquals(first, array) ? 7 : -100);
            ((IEnumerator)first).Reset();
            try { result += first.Current; } catch (InvalidOperationException e) { result += e.Message == "Enumeration has not started. Call MoveNext." ? 3 : -101; }
            list.Add(a); if (first.MoveNext()) return -102;
            try { result += first.Current; } catch (InvalidOperationException e) { result += e.Message == "Enumeration already finished." ? 5 : -103; }
            first.Dispose(); var later = view.GetEnumerator(); later.MoveNext();
            return unchecked(result + later.Current + list.Trace + b);
        }
        public static int Forwarding(int a, int b)
        {
            var source = new ReadOnlyTrackingList(1, a, 0); var view = new ReadOnlyCollection<int>(source);
            var cursor = view.GetEnumerator(); cursor.MoveNext(); int result = cursor.Current;
            cursor.Dispose(); result = unchecked(result + source.Trace);
            source.Trace = 0; var legacy = ((IEnumerable)view).GetEnumerator();
            legacy.MoveNext(); result = unchecked(result + (int)legacy.Current); legacy.Reset(); ((IDisposable)legacy).Dispose();
            return unchecked(result + source.Trace + b);
        }
        public static int Faults(int a, int b)
        {
            int fault = (b & 3) + 1; var source = new ReadOnlyTrackingList(1, a, fault); var view = new ReadOnlyCollection<int>(source);
            int result = 0;
            try { foreach (int value in view) result = unchecked(result + value); }
            catch (InvalidOperationException e)
            { result = unchecked(result + (e.Message == "count" ? 11 : e.Message == "factory" ? 13 : e.Message == "current" ? 17 : e.Message == "move" ? 19 : -100)); }
            return unchecked(result + source.Trace);
        }
        public static int LegacyEmpty(int a, int b)
        {
            var source = new ReadOnlyTrackingList(0, a, 1); var view = new ReadOnlyCollection<int>(source);
            var cursor = ((IEnumerable)view).GetEnumerator();
            if (cursor.MoveNext()) return -100;
            ((IDisposable)cursor).Dispose(); return unchecked(source.Trace + b);
        }
    }
}
