using System;
using System.Collections.Generic;

namespace Cilica.Fixtures.Execution
{
    public sealed class DictionaryPayload
    {
        public int Number;
        public DictionaryPayload(int number) { Number = number; }
    }
    public struct DictionaryLeaf { public long Number; public DictionaryPayload Payload; }
    public struct DictionaryValue { public DictionaryLeaf Left; }
    public sealed class KeyTrace { public int Hashes; public int Equalities; public int HashFailure; public int EqualityFailure; }
    public struct CollisionKey : IEquatable<CollisionKey>
    {
        public int Number;
        public KeyTrace Trace;
        public CollisionKey(int number, KeyTrace trace) { Number = number; Trace = trace; }
        public bool Equals(CollisionKey other)
        {
            int old = Number++;
            Trace.Equalities++;
            if (Trace.EqualityFailure != 0) throw new InvalidOperationException("equality");
            return old == other.Number;
        }
        public override bool Equals(object other) => other is CollisionKey key && Equals(key);
        public override int GetHashCode() { Number++; Trace.Hashes++; if (Trace.HashFailure != 0) throw new InvalidOperationException("hash"); return 0; }
    }
    public static class DictionaryProbe
    {
        private static Guid MakeGuid(int input, int mode) => new Guid(input, (short)mode, (short)(input >> 16), 1, 2, 3, 4, 5, 6, 7, (byte)mode);
        public static int Integers(int input, int mode)
        {
            var values = new Dictionary<int, long>();
            long absent = 77;
            int result = values.TryGetValue(input, out absent) ? 11 : 17;
            result += (int)absent;
            int count = (int)((uint)mode % 32) + 1;
            for (int i = 0; i < count; ++i) values.TryAdd(unchecked(input + i), (long)i * 2147483647L);
            if (values.TryAdd(input, 91)) return -101;
            values[input] = mode;
            for (int i = 0; i < count; ++i)
            {
                if (!values.TryGetValue(unchecked(input + i), out var value)) return -103;
                result = unchecked(result * 31 + (int)value);
            }
            return unchecked(result + values.Count);
        }
        public static int WideKeys(int input, int mode)
        {
            var values = new Dictionary<ulong, DictionaryValue>();
            ulong key = unchecked((ulong)(long)input * 0x100000001UL);
            DictionaryValue value = default;
            value.Left.Number = input;
            value.Left.Payload = new DictionaryPayload(mode);
            values.TryAdd(key, value);
            value.Left.Number++;
            values.TryAdd(key ^ (1UL << 32), value);
            for (int i = 0; i < 8; ++i) { var scratch = new DictionaryPayload(i); scratch.Number++; }
            values.TryGetValue(key, out var copy);
            copy.Left.Number++;
            values.TryGetValue(key, out var unchanged);
            values.TryGetValue(key ^ (1UL << 31), out var absent);
            return unchecked((int)copy.Left.Number + (int)unchanged.Left.Number + unchanged.Left.Payload.Number
                + (absent.Left.Payload == null ? 37 : 41) + values.Count);
        }
        public static int Guids(int input, int mode)
        {
            var values = new Dictionary<Guid, DictionaryPayload>();
            var first = MakeGuid(input, mode);
            var second = MakeGuid(mode, input);
            var payload = new DictionaryPayload(input);
            values.TryAdd(first, payload);
            bool added = values.TryAdd(second, new DictionaryPayload(mode));
            values.TryGetValue(first, out var stored);
            values[first] = new DictionaryPayload(mode);
            values.TryGetValue(first, out var changed);
            return unchecked(stored.Number + changed.Number + (stored == payload ? 7 : 11) + (added ? 13 : 17) + values.Count);
        }
        public static int Collisions(int input, int mode)
        {
            var trace = new KeyTrace();
            var values = new Dictionary<CollisionKey, int>();
            values.TryGetValue(new CollisionKey(input, trace), out var missing);
            int count = (int)((uint)mode % 20) + 1;
            for (int i = 0; i < count; ++i) values.TryAdd(new CollisionKey(unchecked(input + i), trace), i);
            bool duplicate = values.TryAdd(new CollisionKey(input, trace), 91);
            values[new CollisionKey(input, trace)] = mode;
            values.TryGetValue(new CollisionKey(input, trace), out var found);
            values.TryGetValue(new CollisionKey(unchecked(input - 1), trace), out missing);
            return unchecked(trace.Hashes * 1009 + trace.Equalities * 31 + found + missing + values.Count + (duplicate ? 13 : 17));
        }
        public static int CallbackFaults(int input, int mode)
        {
            var trace = new KeyTrace(); trace.HashFailure = mode & 1;
            var values = new Dictionary<CollisionKey, int>();
            int result = values.TryGetValue(new CollisionKey(input, trace), out var absent) ? 7 : 11;
            try { values.TryAdd(new CollisionKey(input, trace), mode); }
            catch (InvalidOperationException) { result += 17; }
            trace.HashFailure = 0;
            values.TryAdd(new CollisionKey(input, trace), input);
            trace.EqualityFailure = 1;
            try { values.TryAdd(new CollisionKey(input, trace), mode); }
            catch (InvalidOperationException) { result += 31; }
            return unchecked(result + trace.Hashes * 1009 + trace.Equalities * 31 + values.Count + absent);
        }
        public static int UnsupportedReferenceKey(int input, int mode)
        {
            var values = new Dictionary<DictionaryPayload, int>();
            values.TryAdd(new DictionaryPayload(input), mode);
            return values.Count;
        }
    }
}
