using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class DictionaryMutationProbe
    {
        public static int Integers(int input, int mode)
        {
            var d = new Dictionary<int, int>(); int result = d.Remove(input) ? -100 : 1;
            for (int i = 0; i < 24; i++) d.TryAdd(unchecked(input + i), i);
            for (int i = 0; i < 24; i += 2) if (!d.Remove(unchecked(input + i), out var removed) || removed != i) return -101;
            if (d.Count != 12) return -102;
            for (int i = 0; i < 24; i += 2) if (!d.TryAdd(unchecked(input + i), unchecked(mode + i))) return -103;
            for (int i = 0; i < 40; i++) d.TryAdd(unchecked(input + i), i);
            for (int i = 0; i < 40; i++) { d.TryGetValue(unchecked(input + i), out var v); result = unchecked(result * 31 + v); }
            return unchecked(result + d.Count);
        }
        public static int Clear(int input, int mode)
        {
            var d = new Dictionary<int, long>(); d.Clear();
            for (int round = 0; round < 4; round++)
            {
                for (int i = 0; i < 20; i++) d.TryAdd(i, ((long)input << 32) + mode);
                d.Remove(5); d.Remove(15); d.Clear(); d.Clear();
                if (d.Count != 0 || d.ContainsKey(0)) return -100;
            }
            d.TryAdd(input, mode); d.TryGetValue(input, out long result); return unchecked((int)result + d.Count);
        }
        public static int Strings(int input, int mode)
        {
            var d = new Dictionary<string, ValueVectorBox>();
            d.TryAdd("one", new ValueVectorBox(input)); d.TryAdd("two", new ValueVectorBox(mode));
            d.Remove(string.Concat("o", "ne"), out var removed);
            if (removed == null || d.ContainsKey("one")) return -100;
            d.TryAdd("three", removed); d.Remove("absent", out var absent);
            if (absent != null) return -101;
            d.TryGetValue("three", out var saved); d.Clear();
            return unchecked(saved.Number + removed.Number + d.Count);
        }
        public static int Values(int input, int mode)
        {
            var d = new Dictionary<long, ValueVectorCell>(); ValueVectorCell value = default;
            value.Number = input; value.Wide = ((long)input << 32) + mode; value.Reference = new ValueVectorBox(mode);
            d.TryAdd(input, value); d.Remove(input, out var removed); value.Number = mode;
            d.TryAdd(mode, value); d.Remove(unchecked((long)mode + 1), out var absent);
            if (absent.Reference != null || absent.Wide != 0 || absent.Number != 0) return -100;
            d.Clear(); return unchecked(removed.Number + (int)(removed.Wide >> 32) + removed.Reference.Number + d.Count);
        }
        public static int Collisions(int input, int mode)
        {
            var trace = new KeyTrace(); var d = new Dictionary<CollisionKey, int>();
            for (int i = 0; i < 12; i++) d.TryAdd(new CollisionKey(unchecked(input + i), trace), i);
            int result = 0;
            // Constant hashes exercise head, middle and tail links, including mutable hash receivers.
            for (int step = 0; step < 4; step++)
            {
                int offset = step == 0 ? 11 : step == 2 ? 0 : 5;
                if (d.Remove(new CollisionKey(unchecked(input + offset), trace), out var value)) result = unchecked(result + value + 1);
            }
            for (int i = 12; i < 24; i++) d.TryAdd(new CollisionKey(unchecked(input + i), trace), i);
            return unchecked(result + d.Count + trace.Hashes * 1009 + trace.Equalities * 31);
        }
        public static int CallbackFaults(int input, int mode)
        {
            var trace = new KeyTrace(); var d = new Dictionary<CollisionKey, int>();
            trace.HashFailure = 1;
            int result = d.Remove(new CollisionKey(input, trace)) ? -100 : 1; // Empty skips hashing.
            trace.HashFailure = 0; d.TryAdd(new CollisionKey(input, trace), mode);
            int value = 77; trace.HashFailure = 1;
            try { d.Remove(new CollisionKey(input, trace), out value); } catch (InvalidOperationException) { result += value; }
            trace.HashFailure = 0; trace.EqualityFailure = 1;
            try { d.Remove(new CollisionKey(input, trace), out value); } catch (InvalidOperationException) { result += value; }
            trace.EqualityFailure = 0;
            if (!d.Remove(new CollisionKey(input, trace), out value)) return -101;
            return unchecked(result + value + d.Count + trace.Hashes * 1009 + trace.Equalities * 31);
        }
        public static int Nulls(int input, int mode)
        {
            var d = new Dictionary<string, int>(); int result = 0;
            try { d.Remove(null); } catch (ArgumentNullException e) { result += e.ParamName.Length + e.Message.Length; }
            d.TryAdd("x", input);
            try { d.Remove(null, out var missing); } catch (ArgumentNullException e) { result += e.ParamName.Length + e.Message.Length; }
            Dictionary<int, int> absent = null;
            try { absent.Remove(input / mode); }
            catch (DivideByZeroException) { result += 3; }
            catch (OverflowException) { result += 5; }
            catch (NullReferenceException) { result += 7; }
            return result + d.Count;
        }
        public static int Interfaces(int input, int mode)
        {
            var d = new Dictionary<int, int>(); IDictionary<int, int> view = d;
            d.TryAdd(input, mode); int result = view.Remove(input) ? 1 : -100;
            d.TryAdd(mode, input); view.Clear();
            d.TryAdd(input, mode); ((System.Collections.IDictionary)d).Clear();
            return result + view.Count;
        }
        public static Dictionary<int, ValueVectorBox> CreateTracked()
        {
            var d = new Dictionary<int, ValueVectorBox>(); d.TryAdd(1, new ValueVectorBox(7)); d.TryAdd(2, new ValueVectorBox(9)); return d;
        }
        public static void RemoveTracked(Dictionary<int, ValueVectorBox> d) { d.Remove(1); }
        public static void ClearTracked(Dictionary<int, ValueVectorBox> d) { d.Clear(); }
        public static Dictionary<int, ValueVectorCell> CreateTrackedValues()
        {
            var d = new Dictionary<int, ValueVectorCell>(); ValueVectorCell value = default;
            value.Reference = new ValueVectorBox(7); d.TryAdd(1, value);
            value.Reference = new ValueVectorBox(9); d.TryAdd(2, value); return d;
        }
        public static void RemoveTrackedValues(Dictionary<int, ValueVectorCell> d) { d.Remove(1); }
        public static void ClearTrackedValues(Dictionary<int, ValueVectorCell> d) { d.Clear(); }
        public static int TrackedValues(int input, int mode) { var d = CreateTrackedValues(); RemoveTrackedValues(d); ClearTrackedValues(d); return d.Count; }
        public static int Tracked(int input, int mode) { var d = CreateTracked(); RemoveTracked(d); ClearTracked(d); return d.Count; }
    }
}
