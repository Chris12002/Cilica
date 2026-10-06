using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace Cilica.Fixtures.Execution
{
    // Generic conformance probes; no application sources or saved-game data.
    public sealed class ProbeEnumHolder { public ProbeKind Value; }
    public delegate int RefReader(ProbeState state);
    public enum ProbeKind { First, Second }
    public sealed class ProbeState { public int Value; public ProbeState(int value) { Value = value; } }
    public delegate int IntBinary(int left, int right);
    public delegate ProbeState RefHandler(ProbeState state, int amount);
    public static class CompatibilityGapProbe
    {
        private static int Add(int a, int b) => unchecked(a + b);
        private static ProbeState Update(ProbeState state, int amount) => new ProbeState(unchecked(state.Value + amount));
        public static int Probe_ControlArithmetic(int a, int b) => unchecked(a + b);
        public static int Probe_ControlTypeKey(int a, int b) { var d = new Dictionary<Type,int>(); d.TryAdd(typeof(ProbeState), a); d.TryGetValue(new ProbeState(b).GetType(), out var value); return value; }
        public static int Probe_ControlList(int a, int b) { var list = new List<int>(); list.Add(a); return list[0]; }
        public static int Probe_ControlWide(int a, int b) => unchecked((int)((long)a * b));
        public static int Probe_ControlDelegate(int a, int b) { var call = new IntBinary(Add); return call(a,b); }
        public static int Probe_NullableInt(int a, int b) { int? value=a>0?a:(int?)null; return value.GetValueOrDefault(b); }
        public static int Probe_NullableGuid(int a, int b) { Guid? value=default; return value.HasValue?a:b; }
        public static int Probe_EnumLocal(int a, int b) { var value=(ProbeKind)a; return value==ProbeKind.First?b:a; }
        public static int Probe_EnumArray(int a, int b) { var values=new ProbeKind[1]; values[0]=(ProbeKind)a; return (int)values[0]; }
        public static int Probe_DictionaryAdd(int a, int b) { var d=new Dictionary<int,int>();d.Add(a,b);return d.Count; }
        public static int Probe_DictionaryRead(int a, int b) { var d=new Dictionary<int,int>();d[a]=b;return d[a]; }
        public static int Probe_DictionaryRemove(int a, int b) { var d=new Dictionary<int,int>();d.TryAdd(a,b);return d.Remove(a)?1:0; }
        public static int Probe_DictionaryKeys(int a, int b) { var d=new Dictionary<int,int>();d.TryAdd(a,b);return d.Keys.Count; }
        public static int Probe_DictionaryForeach(int a, int b) { var d=new Dictionary<int,int>();d.TryAdd(a,b);int sum=0;foreach(var pair in d)sum+=pair.Value;return sum; }
        public static int Probe_ListForeach(int a, int b) { var list=new List<int>();list.Add(a);int sum=0;foreach(var value in list)sum+=value;return sum; }
        public static int Probe_ListRemoveAt(int a, int b) { var list=new List<int>();list.Add(a);list.RemoveAt(0);return list.Count; }
        public static int Probe_ListAddRange(int a, int b) { var list=new List<int>();list.AddRange(new[]{a,b});return list.Count; }
        public static int Probe_ListContains(int a, int b) { var list=new List<int>();list.Add(a);return list.Contains(b)?1:0; }
        public static int Probe_ListSort(int a, int b) { var list=new List<int>();list.Add(a);list.Add(b);list.Sort();return list[0]; }
        public static int Probe_ListToArray(int a, int b) { var list=new List<int>();list.Add(a);return list.ToArray()[0]; }
        public static int Probe_HashSet(int a, int b) { var set=new HashSet<int>();set.Add(a);return set.Contains(b)?1:0; }
        public static int Probe_RefDelegate(int a, int b) { var call=new RefHandler(Update);return call(new ProbeState(a),b).Value; }
        public static int Probe_FuncDelegate(int a, int b) { var call=new Func<int,int,int>(Add);return call(a,b); }
        public static int Probe_LinqAny(int a, int b) => new[]{a,b}.Any()?1:0;
        public static int Probe_LinqWhere(int a, int b) => new[]{a,b}.Where(x=>x>0).Count();
        public static int Probe_ArrayAsReadOnly(int a, int b) => Array.AsReadOnly(new[]{a,b}).Count;
        public static int Probe_MathAbs64(int a, int b) => unchecked((int)Math.Abs((long)a));
        public static int Probe_MathMinMax(int a, int b) => Math.Max(a,b);
        public static int Probe_StringCompareOrdinal(int a, int b) => string.CompareOrdinal("a","b");
        public static int Probe_StringStartsWith(int a, int b) => "abc".StartsWith("a",StringComparison.Ordinal)?1:0;
        public static int Probe_StringSubstring(int a, int b) => "abc".Substring(1).Length;
        public static int Probe_StringFormatTwo(int a, int b) => string.Format("{0}:{1}",a,b).Length;
        public static int Probe_StringBuilderInt(int a, int b) { var builder=new StringBuilder();builder.Append(a);return builder.Length; }
        public static int Probe_TupleFive(int a, int b) { var value=(a,b,a,b,a);return value.Item5; }
        public static int Probe_ByteArray(int a, int b) { var bytes=new byte[2];bytes[0]=(byte)a;return bytes[0]; }
        public static int Probe_Utf8(int a, int b) => Encoding.UTF8.GetBytes("abc").Length;
        public static int Probe_Sha256(int a, int b) { using(var sha=SHA256.Create())return sha.ComputeHash(new byte[]{1,2})[0]; }
        private static int Read(ProbeState state) => state.Value;
        private static IEnumerable<int> Yield(int value) { yield return value; }
        public static int Probe_EnumField(int a, int b) { var holder=new ProbeEnumHolder();holder.Value=(ProbeKind)a;return (int)holder.Value; }
        public static int Probe_ReferenceArgument(int a, int b) { var call=new RefReader(Read);return call(new ProbeState(a)); }
        public static int Probe_ListEnumerableConstructor(int a, int b) => new List<int>(new[]{a,b}).Count;
        public static int Probe_EnumIsDefined(int a, int b) => Enum.IsDefined(typeof(ProbeKind),a)?1:0;
        public static int Probe_IntCompareTo(int a, int b) => a.CompareTo(b);
        public static int Probe_GuidTextConstructor(int a, int b) => new Guid("11111111-1111-1111-1111-111111111111").GetHashCode();
        public static int Probe_StaticArrayData(int a, int b) { var values=new[]{1,2,3,4};return values[a&3]; }
        public static int Probe_Iterator(int a, int b) { int result=0;foreach(var value in Yield(a))result+=value;return result; }
        public static int Probe_ArrayEnumeration(int a, int b) { IEnumerable<int> values=new[]{a,b};int result=0;foreach(var value in values)result+=value;return result; }
        public static int Probe_CharArrayString(int a, int b) { var values=new char[1];values[0]=(char)a;return new string(values).Length; }
    }
}
