using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class ConstructionSequence<T> : IEnumerable<T>
    {
        public T[] Data; public int Trace, Mode, FaultIndex;
        public IEnumerator<T> GetEnumerator()
        {
            Trace = unchecked(Trace * 7 + 1);
            if (Mode == 1) throw new InvalidOperationException("get");
            if (Mode == 6) return null;
            return new Cursor(this);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public struct Cursor : IEnumerator<T>
        {
            private readonly ConstructionSequence<T> owner; private int index;
            public Cursor(ConstructionSequence<T> owner) { this.owner = owner; index = -1; }
            public bool MoveNext()
            {
                owner.Trace = unchecked(owner.Trace * 7 + 2); index++;
                var pressure = new ValueVectorBox(index); owner.Trace = unchecked(owner.Trace + pressure.Number - index);
                if ((owner.Mode == 2 || owner.Mode == 5) && index == owner.FaultIndex) throw new InvalidOperationException("move");
                return index < owner.Data.Length;
            }
            public T Current
            {
                get
                {
                    owner.Trace = unchecked(owner.Trace * 7 + 3);
                    if (owner.Mode == 3 && index == owner.FaultIndex) throw new InvalidOperationException("current");
                    return owner.Data[index];
                }
            }
            object IEnumerator.Current => Current;
            public void Dispose() { owner.Trace = unchecked(owner.Trace * 7 + 5); if (owner.Mode == 4 || owner.Mode == 5) throw new InvalidOperationException("dispose"); }
            public void Reset() { throw new NotSupportedException(); }
        }
    }
    public sealed class ConstructionTrace { public int Value; }
    public static class ListEnumerableConstructionProbe
    {
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Integers(int input, int mode)
        {
            var source = new ConstructionSequence<int>(); source.Data = new int[input & 31];
            for (int i = 0; i < source.Data.Length; i++) source.Data[i] = unchecked(i + mode);
            var copy = new List<int>(source); int result = copy.Capacity + copy.Count;
            for (int i = 0; i < copy.Count; i++) result = unchecked(result * 31 + copy[i]);
            return unchecked(result + source.Trace);
        }
        public static int Faults(int input, int mode)
        {
            var source = new ConstructionSequence<int>(); source.Data = new[] { input, mode, 19 };
            source.Mode = (int)((uint)mode % 7); source.FaultIndex = input & 3; int result;
            try { var copy = new List<int>(source); result = copy.Count + copy.Capacity; }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            catch (NullReferenceException) { result = 101; }
            return unchecked(result * 31 + source.Trace);
        }
        public static int References(int input, int mode)
        {
            var source = new ConstructionSequence<ValueVectorBox>(); var item = new ValueVectorBox(input); source.Data = new[] { item, null, new ValueVectorBox(mode) };
            var copy = new List<ValueVectorBox>(source); source.Data[0] = null; item.Number = mode;
            return unchecked(copy[0].Number + copy[2].Number + (copy[0] == item && copy[1] == null ? 19 : -100) + source.Trace + copy.Capacity);
        }
        public static int Values(int input, int mode)
        {
            var source = new ConstructionSequence<ValueVectorCell>(); ValueVectorCell item = default; item.Number = input; item.Wide = (long)mode << 32; item.Reference = new ValueVectorBox(mode);
            source.Data = new[] { item, item }; var copy = new List<ValueVectorCell>(source); source.Data[0] = default; item.Number = mode;
            return unchecked(copy[0].Number + (int)(copy[1].Wide >> 32) + copy[0].Reference.Number + source.Trace + copy.Capacity);
        }
        public static int Null(int input, int mode)
        {
            IEnumerable<int> source = null;
            try { return new List<int>(source).Count; }
            catch (ArgumentNullException error) { return unchecked(Hash(error.Message) + Hash(error.ParamName)); }
        }
        private static IEnumerable<int> Iterate(int count, int value, ConstructionTrace trace)
        {
            try { for (int i = 0; i < count; i++) { trace.Value = unchecked(trace.Value * 7 + 2); yield return unchecked(value + i); } }
            finally { trace.Value = unchecked(trace.Value * 7 + 5); }
        }
        public static int Yield(int input, int mode)
        {
            var trace = new ConstructionTrace(); var copy = new List<int>(Iterate(input & 31, mode, trace));
            int result = copy.Count + copy.Capacity; foreach (int item in copy) result = unchecked(result * 13 + item);
            return unchecked(result + trace.Value);
        }
        public static int RuntimeCollection(int input, int mode)
        {
            var source = new ConstructionCollection(); source.Mode = (int)((uint)input % 5); source.First = input; source.Second = mode;
            IEnumerable<int> sequence = source; int result;
            try { var copy = new List<int>(sequence); result = copy.Count + copy.Capacity; if (copy.Count != 0) result = unchecked(result + copy[0] * 3 + copy[1]); }
            catch (InvalidOperationException error) { result = Hash(error.Message); }
            catch (OverflowException error) { result = Hash(error.Message); }
            return unchecked(result * 7 + source.Reads * 13 + source.Copies);
        }
    }
}
