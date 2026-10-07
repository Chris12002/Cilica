using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class DictionaryCopyProbe
    {
        private static int Hash(string text) { if (text == null) return 0; int result = 17; for (int i = 0; i < text.Length; i++) result = unchecked(result * 31 + text[i]); return result; }
        private static Dictionary<int,int> Populate(int a, int b)
        {
            var dictionary = new Dictionary<int,int>();
            for (int i = 0; i < 12; i++) dictionary.Add(i, unchecked(a + i));
            dictionary.Remove(1); dictionary.Remove(3); dictionary.Remove(9);
            dictionary.Add(17, b); dictionary.Add(23, a); dictionary[4] = b;
            return dictionary;
        }
        public static int Keys(int a, int b)
        {
            var dictionary = Populate(a,b); var target = new int[14]; dictionary.Keys.CopyTo(target, 1);
            dictionary.Clear(); int result = 0; for (int i = 0; i < target.Length; i++) result = unchecked(result * 31 + target[i]); return result;
        }
        public static int Values(int a, int b)
        {
            var dictionary = Populate(a,b); var target = new int[14]; ((ICollection<int>)dictionary.Values).CopyTo(target, 1);
            int result = 0; for (int i = 0; i < target.Length; i++) result = unchecked(result * 31 + target[i]); return result;
        }
        public static int Pairs(int a, int b)
        {
            var dictionary = Populate(a,b); var target = new KeyValuePair<int,int>[14]; ((ICollection<KeyValuePair<int,int>>)dictionary).CopyTo(target, 1);
            int result = 0; for (int i = 0; i < target.Length; i++) result = unchecked(result * 31 + target[i].Key * 7 + target[i].Value); return result;
        }
        public static int KeyFaults(int a, int b)
        {
            var dictionary = new Dictionary<int,int>(); if ((b & 1) != 0) dictionary.Add(a,b); var target = b < 0 ? null : new int[2];
            try { dictionary.Keys.CopyTo(target,a); return 3; }
            catch (ArgumentException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int ValueFaults(int a, int b)
        {
            var dictionary = new Dictionary<int,int>(); if ((b & 1) != 0) dictionary.Add(a,b); var target = b < 0 ? null : new int[2];
            try { dictionary.Values.CopyTo(target,a); return 3; }
            catch (ArgumentException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int PairFaults(int a, int b)
        {
            var dictionary = new Dictionary<int,int>(); if ((b & 1) != 0) dictionary.Add(a,b); var target = b < 0 ? null : new KeyValuePair<int,int>[2];
            try { ((ICollection<KeyValuePair<int,int>>)dictionary).CopyTo(target,a); return 3; }
            catch (ArgumentException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int CovariantValues(int a, int b)
        {
            var dictionary = new Dictionary<int,object>(); dictionary.Add(1,"first"); dictionary.Add(2,b < 0 ? new object() : "second");
            var target = new string[3]; int result = 0;
            try { dictionary.Values.CopyTo(target,a & 1); result = 3; }
            catch (ArrayTypeMismatchException e) { result = Hash(e.Message); }
            for (int i = 0; i < target.Length; i++) result = unchecked(result * 7 + (target[i] == null ? 0 : target[i].Length)); return result;
        }
        public static int CovariantKeys(int a, int b)
        {
            var dictionary = new Dictionary<ReferenceKey,int>(); var trace = new ReferenceKeyTrace();
            var first = new TypedReferenceKey(); first.Id = 1; first.Trace = trace;
            ReferenceKey second = b < 0 ? new ReferenceKey() : new TypedReferenceKey(); second.Id = 2; second.Trace = trace;
            dictionary.TryAdd(first,a); dictionary.TryAdd(second,b);
            var target = new TypedReferenceKey[3]; int result = 0;
            try { dictionary.Keys.CopyTo(target,a & 1); result = 3; }
            catch (ArrayTypeMismatchException e) { result = Hash(e.Message); }
            for (int i = 0; i < target.Length; i++) result = unchecked(result * 7 + (target[i] == null ? 0 : target[i].Id)); return result;
        }
        public static int InlineValues(int a, int b)
        {
            ValueVectorCell value = default; value.Number = a; value.Wide = ((long)a << 32) + b; value.Reference = new ValueVectorBox(b);
            var dictionary = new Dictionary<int,ValueVectorCell>(); dictionary.Add(1,value);
            var target = new ValueVectorCell[2]; dictionary.Values.CopyTo(target,1);
            var pairs = new KeyValuePair<int,ValueVectorCell>[1]; ((ICollection<KeyValuePair<int,ValueVectorCell>>)dictionary).CopyTo(pairs,0);
            dictionary.Clear(); value.Reference.Number = a; string pressure = string.Concat("copy","values");
            return unchecked(target[1].Number + (int)(target[1].Wide >> 32) + target[1].Reference.Number + pairs[0].Value.Number + pressure.Length);
        }
        public static int ReferenceIdentity(int a, int b)
        {
            var first = new PlainReferenceKey(); var second = new PlainReferenceKey(); var value = new ValueVectorBox(a);
            var dictionary = new Dictionary<PlainReferenceKey,ValueVectorBox>(); dictionary.Add(first,value); dictionary.Add(second,value);
            var keys = new PlainReferenceKey[2]; dictionary.Keys.CopyTo(keys,0); var values = new ValueVectorBox[2]; dictionary.Values.CopyTo(values,0);
            dictionary.Clear(); value.Number = b; string pressure = string.Concat("copy","identity");
            return unchecked(values[0].Number + pressure.Length + (ReferenceEquals(keys[0],first) && ReferenceEquals(keys[1],second) && ReferenceEquals(values[0],values[1]) ? 17 : -100));
        }
        public static int Callbacks(int a, int b)
        {
            var trace = new KeyTrace(); var key = new CollisionKey(1,trace); var dictionary = new Dictionary<CollisionKey,int>(); dictionary.TryAdd(key,a);
            int before = trace.Hashes + trace.Equalities; trace.HashFailure = 1; trace.EqualityFailure = 1;
            var keys = new CollisionKey[1]; dictionary.Keys.CopyTo(keys,0); var values = new int[1]; dictionary.Values.CopyTo(values,0);
            var pairs = new KeyValuePair<CollisionKey,int>[1]; ((ICollection<KeyValuePair<CollisionKey,int>>)dictionary).CopyTo(pairs,0);
            return unchecked(keys[0].Number + pairs[0].Key.Number + values[0] + trace.Hashes + trace.Equalities - before + b);
        }
        public static int Construction(int a, int b)
        {
            var dictionary = Populate(a,b); var keys = new List<int>(dictionary.Keys); var values = new List<int>(dictionary.Values);
            var pairs = new List<KeyValuePair<int,int>>(dictionary); dictionary.Clear();
            int result = 0; for (int i = 0; i < keys.Count; i++) result = unchecked(result * 31 + keys[i] + values[i] + pairs[i].Key + pairs[i].Value); return result;
        }
        public static int Cleared(int a, int b)
        {
            var dictionary = Populate(a,b); dictionary.Clear(); var keys = new int[0]; dictionary.Keys.CopyTo(keys,0);
            var values = new int[0]; dictionary.Values.CopyTo(values,0); var pairs = new KeyValuePair<int,int>[0]; ((ICollection<KeyValuePair<int,int>>)dictionary).CopyTo(pairs,0);
            return dictionary.Count + a;
        }
        public static int UnsupportedLegacy(int a, int b)
        {
            var dictionary = Populate(a,b); ((ICollection)dictionary.Keys).CopyTo(new int[20],0); return a;
        }
    }
}
