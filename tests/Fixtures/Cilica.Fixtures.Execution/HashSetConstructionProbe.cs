using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class SetCollectionSource : ICollection<int>
    {
        public int Trace, Mode, ReportedCount, Value;
        public FirstSequence Sequence;
        public int Count { get { Trace = unchecked(Trace * 11 + 1); if (Mode == 1) throw new InvalidOperationException("count"); return ReportedCount; } }
        public IEnumerator<int> GetEnumerator() { Trace = unchecked(Trace * 11 + 2); return Sequence.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool IsReadOnly => true;
        public void Add(int value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Contains(int value) { throw new NotSupportedException(); }
        public void CopyTo(int[] values, int index) { throw new InvalidOperationException("must enumerate"); }
        public bool Remove(int value) { throw new NotSupportedException(); }
    }
    public sealed class SetYieldSource
    {
        public int Trace;
        public IEnumerable<CollisionKey> Values(int value, KeyTrace trace)
        {
            try
            {
                for (int i = 0; i < 12; i++) { Trace++; yield return new CollisionKey(unchecked(value + i % 3), trace); }
            }
            finally { Trace = unchecked(Trace * 31 + 7); }
        }
    }
    public static class HashSetConstructionProbe
    {
        private static int Hash(string text) { int h = 17; for (int i = 0; i < text.Length; i++) h = unchecked(h * 31 + text[i]); return h; }
        public static int Arrays(int a, int b)
        {
            var source = new int[24]; for (int i = 0; i < source.Length; i++) source[i] = unchecked(a + i % 3);
            var set = new HashSet<int>(source); source[0] = b;
            int result = set.Count + (set.Contains(a) ? 11 : 13);
            set.Remove(a); set.Add(b); return unchecked(result + set.Count * 17 + (set.Contains(b) ? 19 : 23));
        }
        private static IEnumerable<long> WideSource(long value)
        { yield return value; yield return value; yield return ~value; yield return 0L; }
        public static int Wide(int a, int b)
        {
            long x = ((long)a << 32) | (uint)b;
            var set = new HashSet<long>(WideSource(x));
            set.Remove(x); set.Add(x); return set.Count + (set.Contains(x) ? 7 : -101) + (set.Contains(~x) ? 11 : -103);
        }
        public static int WideValues(int a, int b)
        {
            long x = ((long)a << 32) | (uint)b;
            var key = new ValueTuple<long, int>(x, b);
            var set = new HashSet<ValueTuple<long, int>>(new[] { key, key, new ValueTuple<long, int>(~x, a) });
            return set.Count + (set.Contains(key) ? 7 : -101);
        }
        public static int Strings(int a, int b)
        {
            string first = string.Concat("a", "b"), equal = string.Concat("", "ab");
            var set = new HashSet<string>(new[] { first, null, equal, null });
            bool found = set.TryGetValue(equal, out var actual);
            return set.Count + (found && (object)actual == first ? 7 : -101) + (set.Contains(null) ? 11 : -103);
        }
        public static int EmptyNull(int a, int b)
        {
            var empty = ((IEnumerable<int>)new int[0]).GetEnumerator(); empty.Dispose();
            IEnumerable<int> source = a < 0 ? null : new int[0];
            try { return new HashSet<int>(source).Count; }
            catch (ArgumentNullException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int Collection(int a, int b)
        {
            var sequence = new FirstSequence(b & 7, a == 0 ? 0 : 7, a);
            var source = new SetCollectionSource { Sequence = sequence, Mode = b < 0 ? 1 : 0, ReportedCount = a < 0 ? -1 : a == 0 ? 0 : 37 };
            int result;
            try { var set = new HashSet<int>(source); result = set.Count + (set.Contains(a) ? 7 : 11); }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + source.Trace * 37 + sequence.Trace * 53);
        }
        public static int Fallback(int a, int b)
        {
            var source = new FirstSequence(b & 7, a == 0 ? 0 : 9, a); int result;
            try { result = new HashSet<int>(source).Count; }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + source.Trace * 37);
        }
        public static int Yield(int a, int b)
        {
            var source = new SetYieldSource(); var trace = new KeyTrace();
            trace.HashFailure = b == 1 ? 1 : 0; trace.EqualityFailure = b == 2 ? 1 : 0; int result;
            try { result = new HashSet<CollisionKey>(source.Values(a, trace)).Count; }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + trace.Hashes * 37 + trace.Equalities * 53 + source.Trace * 71);
        }
        public static int Copy(int a, int b)
        {
            var source = new HashSet<int>(); int count = (b & 31) + 1;
            for (int i = 0; i < count; i++) source.Add(unchecked(a + i));
            for (int i = 1; i < count; i++) if ((b & 1) == 0 || (i & 1) == 0) source.Remove(unchecked(a + i));
            IEnumerable<int> alias = source; var copied = new HashSet<int>(alias);
            int result = copied.Count;
            for (int i = 0; i < count; i++) result = unchecked(result * 7 + (copied.Contains(unchecked(a + i)) ? 1 : 0));
            source.Clear(); copied.Add(b); return unchecked(result + copied.Count * 37 + (copied.Contains(a) ? 11 : 13));
        }
        public static int TrimKeys(int a, int b)
        {
            var trace = new KeyTrace(); var values = new CollisionKey[37];
            for (int i = 0; i < values.Length; i++) values[i] = new CollisionKey(unchecked(a + i % 3), trace);
            var set = new HashSet<CollisionKey>(values); int result = set.Count;
            foreach (var key in set) result = unchecked(result * 7 + key.Number);
            trace.HashFailure = 1; var copy = new HashSet<CollisionKey>(set);
            foreach (var key in copy) result = unchecked(result * 11 + key.Number);
            return unchecked(result + trace.Hashes * 37 + trace.Equalities * 53);
        }
        public static int CopyCallbacks(int a, int b)
        {
            var trace = new KeyTrace(); var source = new HashSet<CollisionKey>(); int count = (a & 31) + 1;
            for (int i = 0; i < count; i++) source.Add(new CollisionKey(i, trace));
            if (b < 0) source.Clear();
            else if ((b & 1) == 0) for (int i = 1; i < count; i++) source.Remove(new CollisionKey(i, trace));
            trace.Hashes = 0; trace.Equalities = 0;
            trace.HashFailure = b == 4 || b == 5 ? 1 : 0;
            trace.EqualityFailure = b == 6 || b == 7 ? 1 : 0; int result;
            try { result = new HashSet<CollisionKey>(source).Count; }
            catch (InvalidOperationException e) { result = Hash(e.Message); }
            return unchecked(result + source.Count * 11 + trace.Hashes * 37 + trace.Equalities * 53);
        }
    }
}
