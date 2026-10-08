using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public struct OrdinalHashId : IEquatable<OrdinalHashId>
    {
        public string Value;
        public OrdinalHashId(string value) { Value = value; }
        public bool Equals(OrdinalHashId other) => Value == other.Value;
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    }
    public sealed class OrdinalHashTrace
    {
        public int Order;
        public string Value;
        public string Read(int digit) { Order = unchecked(Order * 10 + digit); return Value; }
    }
    public static class OrdinalHashProbe
    {
        public static int Stable(int a,int b)
        {
            string first = new string(new char[] { (char)a, '\0', (char)b, '\ud800', '\udfff' });
            string second = new string(new char[] { (char)a, '\0', (char)b, '\ud800', '\udfff' });
            var comparer = StringComparer.Ordinal;
            int hash = comparer.GetHashCode(first);
            return hash == comparer.GetHashCode(second) && hash == comparer.GetHashCode(first) ? 17 : 31;
        }
        public static int Nulls(int a,int b)
        {
            try { return StringComparer.Ordinal.GetHashCode((string)null); }
            catch (ArgumentNullException e) { return (e.ParamName == "obj" ? 7 : 11) + e.Message.Length; }
        }
        public static int Interfaces(int a,int b)
        {
            IEqualityComparer<string> comparer = StringComparer.Ordinal;
            string value = new string(new char[] { (char)a, (char)b });
            int result = comparer.GetHashCode(value) == StringComparer.Ordinal.GetHashCode(value) ? 3 : 5;
            try { comparer.GetHashCode(null); }
            catch (ArgumentNullException e) { result += e.ParamName == "obj" ? 7 : 11; }
            return result;
        }
        public static int Dictionary(int a,int b)
        {
            var table = new Dictionary<OrdinalHashId,int>();
            table.TryAdd(new OrdinalHashId("a\0\ud800"),a);
            table.TryAdd(default,b);
            for (int i=0;i<20;++i) table.TryAdd(new OrdinalHashId(i.ToString()),i);
            var same = new OrdinalHashId(string.Concat("a","\0\ud800"));
            bool added = table.TryAdd(same,17); table.TryGetValue(same,out int value);
            bool removed = table.Remove(same); table.TryGetValue(default,out int empty);
            return unchecked(value * 3 + empty * 7 + table.Count * 11 + (added ? 13 : 17) + (removed ? 19 : 23));
        }
        public static int Evaluation(int a,int b)
        {
            var trace = new OrdinalHashTrace { Value = b < 0 ? null : new string(new char[] { (char)a }) };
            int result;
            try { int hash = StringComparer.Ordinal.GetHashCode(trace.Read(1)); result = hash == StringComparer.Ordinal.GetHashCode(trace.Read(2)) ? 3 : 5; }
            catch (ArgumentNullException) { result = 7; }
            return trace.Order * 11 + result;
        }
        public static int ReceiverNull(int a,int b)
        {
            var trace = new OrdinalHashTrace { Value = b < 0 ? null : "value" };
            StringComparer comparer = null;
            try { comparer.GetHashCode(trace.Read(1)); return 17; }
            catch (NullReferenceException) { return trace.Order + 31; }
            catch (ArgumentNullException) { return trace.Order + 53; }
        }
        public static int RawLength(int a,int b)
        {
            int length = a % 65; if (length < 0) length = -length;
            var units = new char[length];
            for (int i=0;i<length;++i) units[i] = (char)(b+i);
            return StringComparer.Ordinal.GetHashCode(new string(units));
        }
        public static int RawUnits(int a,int b) => StringComparer.Ordinal.GetHashCode(new string(new char[] { (char)a, '\0', (char)b, '\ud800', '\udfff', 'z' }));
        public static int UnsupportedObject(int a,int b) => StringComparer.Ordinal.GetHashCode((object)"x");
        public static int UnsupportedComparerHash(int a,int b) => StringComparer.Ordinal.GetHashCode();
        public static int UnsupportedString(int a,int b) => "x".GetHashCode();
    }
}
