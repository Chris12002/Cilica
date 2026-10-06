using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public struct ReentrantSetKey : IEquatable<ReentrantSetKey>
    {
        public int Number;
        public bool Equals(ReentrantSetKey other)
        {
            if (ReentrantSetState.Removing == 1) { ReentrantSetState.Removing = 2; ReentrantSetState.Owner.Remove(other); }
            return Number == other.Number;
        }
        public override bool Equals(object other) => other is ReentrantSetKey key && Equals(key);
        public override int GetHashCode() => 0;
    }
    public static class ReentrantSetState
    {
        public static HashSet<ReentrantSetKey> Owner;
        public static int Removing;
    }
    public static class HashSetProbe
    {
        public static int Integers(int a, int b)
        {
            var set = new HashSet<int>(); int result = set.Contains(a) || set.Remove(a) ? -100 : 1;
            for (int i = 0; i < 24; i++) if (!set.Add(unchecked(a + i))) return -101;
            if (set.Add(a)) return -102;
            for (int i = 0; i < 24; i += 2) if (!set.Remove(unchecked(a + i))) return -103;
            for (int i = 0; i < 40; i++) { bool added = set.Add(unchecked(a + i)); result = unchecked(result * 7 + (added ? 1 : 0)); }
            return unchecked(result + set.Count + (set.Contains(b) ? 13 : 17));
        }
        public static int Clear(int a, int b)
        {
            var set = new HashSet<long>(); set.Clear();
            for (int round = 0; round < 3; round++)
            {
                for (int i = 0; i < 12; i++) set.Add(((long)a << 32) + i);
                set.Remove(((long)a << 32) + 5); set.Clear(); set.Clear();
                if (set.Count != 0) return -100;
            }
            set.Add(b); return set.Count + (set.Contains(b) ? 7 : -100);
        }
        public static int Strings(int a, int b)
        {
            var set = new HashSet<string>(); int result = set.Contains(null) ? -100 : 1;
            set.Add(null); if (set.Add(null) || !set.Contains(null)) return -101;
            string first = string.Concat("o", "ne"), equal = string.Concat("on", "e"); set.Add(first);
            if (set.Add(equal) || !set.TryGetValue(equal, out string actual) || (object)actual != first) return -102;
            set.Remove(null); if (set.Contains(null)) return -103;
            set.Add(null); set.Remove(equal); set.Clear();
            return unchecked(result + set.Count + a + b);
        }
        public static int Collisions(int a, int b)
        {
            var trace = new KeyTrace(); var set = new HashSet<CollisionKey>();
            for (int i = 0; i < 12; i++) set.Add(new CollisionKey(unchecked(a + i), trace));
            int result = set.Add(new CollisionKey(a, trace)) ? -100 : 1;
            for (int i = 0; i < 12; i += 2) if (set.Remove(new CollisionKey(unchecked(a + i), trace))) result++;
            for (int i = 12; i < 24; i++) set.Add(new CollisionKey(unchecked(a + i), trace));
            if (set.TryGetValue(new CollisionKey(unchecked(a + 23), trace), out var actual)) result = unchecked(result + actual.Number);
            result = unchecked(result + (set.Contains(new CollisionKey(b, trace)) ? 7 : 13));
            return unchecked(result + set.Count + trace.Hashes * 1009 + trace.Equalities * 31);
        }
        public static int Faults(int a, int b)
        {
            var trace = new KeyTrace(); var set = new HashSet<CollisionKey>(); trace.HashFailure = 1;
            int result = set.Contains(new CollisionKey(a, trace)) || set.Remove(new CollisionKey(a, trace)) ? -100 : 1;
            try { set.Add(new CollisionKey(a, trace)); } catch (InvalidOperationException) { result += 7; }
            trace.HashFailure = 0; set.Add(new CollisionKey(a, trace)); trace.EqualityFailure = 1;
            try { set.Remove(new CollisionKey(a, trace)); } catch (InvalidOperationException) { result += 11; }
            trace.EqualityFailure = 0; if (!set.Contains(new CollisionKey(a, trace))) return -101;
            set.Clear(); return unchecked(result + set.Count + trace.Hashes * 1009 + trace.Equalities * 31 + b);
        }
        public static int Interfaces(int a, int b)
        {
            var set = new HashSet<int>(); ICollection<int> collection = set; ISet<int> view = set;
            collection.Add(a); if (view.Add(a) || collection.IsReadOnly) return -100;
            view.Add(b); int result = collection.Count;
            collection.Remove(a); collection.Clear(); return result + set.Count;
        }
        public static int NullSet(int a, int b)
        {
            HashSet<int> set = null;
            try { return set.Add(a / b) ? 1 : 0; }
            catch (DivideByZeroException) { return 3; }
            catch (OverflowException) { return 5; }
            catch (NullReferenceException) { return 7; }
        }
        public static int GuidKeys(int a, int b)
        {
            var set = new HashSet<Guid>(); var key = new Guid(a, (short)b, 0, 1,2,3,4,5,6,7,8);
            set.Add(key); if (!set.TryGetValue(key, out var value) || value != key) return -100;
            set.Remove(key); return set.Count + (set.Contains(key) ? -101 : 7);
        }
        public static int ReentrantRemoval(int a, int b)
        {
            var set = new HashSet<ReentrantSetKey>(); ReentrantSetState.Owner = set; ReentrantSetState.Removing = 0;
            ReentrantSetKey key = default; key.Number = a; set.Add(key); ReentrantSetState.Removing = 1;
            bool found = set.TryGetValue(key, out var actual);
            ReentrantSetState.Owner = null; ReentrantSetState.Removing = 0;
            return unchecked(actual.Number + set.Count + (found ? 7 : -100) + b);
        }
        public static HashSet<CollisionKey> CreateTracked()
        {
            var set = new HashSet<CollisionKey>(); set.Add(new CollisionKey(1, new KeyTrace())); set.Add(new CollisionKey(2, new KeyTrace())); return set;
        }
        public static void RemoveTracked(HashSet<CollisionKey> set) { set.Remove(new CollisionKey(1, new KeyTrace())); }
        public static void ClearTracked(HashSet<CollisionKey> set) { set.Clear(); }
        public static int Tracked(int a, int b) { var set = CreateTracked(); RemoveTracked(set); ClearTracked(set); return set.Count + a; }
        public static int UnsupportedEnumeration(int a, int b) { var set = new HashSet<int>(); foreach (var value in (IEnumerable<int>)set) a += value; return a; }
        public static int UnsupportedComparer(int a, int b) => new HashSet<int>(EqualityComparer<int>.Default).Count;
    }
}
