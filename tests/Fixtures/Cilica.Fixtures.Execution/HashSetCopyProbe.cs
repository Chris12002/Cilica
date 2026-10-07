using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class HashSetCopyProbe
    {
        private static int Hash(string text) { if (text == null) return 0; int result = 17; for (int i = 0; i < text.Length; i++) result = unchecked(result * 31 + text[i]); return result; }
        private static HashSet<int> Populate(int a, int b)
        {
            var set = new HashSet<int>(); for (int i = 0; i < 12; i++) set.Add(i);
            set.Remove(1); set.Remove(3); set.Remove(9); set.Add(17); set.Add(23); return set;
        }
        public static int Whole(int a, int b)
        {
            var set = Populate(a,b); var target = new int[14]; set.CopyTo(target);
            int result = a; for (int i = 0; i < target.Length; i++) result = unchecked(result * 31 + target[i]); return result;
        }
        public static int Offset(int a, int b)
        {
            var set = Populate(a,b); var target = new int[14]; ((ICollection<int>)set).CopyTo(target, 2);
            int result = b; for (int i = 0; i < target.Length; i++) result = unchecked(result * 31 + target[i]); return result;
        }
        public static int Prefix(int a, int b)
        {
            var set = Populate(a,b); var cursor = set.GetEnumerator(); cursor.MoveNext();
            var target = new int[20]; set.CopyTo(target, 1, b & 15);
            int result = cursor.Current; while (cursor.MoveNext()) result = unchecked(result * 7 + cursor.Current);
            for (int i = 0; i < target.Length; i++) result = unchecked(result * 31 + target[i]); return result;
        }
        public static int Faults(int a, int b)
        {
            var set = new HashSet<int>(); set.Add(11); set.Add(23);
            var target = a == int.MinValue || b == int.MinValue ? null : new int[5]; int result = 0;
            try { set.CopyTo(target,a,b); result = 3; }
            catch (ArgumentException e) { result = unchecked(Hash(e.Message) + Hash(e.ParamName)); }
            if (target != null) for (int i = 0; i < target.Length; i++) result = unchecked(result * 7 + target[i]); return result;
        }
        public static int OffsetFaults(int a, int b)
        {
            var set = new HashSet<int>(); if ((b & 1) != 0) set.Add(11);
            var target = b < 0 ? null : new int[2];
            try { set.CopyTo(target,a); return 3; }
            catch (ArgumentException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName)); }
        }
        public static int Covariance(int a, int b)
        {
            var set = new HashSet<ReferenceKey>(); var trace = new ReferenceKeyTrace();
            var first = new TypedReferenceKey(); first.Id = 1; first.Trace = trace;
            ReferenceKey second = b < 0 ? new ReferenceKey() : new TypedReferenceKey(); second.Id = 2; second.Trace = trace;
            set.Add(first); set.Add(second); var target = new TypedReferenceKey[3]; int result = 0;
            try { set.CopyTo(target,a & 1,2); result = 3; }
            catch (ArrayTypeMismatchException e) { result = Hash(e.Message); }
            for (int i = 0; i < target.Length; i++) result = unchecked(result * 7 + (target[i] == null ? 0 : target[i].Id)); return result;
        }
        public static int ValuesAndCallbacks(int a, int b)
        {
            var trace = new KeyTrace(); var set = new HashSet<CollisionKey>();
            set.Add(new CollisionKey(a,trace)); set.Add(new CollisionKey(unchecked(a + 1),trace));
            int before = trace.Hashes + trace.Equalities; trace.HashFailure = 1; trace.EqualityFailure = 1;
            var target = new CollisionKey[3]; set.CopyTo(target,1); set.Clear(); string pressure = string.Concat("set","copy");
            return unchecked(target[1].Number + target[2].Number + target[1].Trace.Hashes + target[1].Trace.Equalities - before + pressure.Length + b);
        }
        public static int NullAndIdentity(int a, int b)
        {
            var set = new HashSet<string>(); var text = string.Concat("saved"," game"); set.Add(null); set.Add(text);
            var target = new string[3]; set.CopyTo(target,1); set.Clear(); string pressure = string.Concat("pressure","copy");
            return unchecked(a + b + pressure.Length + (target[1] == null && ReferenceEquals(target[2],text) ? 17 : -100));
        }
        public static int Cleared(int a, int b)
        {
            var set = Populate(a,b); set.Clear(); var target = new int[0]; set.CopyTo(target); set.CopyTo(target,0); set.CopyTo(target,0,0); return set.Count + a;
        }
        public static int AddRange(int a, int b)
        {
            var set = Populate(a,b); var list = new List<int>(); list.Add(a); list.AddRange(set); set.Clear();
            int result = list.Count; for (int i = 0; i < list.Count; i++) result = unchecked(result * 31 + list[i]); return result;
        }
        public static int Construction(int a, int b)
        {
            var set = Populate(a,b); var list = new List<int>(set); set.Clear();
            int result = list.Count; for (int i = 0; i < list.Count; i++) result = unchecked(result * 31 + list[i]); return unchecked(result + a + b);
        }
    }
}
