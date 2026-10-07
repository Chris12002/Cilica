using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class RangeCollection : ICollection<int>
    {
        public List<int> Target; public int Mode, Trace, First, Second; public int[] Captured;
        public int Count
        {
            get
            {
                Trace = unchecked(Trace * 7 + 1);
                if (Mode == 1) throw new InvalidOperationException("count");
                if (Mode == 2) return 0;
                if (Mode == 3) return -1;
                if (Mode == 4) return 0x7fffffff;
                if (Mode == 6) Target.Add(71);
                if (Mode == 7) Target.Clear();
                return 2;
            }
        }
        public bool IsReadOnly => true;
        public void CopyTo(int[] array, int index)
        {
            Trace = unchecked(Trace * 7 + 2); Captured = array;
            if (Mode == 8) { Target.Capacity = 16; Target.Add(23); }
            if (Mode == 9) Target.Clear();
            if (Mode == 10) Target.RemoveAt(0);
            if (Mode == 11) return;
            array[index] = First;
            if (Mode == 5) throw new InvalidOperationException("copy");
            if (Mode == 12) array[array.Length] = 99;
            array[index + 1] = Second;
        }
        public IEnumerator<int> GetEnumerator() { throw new NotSupportedException("enumerated"); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(int item) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int item) { throw new NotSupportedException(); }
        public bool Remove(int item) { throw new NotSupportedException(); }
    }
    public static class ListAddRangeProbe
    {
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        private static int Read(List<int> list) { int hash = list.Count * 13 + list.Capacity; for (int i = 0; i < list.Count; i++) hash = unchecked(hash * 31 + list[i]); return hash; }
        public static int Arrays(int input, int mode)
        {
            var list = new List<int>(); for (int i = 0; i < (mode & 3); i++) list.Add(unchecked(input + i));
            var cursor = list.GetEnumerator(); var source = new int[input & 15]; for (int i = 0; i < source.Length; i++) source[i] = unchecked(mode + i);
            list.AddRange(source); int result = Read(list);
            try { if (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidOperationException) { result = unchecked(result + 97); }
            return result;
        }
        public static int Self(int input, int mode)
        {
            var list = new List<int>(); for (int i = 0; i < (input & 31); i++) list.Add(unchecked(mode + i));
            var cursor = list.GetEnumerator(); list.AddRange(list); list.AddRange((IEnumerable<int>)list);
            int result = Read(list);
            try { if (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidOperationException) { result = unchecked(result + 19); }
            return result;
        }
        public static int CollectionCallbacks(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode); var cursor = list.GetEnumerator(); cursor.MoveNext();
            var source = new RangeCollection(); source.Mode = (int)((uint)input % 13); source.Target = list; source.First = input; source.Second = mode;
            int result = 0;
            try { list.AddRange(source); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            catch (OverflowException) { result = 43; }
            catch (IndexOutOfRangeException) { result = 59; }
            result = unchecked(result + Read(list) + source.Trace * 17);
            if (source.Captured != null) for (int i = 0; i < source.Captured.Length; i++) result = unchecked(result * 7 + source.Captured[i]);
            try { if (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidOperationException) { result = unchecked(result + 101); }
            return result;
        }
        public static int EnumerableFaults(int input, int mode)
        {
            var list = new List<int>(); list.Add(17); var cursor = list.GetEnumerator();
            var source = new ConstructionSequence<int>(); source.Data = new[] { input, mode, 23 }; source.Mode = (int)((uint)mode % 7); source.FaultIndex = input & 3;
            int result = 0;
            try { list.AddRange(source); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            catch (NullReferenceException) { result = 131; }
            try { if (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidOperationException) { result = unchecked(result + 197); }
            return unchecked(result + Read(list) + source.Trace);
        }
        private static IEnumerable<int> Echo(IEnumerable<int> source) { foreach (int item in source) yield return item; }
        public static int WrappedSelf(int input, int mode)
        {
            var list = new List<int>(); for (int i = 0; i < (input & 3); i++) list.Add(unchecked(mode + i)); int result = 0;
            try { list.AddRange(Echo(list)); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            return unchecked(result + Read(list));
        }
        public static int References(int input, int mode)
        {
            var list = new List<ValueVectorBox>(); var node = new ValueVectorBox(input); list.Add(node);
            var source = new ConstructionSequence<ValueVectorBox>(); source.Data = new[] { node, null, new ValueVectorBox(mode) };
            list.AddRange(source); source.Data[0] = null; node.Number = mode;
            return unchecked(list[1].Number + list[3].Number + list.Count + list.Capacity + (list[0] == list[1] && list[2] == null ? 17 : -100));
        }
        public static int Values(int input, int mode)
        {
            var list = new List<ValueVectorCell>(); ValueVectorCell item = default; item.Number = input; item.Wide = (long)mode << 32; item.Reference = new ValueVectorBox(mode); list.Add(item);
            var source = new ConstructionSequence<ValueVectorCell>(); source.Data = new[] { item, item }; list.AddRange(source); source.Data[0] = default;
            return unchecked(list[1].Number + (int)(list[2].Wide >> 32) + list[1].Reference.Number + list.Count + list.Capacity);
        }
        public static int Null(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); var cursor = list.GetEnumerator(); int result = 0;
            try { list.AddRange(null); }
            catch (ArgumentNullException error) { result = unchecked(Hash(error.Message) + Hash(error.ParamName)); }
            cursor.MoveNext(); return unchecked(result + cursor.Current + Read(list));
        }
    }
}
