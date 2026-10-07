using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public class TypeItem { public int Number; public TypeItem(int number) { Number = number; } }
    public sealed class DerivedTypeItem : TypeItem { public DerivedTypeItem(int number) : base(number) { } }
    public struct TypeValue { public int Number; public ValueVectorBox Reference; }
    public class TypeSequence : IEnumerable
    {
        public object First, Second, Third;
        public int Trace, Mode;
        public TypeSequence(object first, object second, object third, int mode) { First = first; Second = second; Third = third; Mode = mode; }
        public IEnumerator GetEnumerator()
        {
            Trace = unchecked(Trace * 7 + 1); if (Mode == 3) throw new InvalidOperationException("get");
            if (Mode == 4) return new TypeCursor(this); return new DisposableTypeCursor(this);
        }
    }
    public class TypeCursor : IEnumerator
    {
        public TypeSequence source;
        private int index;
        public TypeCursor(TypeSequence source) { this.source = source; }
        public bool MoveNext() { source.Trace = unchecked(source.Trace * 7 + 2); if (source.Mode == 5) throw new InvalidOperationException("move"); return ++index <= 3; }
        public object Current { get { source.Trace = unchecked(source.Trace * 7 + 3); if (source.Mode == 1) throw new InvalidOperationException("current"); return index == 1 ? source.First : index == 2 ? source.Second : source.Third; } }
        public void Reset() { index = 0; }
    }
    public sealed class DisposableTypeCursor : TypeCursor, IDisposable
    {
        public DisposableTypeCursor(TypeSequence source) : base(source) { }
        public void Dispose() { source.Trace = unchecked(source.Trace * 7 + 4); if (source.Mode == 2) throw new InvalidOperationException("dispose"); }
    }
    public sealed class DualTypeSequence : IEnumerable<object>
    {
        public object Item;
        public int Trace;
        public DualTypeSequence(object item) { Item = item; }
        public IEnumerator<object> GetEnumerator() { Trace = unchecked(Trace * 7 + 1); return new DualTypeCursor(this); }
        IEnumerator IEnumerable.GetEnumerator() { Trace = unchecked(Trace * 7 + 2); return new DualTypeCursor(this); }
    }
    public sealed class DualTypeCursor : IEnumerator<object>
    {
        private DualTypeSequence source;
        private int index;
        public DualTypeCursor(DualTypeSequence source) { this.source = source; }
        public bool MoveNext() { source.Trace = unchecked(source.Trace * 7 + 3); return index++ == 0; }
        public object Current { get { source.Trace = unchecked(source.Trace * 7 + 4); return source.Item; } }
        object IEnumerator.Current { get { source.Trace = unchecked(source.Trace * 7 + 5); return source.Item; } }
        public void Dispose() { source.Trace = unchecked(source.Trace * 7 + 6); }
        public void Reset() { index = 0; }
    }
    public sealed class TypeSelector
    {
        public int Calls;
        public int Item(TypeItem item) { Calls++; return item.Number; }
        public int Nullable(int? value) { Calls++; return value.GetValueOrDefault(17); }
        public int Value(TypeValue value) { Calls++; return value.Number + value.Reference.Number; }
    }
    public static class LinqOfTypeProbe
    {
        public static int References(int a, int b)
        {
            var source = new TypeSequence(new TypeItem(a), null, new DerivedTypeItem(b), 0); var query = source.OfType<TypeItem>(); int result = 0;
            foreach (var item in query) result = unchecked(result * 7 + item.Number);
            var values = query.ToArray(); return unchecked(result + values[0].Number + values[1].Number + query.Count() + (query.Any() ? 11 : 0) + source.Trace * 37);
        }
        public static int Subtypes(int a, int b)
        {
            var source = new TypeSequence(new TypeItem(a), new DerivedTypeItem(b), "wrong", 0); var values = source.OfType<DerivedTypeItem>().ToArray();
            return unchecked(values.Length + values[0].Number + source.Trace * 37);
        }
        public static int Scalars(int a, int b)
        {
            var source = new TypeSequence(a, (long)b, b, 0); var values = source.OfType<int>().ToArray();
            return unchecked(values.Length + values[0] + values[1] + source.Trace * 37);
        }
        public static int Nullable(int a, int b)
        {
            var source = new TypeSequence(a, null, (long)b, 0); var query = source.OfType<int?>(); var values = query.ToArray();
            return unchecked(values.Length + values[0].GetValueOrDefault() + query.Count() + (query.Any() ? 7 : 0) + source.Trace * 37);
        }
        public static int Values(int a, int b)
        {
            TypeValue first = default; first.Number = a; first.Reference = new ValueVectorBox(b);
            var source = new TypeSequence(first, null, a, 0); var values = source.OfType<TypeValue>().ToArray();
            return unchecked(values.Length + values[0].Number + values[0].Reference.Number + source.Trace * 37);
        }
        public static int TypedShortcut(int a, int b)
        {
            var source = new PredicateSequence(0); var query = source.OfType<int>();
            if ((object)query != source) return -100; int result = query.Count(); return unchecked(result + source.Trace * 37);
        }
        public static int Identity(int a, int b)
        {
            var source = new TypeSequence(null, new TypeItem(a), new TypeItem(b), 0); var query = source.OfType<TypeItem>();
            if (source.Trace != 0) return -100; var e = query.GetEnumerator(); var other = query.GetEnumerator();
            if ((object)e != query || e == other || source.Trace != 0 || e.Current != null) return -101;
            e.MoveNext(); var saved = e.Current; int result = query.Count() + query.ToArray().Length + (query.Any() ? 7 : 0);
            if (saved != e.Current) return -102; other.Dispose(); e.Dispose(); if (e.Current != null || e.MoveNext()) return -103;
            return unchecked(result + saved.Number + source.Trace * 37);
        }
        public static int Unstarted(int a, int b)
        {
            var source = new TypeSequence(null, new TypeItem(a), "skip", 0); var query = source.OfType<TypeItem>();
            int result = query.Count() + query.ToArray().Length + (query.Any() ? 7 : 0);
            var e = query.GetEnumerator(); if ((object)e != query) return -100; e.Dispose(); return unchecked(result + source.Trace * 37);
        }
        public static int NullAndReset(int a, int b)
        {
            int result = 0; try { ((IEnumerable)null).OfType<TypeItem>(); } catch (ArgumentNullException e) { result = e.ParamName == "source" ? 7 : -100; }
            var source = new TypeSequence(null, null, null, 0); var query = source.OfType<TypeItem>();
            result += query.Count() + query.ToArray().Length + (query.Any() ? 100 : 0);
            var e2 = query.GetEnumerator(); try { ((IEnumerator)e2).Reset(); return -101; } catch (NotSupportedException) { result += 11; } e2.Dispose();
            return unchecked(result + source.Trace * 37);
        }
        public static int Faults(int a, int b)
        {
            var source = new TypeSequence(new TypeItem(a), null, new TypeItem(b), (b & 7) % 6); var query = source.OfType<TypeItem>(); int result = 0;
            try { if (a < 0) result = query.Count(); else if (a == 0) result = query.ToArray().Length; else if (a == 1) result = query.Any() ? 7 : 11; else foreach (var value in query) result = unchecked(result + value.Number); }
            catch (InvalidOperationException e) { result = e.Message == "get" ? 13 : e.Message == "move" ? 17 : e.Message == "current" ? 19 : e.Message == "dispose" ? 23 : -100; }
            return unchecked(result + source.Trace * 37);
        }
        public static int ReferenceProjection(int a, int b)
        {
            var source = new DualTypeSequence(new TypeItem(a)); var selector = new TypeSelector(); var query = source.OfType<TypeItem>();
            var values = query.Select(selector.Item).ToArray(); var e = query.GetEnumerator(); bool reused = (object)e == query; e.Dispose();
            return unchecked(values[0] + source.Trace * 37 + selector.Calls * 53 + (reused ? 71 : 97));
        }
        public static int NullableProjection(int a, int b)
        {
            var source = new DualTypeSequence(a); var selector = new TypeSelector(); var query = source.OfType<int?>();
            var values = query.Select(selector.Nullable).ToArray(); var e = query.GetEnumerator(); bool reused = (object)e == query; e.Dispose();
            return unchecked(values[0] + source.Trace * 37 + selector.Calls * 53 + (reused ? 71 : 97));
        }
        public static int NongenericProjection(int a, int b)
        {
            var source = new TypeSequence(new TypeItem(a), null, new TypeItem(b), 0); var selector = new TypeSelector(); var query = source.OfType<TypeItem>();
            var values = query.Select(selector.Item).ToArray(); var e = query.GetEnumerator(); bool reused = (object)e == query; e.Dispose();
            return unchecked(values[0] + values[1] + source.Trace * 37 + selector.Calls * 53 + (reused ? 71 : 97));
        }
        public static int ValueProjection(int a, int b)
        {
            TypeValue item = default; item.Number = a; item.Reference = new ValueVectorBox(b);
            var source = new DualTypeSequence(item); var selector = new TypeSelector(); var query = source.OfType<TypeValue>();
            var values = query.Select(selector.Value).ToArray(); var e = query.GetEnumerator(); bool reused = (object)e == query; e.Dispose();
            return unchecked(values[0] + source.Trace * 37 + selector.Calls * 53 + (reused ? 71 : 97));
        }
        public static int ReferenceCurrent(int a, int b)
        {
            var source = new TypeSequence(new TypeItem(a), null, null, 0); var e = source.OfType<TypeItem>().GetEnumerator(); e.MoveNext(); var saved = e.Current;
            source.First = new TypeItem(b); if (e.Current != saved) return -100;
            e.Dispose(); if (e.Current != null) return -101; return unchecked(saved.Number + source.Trace * 37);
        }
        public static int ListOfType(int a, int b) { return new List<TypeItem>().OfType<TypeItem>().Count(); }
        public static int ArraySource(int a, int b) { return new object[] { new TypeItem(a) }.OfType<TypeItem>().Count(); }
    }
}
