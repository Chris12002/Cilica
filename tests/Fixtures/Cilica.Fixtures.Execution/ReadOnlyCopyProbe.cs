using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Cilica.Fixtures.Execution
{
    public sealed class CopyCallbackList : IList<int>
    {
        public int Trace;
        public int Value;
        public bool Fail;
        public int Count { get { Trace = unchecked(Trace * 10 + 1); throw new InvalidOperationException("count"); } }
        public int this[int index] { get => throw new InvalidOperationException("index"); set => throw new NotSupportedException(); }
        public bool IsReadOnly => false;
        public void CopyTo(int[] array, int index)
        {
            Trace = unchecked(Trace * 10 + 7);
            if (array != null && index >= 0 && index < array.Length) array[index] = Value;
            if (Fail) throw new InvalidOperationException("copy");
        }
        public void Add(int value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(int value) => throw new NotSupportedException();
        public int IndexOf(int value) => throw new NotSupportedException();
        public void Insert(int index, int value) => throw new NotSupportedException();
        public bool Remove(int value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("enumerator");
        IEnumerator IEnumerable.GetEnumerator() => throw new InvalidOperationException("legacy");
    }
    public static class ReadOnlyCopyProbe
    {
        private static int Hash(string text) { if (text == null) return 0; int result = 17; for (int i = 0; i < text.Length; i++) result = unchecked(result * 31 + text[i]); return result; }
        public static int Lists(int a, int b)
        {
            var source = new List<int>(); source.Add(a); var view = new ReadOnlyCollection<int>(source);
            source.Add(b); var target = new int[4]; view.CopyTo(target, 1); source[0] = b;
            return unchecked(target[1] * 31 + target[2] + view[0]);
        }
        public static int Arrays(int a, int b)
        {
            var source = new[] { a, b }; var view = new ReadOnlyCollection<int>(source);
            view.CopyTo(source, 0); var target = new int[3]; ((ICollection<int>)view).CopyTo(target, 1);
            return unchecked(target[1] * 31 + target[2]);
        }
        public static int Faults(int a, int b)
        {
            var list = new List<int>(); if ((b & 1) != 0) list.Add(a);
            var view = new ReadOnlyCollection<int>(list); var target = b < 0 ? null : new int[2];
            try { view.CopyTo(target, a); return 3; }
            catch (ArgumentException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int Values(int a, int b)
        {
            ValueVectorCell value = default; value.Number = a; value.Wide = ((long)a << 32) + b; value.Reference = new ValueVectorBox(b);
            var source = new[] { value }; var view = new ReadOnlyCollection<ValueVectorCell>(source); var target = new ValueVectorCell[2];
            view.CopyTo(target, 1); source[0] = default; value.Reference.Number = a;
            string pressure = string.Concat("copy", "root");
            return unchecked(target[1].Number + (int)(target[1].Wide >> 32) + target[1].Reference.Number + pressure.Length);
        }
        public static int References(int a, int b)
        {
            var node = new ValueVectorBox(a); var source = new List<ValueVectorBox>(); source.Add(node);
            var view = new ReadOnlyCollection<ValueVectorBox>(source); var target = new ValueVectorBox[2]; view.CopyTo(target, 1);
            source.Clear(); node.Number = b; string pressure = string.Concat("reference", "copy");
            return unchecked(target[1].Number + pressure.Length + (ReferenceEquals(node, target[1]) ? 7 : -100));
        }
        public static int Covariant(int a, int b)
        {
            var source = new List<object>(); source.Add("first"); source.Add(b < 0 ? new object() : "second");
            var view = new ReadOnlyCollection<object>(source); var target = new string[3]; int result = 0;
            try { view.CopyTo(target, a & 1); result = 3; }
            catch (InvalidCastException e) { result = Hash(e.Message); }
            catch (ArgumentException e) { result = Hash(e.Message); }
            for (int i = 0; i < target.Length; i++) result = unchecked(result * 7 + (target[i] == null ? 0 : target[i].Length));
            return result;
        }
        public static int Forwarding(int a, int b)
        {
            var source = new CopyCallbackList(); source.Value = a; source.Fail = (b & 1) != 0;
            var view = new ReadOnlyCollection<int>(source); var target = b < 0 ? null : new int[2]; int result = 0;
            try { view.CopyTo(target, b); result = 3; }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            if (target != null) result = unchecked(result + target[0] * 7 + target[1] * 11);
            return unchecked(result + source.Trace * 13);
        }
        public static int Construction(int a, int b)
        {
            var source = new List<int>(); if ((a & 1) != 0) { source.Add(a); source.Add(b); }
            var view = new ReadOnlyCollection<int>(source); var snapshot = new List<int>(view); source.Clear();
            int result = snapshot.Count; for (int i = 0; i < snapshot.Count; i++) result = unchecked(result * 31 + snapshot[i]);
            return result;
        }
        public static int UnsupportedLegacy(int a, int b)
        {
            var view = new ReadOnlyCollection<int>(new[] { a }); ((ICollection)view).CopyTo(new int[2], b); return a;
        }
    }
}
