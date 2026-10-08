using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    internal readonly struct InternalValueKey : IEquatable<InternalValueKey>
    {
        internal InternalValueKey(int value){Value=value;}
        internal int Value{get;}
        public bool Equals(InternalValueKey other)=>Value==other.Value;
        public override bool Equals(object other)=>other is InternalValueKey value&&Equals(value);
        public override int GetHashCode()=>Value;
    }
    public readonly struct PublicValueKey : IEquatable<PublicValueKey>
    {
        public PublicValueKey(int value){Value=value;}
        public int Value{get;}
        public bool Equals(PublicValueKey other)=>Value==other.Value;
        public override bool Equals(object other)=>other is PublicValueKey value&&Equals(value);
        public override int GetHashCode()=>Value;
    }
    public readonly struct ExplicitValueKey : IEquatable<ExplicitValueKey>
    {
        private readonly int value;
        public ExplicitValueKey(int input){value=input;}
        bool IEquatable<ExplicitValueKey>.Equals(ExplicitValueKey other)=>value==other.value;
        public override bool Equals(object other)=>other is ExplicitValueKey key&&value==key.value;
        public override int GetHashCode()=>value;
    }
    internal struct InternalGenericValueKey<T> : IEquatable<InternalGenericValueKey<T>>
    {
        public int Value;
        public InternalGenericValueKey(int value) { Value = value; }
        public bool Equals(InternalGenericValueKey<T> other) => Value == other.Value;
        public override bool Equals(object other) => other is InternalGenericValueKey<T> key && Equals(key);
        public override int GetHashCode() => Value;
    }
    public static class HiddenValueKeyProbe
    {
        private readonly struct PrivateValueKey : IEquatable<PrivateValueKey>
        {
            private readonly int value;
            public PrivateValueKey(int input){value=input;}
            public bool Equals(PrivateValueKey other)=>value==other.value;
            public override bool Equals(object other)=>other is PrivateValueKey key&&Equals(key);
            public override int GetHashCode()=>value;
        }
        private sealed class Cell
        {
            public int Id;
            public int Calls;
            public Cell(int id) { Id = id; }
        }
        private struct MovingKey : IEquatable<MovingKey>
        {
            public Cell Data;
            public int Mutations;
            public MovingKey(Cell data) { Data = data; Mutations = 0; }
            public override int GetHashCode()
            {
                ++Mutations;
                ++Data.Calls;
                var temporary = new Cell(Data.Id);
                if (Data.Id < 0) throw new ArgumentException("hash");
                return temporary.Id & 1;
            }
            public bool Equals(MovingKey other)
            {
                ++Mutations;
                ++Data.Calls;
                var temporary = new Cell(other.Data.Id);
                if (Data.Id == 3) throw new InvalidOperationException("equal");
                return Data.Id == temporary.Id;
            }
            public override bool Equals(object other) => other is MovingKey key && Equals(key);
        }
        public static int BorrowedSet(int a, int b)
        {
            var first = new Cell(a & 7);
            var second = new Cell(b & 7);
            var set = new HashSet<MovingKey>();
            int caught = 0;
            try { set.Add(new MovingKey(first)); set.Add(new MovingKey(second)); set.Contains(new MovingKey(first)); }
            catch (InvalidOperationException) { caught = 1000; }
            int mutations = 0;
            foreach (var key in set) mutations += key.Mutations;
            return caught + set.Count * 100 + mutations * 10 + first.Calls + second.Calls;
        }
        public static int BorrowedDictionary(int a, int b)
        {
            var first = new Cell(a & 7);
            var second = new Cell(b & 7);
            var map = new Dictionary<MovingKey, int>();
            int caught = 0;
            try { map.TryAdd(new MovingKey(first), a); map.TryAdd(new MovingKey(second), b); map.ContainsKey(new MovingKey(first)); }
            catch (InvalidOperationException) { caught = 1000; }
            int mutations = 0;
            foreach (var pair in map) mutations += pair.Key.Mutations;
            return caught + map.Count * 100 + mutations * 10 + first.Calls + second.Calls;
        }
        public static int HashFault(int a, int b)
        {
            var cell = new Cell(a < 0 ? -1 : 1);
            var map = new Dictionary<MovingKey, int>();
            try { map.TryAdd(new MovingKey(cell), b); }
            catch (ArgumentException) { return cell.Calls + 100; }
            return map.Count + cell.Calls;
        }
        private struct GenericKey<T> : IEquatable<GenericKey<T>>
        {
            public int Value;
            public GenericKey(int value) { Value = value; }
            public bool Equals(GenericKey<T> other) => Value == other.Value;
            public override bool Equals(object other) => other is GenericKey<T> key && Equals(key);
            public override int GetHashCode() => Value;
        }
        public static int UnsupportedPrivateGeneric(int a, int b)
        {
            var set = new HashSet<GenericKey<Cell>>();
            set.Add(new GenericKey<Cell>(a)); set.Add(new GenericKey<Cell>(b));
            return set.Count;
        }
        public static int GenericSet(int a, int b)
        {
            var set = new HashSet<InternalGenericValueKey<InternalValueKey>>();
            set.Add(new InternalGenericValueKey<InternalValueKey>(a));
            set.Add(new InternalGenericValueKey<InternalValueKey>(b));
            return set.Count;
        }
        public static int Direct(int a,int b)=>new InternalValueKey(a).GetHashCode()+new InternalValueKey(b).GetHashCode();
        public static int PublicSet(int a,int b){var set=new HashSet<PublicValueKey>();set.Add(new PublicValueKey(a));set.Add(new PublicValueKey(b));return set.Count;}
        public static int InternalSet(int a,int b){var set=new HashSet<InternalValueKey>();set.Add(new InternalValueKey(a));set.Add(new InternalValueKey(b));return set.Count;}
        public static int InternalLookup(int a,int b){var map=new Dictionary<InternalValueKey,int>();map.TryAdd(new InternalValueKey(a),b);map.TryGetValue(new InternalValueKey(a),out int found);return found;}
        public static int InternalDictionary(int a,int b){var map=new Dictionary<InternalValueKey,int>();map[new InternalValueKey(a)]=b;return map[new InternalValueKey(a)];}
        public static int PrivateSet(int a,int b){var set=new HashSet<PrivateValueKey>();set.Add(new PrivateValueKey(a));set.Add(new PrivateValueKey(b));return set.Count;}
        public static int ExplicitSet(int a,int b){var set=new HashSet<ExplicitValueKey>();set.Add(new ExplicitValueKey(a));set.Add(new ExplicitValueKey(b));return set.Count;}
        private static int Hash<T>(T value)=>value.GetHashCode();
        public static int GenericHash(int a,int b)=>Hash(new InternalValueKey(a));
    }
}
