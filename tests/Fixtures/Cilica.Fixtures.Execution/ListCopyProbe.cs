using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ListCopyProbe
    {
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Integers(int a, int b)
        {
            var list = new List<int>(); list.Add(a); list.Add(b);
            var target = new int[4]; var e = list.GetEnumerator();
            if (b < 0) list.CopyTo(target); else list.CopyTo(target, 1);
            bool current = e.MoveNext(); list[0] = 7;
            return unchecked(target[0] + target[1] * 7 + target[2] * 31 + list.Count + (current ? e.Current : 17));
        }
        public static int References(int a, int b)
        {
            var list = new List<ValueVectorBox>(); var value = new ValueVectorBox(a); list.Add(value); list.Add(null);
            var target = new ValueVectorBox[3]; list.CopyTo(target, 1); list.Clear();
            value.Number = b; return target[1].Number + (target[1] == value && target[2] == null ? 7 : -100);
        }
        public static int Values(int a, int b)
        {
            var list = new List<ValueVectorCell>(); ValueVectorCell value = default;
            value.Number = a; value.Wide = ((long)a << 32) + b; value.Reference = new ValueVectorBox(b); list.Add(value);
            var target = new ValueVectorCell[2]; list.CopyTo(target, 1); list.Clear(); value.Number = b;
            return unchecked(target[1].Number + (int)(target[1].Wide >> 32) + target[1].Reference.Number + (target[1].Reference == value.Reference ? 7 : -100));
        }
        public static int Faults(int a, int b)
        {
            var list = new List<int>(); if (b > 0) { list.Add(11); list.Add(13); }
            int[] target = b < 0 ? null : new int[2]; int result = 0;
            try { list.CopyTo(target, a); result = 3; }
            catch (ArgumentNullException e) { result = Hash(e.Message) + Hash(e.ParamName); }
            catch (ArgumentOutOfRangeException e) { result = Hash(e.Message) + Hash(e.ParamName); }
            catch (ArgumentException e) { result = Hash(e.Message) + Hash(e.ParamName); }
            return unchecked(result + (target == null ? 0 : target[0] * 17 + target[1]));
        }
        public static int Covariant(int a, int b)
        {
            var list = new List<object>(); list.Add("first");
            if (b < 0) list.Add(new object()); else list.Add("second");
            var target = new string[4]; int result = 0;
            try { list.CopyTo(target, (int)((uint)a % 3)); result = 3; }
            catch (InvalidCastException e) { result = Hash(e.Message); }
            catch (ArgumentException e) { result = Hash(e.Message); }
            for (int i = 0; i < target.Length; i++) result = unchecked(result * 7 + (target[i] == null ? 0 : target[i].Length));
            return result;
        }
        public static int EmptyValue(int a, int b)
        {
            var list = new List<ValueVectorEmpty>(); list.Add(default); list.Add(default);
            var target = new ValueVectorEmpty[2]; list.CopyTo(target); return list.Count + target.Length + a + b;
        }
        public static int UnsupportedSection(int a, int b) { new List<int>().CopyTo(0, new int[1], 0, 0); return a; }
        public static int UnsupportedLegacy(int a, int b) { ((System.Collections.ICollection)new List<int>()).CopyTo(new int[1], 0); return b; }
    }
}
