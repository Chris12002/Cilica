using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ForgedOrdinalComparer { public static StringComparer Ordinal => StringComparer.Ordinal; }
    public sealed class OrdinalSortCustom : IComparer<string>
    {
        public int Compare(string first, string second) => String.CompareOrdinal(first, second);
    }
    public static class OrdinalListSortProbe
    {
        private static string Word(int index)
        {
            switch (index & 7) {
                case 0: return null; case 1: return ""; case 2: return "aggressive"; case 3: return "magical";
                case 4: return "a\0z"; case 5: return "a\u0301"; case 6: return "\ud83d\ude00"; default: return "\ue000";
            }
        }
        private static int Hash(List<string> list)
        {
            int hash = list.Count;
            for (int i = 0; i < list.Count; i++) {
                var value = list[i]; hash = unchecked(hash * 31 + (value == null ? -1 : value.Length));
                if (value != null) for (int j = 0; j < value.Length; j++) hash = unchecked(hash * 31 + value[j]);
            }
            return hash;
        }
        public static int Cases(int length, int pattern)
        {
            var list = new List<string>(length + 7);
            for (int i = 0; i < length; i++) list.Add(Word(pattern == 0 ? i : pattern == 1 ? length-i : pattern == 2 ? i*5+3 : 3));
            list.Sort(StringComparer.Ordinal); return Hash(list);
        }
        public static int Version(int a, int b)
        {
            var list = new List<string>(); int count = (a & 3);
            for (int i = 0; i < count; i++) list.Add(Word(i+b));
            var cursor = list.GetEnumerator(); cursor.MoveNext(); list.Sort(StringComparer.Ordinal);
            try { cursor.MoveNext(); return -123; }
            catch (InvalidOperationException) { return Hash(list); }
        }
        public static int Null(int a, int b)
        {
            List<string> list = null;
            try { list.Sort(StringComparer.Ordinal); return -321; }
            catch (NullReferenceException) { return unchecked(a+b); }
        }
        public static int Identity(int a, int b)
        {
            var first = new string(new char[] { 'a', (char)(a & 127) });
            var second = new string(new char[] { 'a', (char)(a & 127) });
            var list = new List<string>(); list.Add(second); list.Add(first); list.Sort(StringComparer.Ordinal);
            return unchecked(Hash(list) + (Object.ReferenceEquals(list[0],second) ? 101 : 103) + (Object.ReferenceEquals(list[1],first) ? 107 : 109));
        }
        public static int UnsupportedNull(int a, int b) { var list = new List<string>(); list.Sort((IComparer<string>)null); return a; }
        public static int UnsupportedAlias(int a, int b)
        {
            var list = new List<string>(); var comparer = StringComparer.Ordinal;
            int result = comparer.Compare(Word(a),Word(b)); list.Sort(comparer); return result;
        }
        public static int UnsupportedCustom(int a, int b) { var list = new List<string>(); list.Sort(new OrdinalSortCustom()); return a; }
        public static int UnsupportedGetter(int a, int b) { var list = new List<string>(); list.Sort(ForgedOrdinalComparer.Ordinal); return a; }
        public static int UnsupportedJoin(int a, int b) { var list = new List<string>(); list.Sort(a > 0 ? StringComparer.Ordinal : null); return a; }
        public static int UnsupportedInteger(int a, int b) { var list = new List<int>(); list.Sort((IComparer<int>)null); return a; }
    }
}
