using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public struct CapacityKey : IEquatable<CapacityKey>
    {
        public int Number;
        public bool Equals(CapacityKey other) => Number == other.Number;
        public override bool Equals(object other) => other is CapacityKey key && Equals(key);
        public override int GetHashCode() => Number & 1;
        public override string ToString() => "capacity key";
    }
    public static class DictionaryCapacityProbe
    {
        public static int Empty(int a, int b) => new Dictionary<int,int>(a).Count + b;
        public static int Integers(int a, int b)
        {
            var map = new Dictionary<int,long>(a);
            int count = (b & 31) + 21;
            for (int i = 0; i < count; ++i) map.Add(i, ((long)b << 32) | (uint)i);
            for (int i = 0; i < count; i += 2) map.Remove(i);
            for (int i = count; i < count + 9; ++i) map[i] = i;
            int result = 17;
            foreach (var pair in map) result = unchecked(result * 31 + pair.Key + (int)pair.Value + (int)(pair.Value >> 32));
            map.Clear(); map.Add(a,b);
            return unchecked(result + (int)map[a] + map.Count);
        }
        public static int References(int a, int b)
        {
            var map = new Dictionary<string,DictionaryPayload>(a);
            map.Add("first", new DictionaryPayload(a)); map.Add("second", new DictionaryPayload(b));
            map.Remove("first"); map.Add("third", new DictionaryPayload(~b));
            int result = map["second"].Number ^ map["third"].Number;
            foreach (var value in map.Values) result = unchecked(result * 13 + value.Number);
            return result;
        }
        public static int Collisions(int a, int b)
        {
            var map = new Dictionary<CapacityKey,int>(a);
            for (int i = 0; i < 29; ++i) { var key = new CapacityKey(); key.Number = i; map.TryAdd(key, i ^ b); }
            var removed = new CapacityKey(); removed.Number = b & 15; map.Remove(removed);
            map.Add(removed, a);
            int result = map[removed];
            foreach (var pair in map) result = unchecked(result * 17 + pair.Key.Number + pair.Value);
            return result;
        }
        private static int Text(string s) { if (s == null) return 0; int h = 17; for (int i = 0; i < s.Length; ++i) h = unchecked(h * 31 + s[i]); return h; }
        public static int Negative(int a, int b)
        {
            try { return new Dictionary<int,int>(b < 0 ? int.MinValue : -a).Count; }
            catch (ArgumentOutOfRangeException e) { return Text(e.Message) ^ Text(e.ParamName); }
        }
        public static int Enumeration(int a, int b)
        {
            var map = new Dictionary<int,int>(a); var empty = map.GetEnumerator();
            int result = empty.MoveNext() ? -1 : 3;
            map.Add(1,b);
            try { empty.MoveNext(); } catch (InvalidOperationException e) { result += Text(e.Message); }
            var current = map.GetEnumerator(); current.MoveNext(); map[1] = a;
            result = unchecked(result + current.Current.Value);
            map.Remove(1); result += current.MoveNext() ? 7 : 11;
            map.Add(2,b); return unchecked(result + map[2]);
        }
    }
}
