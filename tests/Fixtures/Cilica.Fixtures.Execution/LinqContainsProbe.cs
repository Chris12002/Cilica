using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class LinqContainsProbe
    {
        private static int[] Values(int a, int b) { var values = new int[3]; values[0] = a; values[1] = b; values[2] = a; return values; }
        public static int Integers(int a, int b)
        { var values = Values(a, b); return (values.Contains(a) ? 1 : 0) + (values.Contains(unchecked(a + 1)) ? 3 : 0) + (new int[0].Contains(b) ? 7 : 0); }
        public static int Strings(int a, int b)
        {
            string[] values = new string[4]; values[0] = null!; values[1] = "A\0\ud800"; values[2] = string.Concat("ab", "c"); values[3] = "\ud83d\ude00";
            string search = a < 0 ? null! : (b & 3) == 0 ? "A\0\ud800" : (b & 3) == 1 ? "abc" : (b & 3) == 2 ? "\ud83d\ude00" : "A";
            return values.Contains(search) ? 13 : 17;
        }
        public static int Null(int a, int b)
        {
            IEnumerable<string> source = a < 0 ? null! : new string[0];
            try { return source.Contains(null!) ? a : b; } catch (ArgumentNullException e) { return e.ParamName.Length + e.Message.Length; }
        }
        public static int Source(int a, int b)
        {
            var trace = new OrderingProbe.Trace(); var source = new OrderingProbe.Source(trace, a & 7);
            bool found = source.Contains(b); return unchecked(trace.Value + (found ? 19 : 23));
        }
        public static int SourceFault(int a, int b)
        {
            var trace = new OrderingProbe.Trace(); int mode = b & 3; trace.Fail = mode == 0 ? 11 : mode == 1 ? 13 : mode == 2 ? 17 : 19;
            try { return new OrderingProbe.Source(trace, a & 7).Contains(a) ? 1 : 0; }
            catch (Exception e) { return unchecked(trace.Value + (e == trace.Fault ? 29 : 31)); }
        }
        public sealed class Collection : ICollection<int>
        {
            private readonly OrderingProbe.Trace trace; private readonly int identity; private readonly int accepted;
            public Collection(OrderingProbe.Trace trace, int identity, int accepted) { this.trace = trace; this.identity = identity; this.accepted = accepted; }
            public int Count { get { trace.Add(100 + identity); return 3; } }
            public bool IsReadOnly => true;
            public bool Contains(int value) { trace.Add(identity); if (trace.Fail == identity) throw trace.Fault; return value == accepted; }
            public IEnumerator<int> GetEnumerator() { trace.Add(200 + identity); return new OrderingProbe.Source(trace, 3); }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public void CopyTo(int[] array, int index) { throw new NotSupportedException(); }
            public void Add(int value) { throw new NotSupportedException(); }
            public void Clear() { throw new NotSupportedException(); }
            public bool Remove(int value) { throw new NotSupportedException(); }
        }
        public static int CollectionEnumerable(int a, int b)
        {
            var trace = new OrderingProbe.Trace(); trace.Fail = a < 0 ? 7 : 0;
            IEnumerable<int> source = new Collection(trace, 7, a);
            try { return unchecked((source.Contains(b) ? 37 : 41) + trace.Value); }
            catch (Exception e) { return unchecked(trace.Value + (e == trace.Fault ? 43 : 47)); }
        }
        public static int Concat(int a, int b)
        {
            var trace = new OrderingProbe.Trace(); trace.Fail = a < 0 ? (b & 7) : 0;
            var query = new Collection(trace, 1, 1).Concat(new Collection(trace, 2, 2));
            if ((a & 1) != 0) query = query.Concat(new Collection(trace, 3, 3)).Concat(new Collection(trace, 4, 4));
            try { bool result = query.Contains(b); return unchecked(trace.Value + (result ? 53 : 59) + (query.GetEnumerator() == query ? 61 : 67)); }
            catch (Exception e) { return unchecked(trace.Value + (e == trace.Fault ? 71 : 73)); }
        }
        public static int Ordered(int a, int b)
        {
            var trace = new OrderingProbe.Trace(); trace.Fail = 1;
            var query = new OrderingProbe.Source(trace, a & 7).OrderBy(trace.Key);
            bool result = query.Contains(b); return unchecked(trace.Value + (result ? 79 : 83) + (query.GetEnumerator() == query ? 89 : 97));
        }
        public struct HashBomb : IEquatable<HashBomb>
        {
            public int Number;
            public bool Equals(HashBomb other) => Number == other.Number;
            public override int GetHashCode() { throw new FormatException("hash must not run"); }
        }
        public static int Distinct(int a, int b)
        {
            var values = new HashBomb[2]; values[0] = new HashBomb { Number = a }; values[1] = new HashBomb { Number = b };
            var query = values.Distinct(); bool result = query.Contains(new HashBomb { Number = unchecked(b + 1) });
            return (result ? 101 : 103) + (query.GetEnumerator() == query ? 107 : 109);
        }
        public static int ProjectionCursor(int a, int b)
        {
            var trace = new OrderingProbe.Trace(); var values = Values(a, b); IEnumerable<int> query;
            int mode = a & 3;
            if (mode == 0) query = values.Select(trace.Project);
            else if (mode == 1) query = values.Where(x => x != 0).Select(trace.Project);
            else if (mode == 2) query = values.Select(trace.Project).Select(trace.Project);
            else query = values.Where(x => x != 0).Select(trace.Project).Select(trace.Project);
            bool result = query.Contains(unchecked(b + 1)); var e = query.GetEnumerator();
            int status = e == query ? 113 : 127; if (e.MoveNext()) status = unchecked(status + e.Current); e.Dispose();
            return unchecked(trace.Value + status + (result ? 131 : 137));
        }
        public static int GeneralProjectionCursor(int a, int b)
        {
            var trace = new OrderingProbe.Trace(); IEnumerable<int> source = new OrderingProbe.Source(trace, a & 7);
            if (b < 0) source = source.Concat(Values(a, b));
            var query = source.Select(trace.Project).Select(trace.Project);
            bool result = query.Contains(b); var e = query.GetEnumerator();
            int status = e == query ? 139 : 149; if (e.MoveNext()) status = unchecked(status + e.Current); e.Dispose();
            return unchecked(trace.Value + status + (result ? 151 : 157));
        }
        public static int ActiveProjection(int a, int b)
        {
            var trace = new OrderingProbe.Trace(); var query = Values(a, b).Where(x => x != 0).Select(trace.Project);
            var e = query.GetEnumerator(); e.MoveNext(); int before = e.Current;
            bool result = query.Contains(b); int after = e.Current; e.MoveNext(); int next = e.Current; e.Dispose();
            return unchecked(trace.Value + before * 7 + after * 11 + next * 17 + (result ? 163 : 167));
        }
        public static int ListProjection(int a, int b)
        {
            var source = new SelectorList(); var state = new SelectorState(a, int.MinValue);
            var query = source.Select(state.Apply).Select(state.Second); bool result = query.Contains(b);
            return unchecked(source.Reads + source.Trace * 31 + state.Trace * 17 + (result ? 173 : 179) + (query.GetEnumerator() == query ? 181 : 191));
        }
        public static int OfType(int a, int b)
        {
            object[] values = new object[4]; values[0] = "abc"; values[1] = a; values[2] = null!; values[3] = "A\0\ud800";
            var query = values.OfType<string>(); bool result = query.Contains(b < 0 ? null! : "abc");
            return (result ? 193 : 197) + (query.GetEnumerator() == query ? 199 : 211);
        }
        public static int OfTypeValueProjection(int a, int b)
        {
            object[] values = new object[4]; values[0] = "abc"; values[1] = a; values[2] = null!; values[3] = b;
            var trace = new OrderingProbe.Trace(); var query = values.OfType<int>().Select(trace.Project).Select(trace.Project);
            bool result = query.Contains(b); return unchecked(trace.Value + (result ? 223 : 227) + (query.GetEnumerator() == query ? 229 : 233));
        }
        public static int UnsupportedReferenceProjection(int a, int b)
        {
            object[] values = new object[4]; values[0] = "abc"; values[1] = a; values[2] = null!; values[3] = "A\0\ud800";
            var trace = new OrderingProbe.Trace(); var query = values.OfType<string>().Select(x => x.Length).Select(trace.Project);
            bool result = query.Contains(b); return unchecked(trace.Value + (result ? 223 : 227) + (query.GetEnumerator() == query ? 229 : 233));
        }
        public static int MutatingValues(int a, int b)
        {
            var values = new SearchCell[2]; values[0] = new SearchCell { Number = a, Payload = new ValueVectorBox(1) }; values[1] = new SearchCell { Number = b, Payload = new ValueVectorBox(2) };
            var value = new SearchCell { Number = b };
            bool result = a < 0 ? values.Where(x => x.Number != 0).Contains(value) : values.Contains(value);
            return unchecked(values[0].Number * 17 + values[1].Number * 31 + (result ? 239 : 241));
        }
        public static int AllocatingValues(int a, int b)
        {
            var values = new AllocatingSearchCell[2]; values[0] = new AllocatingSearchCell { Number = a, Payload = new ValueVectorBox(1) }; values[1] = new AllocatingSearchCell { Number = b, Payload = new ValueVectorBox(2) };
            var value = new AllocatingSearchCell { Number = b }; int result;
            try { result = (a < 0 ? values.Where(x => x.Number != 0).Contains(value) : values.Contains(value)) ? 251 : 257; }
            catch (InvalidOperationException e) { result = e.Message.Length; }
            return unchecked(result + values[0].Payload.Number * 17 + values[1].Payload.Number * 31);
        }
        public static int UnsupportedComparer(int a, int b) => new[] { "a" }.Contains("a", StringComparer.Ordinal) ? a : b;
        public static int UnsupportedNullable(int a, int b) => new int?[] { a, null }.Contains(b) ? a : b;
    }
}
