using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class PlainReferenceKey { }
    public sealed class ReferenceKeyTrace { public int Value; }
    public class ReferenceKey
    {
        public int Id, Mode;
        public ReferenceKeyTrace Trace;
        public override int GetHashCode()
        {
            Trace.Value = unchecked(Trace.Value * 5 + 1);
            var pressure = new int[3]; pressure[0] = Id;
            if (Mode == 1) throw new InvalidOperationException("hash");
            return 7 + (pressure[0] & 0);
        }
        public override bool Equals(object other)
        {
            Trace.Value = unchecked(Trace.Value * 5 + 2);
            var pressure = new int[3]; pressure[0] = Id;
            if (Mode == 2) throw new InvalidOperationException("equal");
            return other is ReferenceKey key && pressure[0] == key.Id;
        }
    }
    public sealed class TypedReferenceKey : ReferenceKey, IEquatable<TypedReferenceKey>
    {
        public bool Equals(TypedReferenceKey other)
        {
            Trace.Value = unchecked(Trace.Value * 5 + 3);
            var pressure = new int[3]; pressure[0] = Id;
            if (Mode == 2) throw new InvalidOperationException("typed");
            return other != null && pressure[0] == other.Id;
        }
        public override bool Equals(object other) { Trace.Value = unchecked(Trace.Value * 5 + 4); return false; }
        public override int GetHashCode() => base.GetHashCode();
    }
    public class InheritedReferenceKey<T> : IEquatable<InheritedReferenceKey<T>>
    {
        public int Id;
        public bool Equals(InheritedReferenceKey<T> other) => other != null && Id == other.Id;
        public override bool Equals(object other) => false;
        public override int GetHashCode() => 1;
    }
    public sealed class DerivedReferenceKey : InheritedReferenceKey<int> { }
    public static class ReferenceKeyProbe
    {
        public static int Identity(int a, int b)
        {
            var first = new PlainReferenceKey(); var second = new PlainReferenceKey(); var d = new Dictionary<PlainReferenceKey,int>();
            d[first] = a; d[second] = b;
            bool duplicate = d.TryAdd(first, 17);
            d.TryGetValue(first, out int x); d.TryGetValue(second, out int y);
            return unchecked(x * 3 + y * 7 + d.Count * 11 + (duplicate ? 31 : 0) + (d.Remove(first) ? 1 : 0));
        }
        public static int Nulls(int a, int b)
        {
            var value = new PlainReferenceKey(); var array = new PlainReferenceKey[] { null, value, null };
            var list = new List<PlainReferenceKey>(array);
            return Array.IndexOf(array, null) + Array.IndexOf(array, value) * 3
                + (list.Remove(null) ? 7 : 0) + list.IndexOf(null) * 11 + (list.Contains(new PlainReferenceKey()) ? 31 : 0);
        }
        public static int Overrides(int a, int b)
        {
            var trace = new ReferenceKeyTrace(); var first = new ReferenceKey { Id = a, Trace = trace };
            var copy = new ReferenceKey { Id = a, Trace = trace }; var second = new ReferenceKey { Id = ~a, Trace = trace };
            var d = new Dictionary<ReferenceKey,int>(); d[first] = b;
            bool duplicate = d.TryAdd(copy, 17); d.TryGetValue(copy, out int result);
            d[second] = a; bool removed = d.Remove(copy);
            return unchecked(trace.Value + result * 7 + d.Count * 11 + (duplicate ? 31 : 0) + (removed ? 13 : 0));
        }
        public static int Typed(int a, int b)
        {
            var trace = new ReferenceKeyTrace(); var first = new TypedReferenceKey { Id = a, Trace = trace };
            var copy = new TypedReferenceKey { Id = a, Trace = trace };
            var d = new Dictionary<TypedReferenceKey,int>(); d[first] = b;
            bool duplicate = d.TryAdd(copy, 17); d.TryGetValue(copy, out int result);
            return unchecked(trace.Value + result * 7 + d.Count * 11 + (duplicate ? 31 : 0));
        }
        public static int StaticBase(int a, int b)
        {
            var trace = new ReferenceKeyTrace(); ReferenceKey first = new TypedReferenceKey { Id = a, Trace = trace };
            ReferenceKey copy = new TypedReferenceKey { Id = a, Trace = trace };
            var d = new Dictionary<ReferenceKey,int>(); d[first] = b;
            bool inserted = d.TryAdd(copy, 17); d.TryGetValue(first, out int result);
            return unchecked(trace.Value + result * 7 + d.Count * 11 + (inserted ? 31 : 0));
        }
        public static int Inherited(int a, int b)
        {
            var x = new DerivedReferenceKey { Id = a }; var y = new DerivedReferenceKey { Id = a };
            var first = new List<InheritedReferenceKey<int>>(); first.Add(x);
            var second = new List<DerivedReferenceKey>(); second.Add(x);
            return (first.Contains(y) ? 3 : 0) + (second.Contains(y) ? 7 : 0);
        }
        public static int Faults(int a, int b)
        {
            var trace = new ReferenceKeyTrace(); var first = new TypedReferenceKey { Id = a, Trace = trace };
            var copy = new TypedReferenceKey { Id = a, Trace = trace };
            var d = new Dictionary<TypedReferenceKey,int>(); d[first] = b;
            int result = 0;
            if ((b & 1) == 0) copy.Mode = 1; else first.Mode = 2;
            try { d.TryAdd(copy, 3); } catch (InvalidOperationException error) { result = error.Message.Length; }
            return unchecked(trace.Value * 7 + result * 3 + d.Count * 11);
        }
        public static int ObjectAliases(int a, int b)
        { var d = new Dictionary<object,int>(); d[new PlainReferenceKey()] = a; return d.Count; }
        public static int Vectors(int a, int b)
        {
            var x = new int[] { a }; var y = new int[] { a };
            var d = new Dictionary<int[],int>(); d[x] = b; d[y] = a; d.TryGetValue(x, out int result);
            return unchecked(result * 7 + d.Count * 3);
        }
        public static int NullDictionary(int a, int b)
        {
            var d = new Dictionary<PlainReferenceKey,int>();
            try { d.TryAdd(null, a); return 0; } catch (ArgumentNullException error) { return error.ParamName.Length + d.Count; }
        }
    }
}
