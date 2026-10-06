using System;
using System.Collections.Generic;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public sealed class AuthoredIntegerComparer : IComparer<int>
    {
        public int Calls;
        public int Compare(int first, int second) { Calls++; return first < second ? -1 : first > second ? 1 : 0; }
    }
    public sealed class AuthoredValueComparer : IComparer<ValueVectorCell>
    {
        public int Calls;
        public int Compare(ValueVectorCell first, ValueVectorCell second)
        {
            Calls++; int value = first.Reference.Number.CompareTo(second.Reference.Number);
            first.Number = 0; return value;
        }
    }
    public static class StringComparerProbe
    {
        public static int Singleton(int a, int b) => (object)StringComparer.Ordinal == StringComparer.Ordinal ? 17 : -1;
        public static int Direct(int a, int b) => StringComparer.Ordinal.Compare(OrdinalTextProbe.Pick(a), OrdinalTextProbe.Pick(b));
        public static int Typed(int a, int b) { IComparer<string> comparer = StringComparer.Ordinal; return comparer.Compare(OrdinalTextProbe.Pick(a), OrdinalTextProbe.Pick(b)); }
        public static int Equality(int a, int b)
        {
            string first = OrdinalTextProbe.Pick(a), second = OrdinalTextProbe.Pick(b);
            IEqualityComparer<string> comparer = StringComparer.Ordinal;
            return (StringComparer.Ordinal.Equals(first, second) ? 1 : 0) + (comparer.Equals(first, second) ? 2 : 0);
        }
        public static int Identity(int a, int b)
        {
            var comparer = StringComparer.Ordinal;
            var left = new StringBuilder(); left.Append((char)a); left.Append((char)b);
            string first = left.ToString(); string second = left.ToString();
            if ((object)first == second) return -100;
            var again = StringComparer.Ordinal; if ((object)comparer != again) return -101;
            return comparer.Compare(first, second) + (again.Equals(first, second) ? 17 : -102);
        }
        public static int Integers(int a, int b)
        {
            var owner = new AuthoredIntegerComparer(); IComparer<int> comparer = owner;
            int result = comparer.Compare(a, b); return unchecked(result + owner.Calls * 37);
        }
        public static int Values(int a, int b)
        {
            var owner = new AuthoredValueComparer(); IComparer<ValueVectorCell> comparer = owner;
            ValueVectorCell first = default, second = default;
            first.Number = a; first.Reference = new ValueVectorBox(a); second.Reference = new ValueVectorBox(b);
            return unchecked(comparer.Compare(first, second) + first.Number + owner.Calls * 37);
        }
        public static int NullReceiver(int a, int b)
        {
            IComparer<string> comparer = a < 0 ? null : StringComparer.Ordinal;
            try { return comparer.Compare(OrdinalTextProbe.Pick(a), OrdinalTextProbe.Pick(b)); }
            catch (NullReferenceException) { return -19; }
        }
        public static int UnsupportedHash(int a, int b) => StringComparer.Ordinal.GetHashCode("a");
        public static int UnsupportedComparerHash(int a, int b) => StringComparer.Ordinal.GetHashCode();
        public static int UnsupportedObjectCompare(int a, int b) => StringComparer.Ordinal.Compare((object)"a", (object)"b");
        public static int UnsupportedIgnoreCase(int a, int b) => StringComparer.OrdinalIgnoreCase.Compare("a", "A");
        public static int UnsupportedObjectEquality(int a, int b) => StringComparer.Ordinal.Equals((object)StringComparer.Ordinal) ? 1 : 0;
    }
}
