using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public struct ComparerValue : IEquatable<ComparerValue>
    {
        public long Number; public ReferenceKeyTrace Trace;
        public bool Equals(ComparerValue other) { Trace.Value++; _ = new WideCell(Number); long before = Number++; if (other.Number == 0) throw new InvalidOperationException("equal"); return before == other.Number; }
        public override int GetHashCode() { Trace.Value += 3; _ = new WideCell(Number); return unchecked((int)Number ^ (int)(Number++ >> 32)); }
    }
    public sealed class CustomEqualityComparer : EqualityComparer<int>
    {
        public override bool Equals(int x, int y) => x % 7 == y % 7;
        public override int GetHashCode(int value) => value % 7;
    }
    public static class EqualityComparerProbe
    {
        public static int Signed(int a, int b) => EqualityComparer<int>.Default.GetHashCode(a) ^ (EqualityComparer<int>.Default.Equals(a,b) ? 17 : 31);
        public static int Unsigned(int a, int b) => EqualityComparer<uint>.Default.GetHashCode((uint)a) ^ (EqualityComparer<uint>.Default.Equals((uint)a,(uint)b) ? 17 : 31);
        public static int Wide(int a, int b)
        { long x = ((long)a << 32) | (uint)b, y = ((long)b << 32) | (uint)a; return EqualityComparer<long>.Default.GetHashCode(x) ^ (EqualityComparer<long>.Default.Equals(x,y) ? 17 : 31); }
        public static int UnsignedWide(int a, int b)
        { ulong x = ((ulong)(uint)a << 32) | (uint)b, y = ((ulong)(uint)b << 32) | (uint)a; return EqualityComparer<ulong>.Default.GetHashCode(x) ^ (EqualityComparer<ulong>.Default.Equals(x,y) ? 17 : 31); }
        public static int Values(int a, int b)
        {
            var trace = new ReferenceKeyTrace(); var x = new ComparerValue { Number = a, Trace = trace }; var y = new ComparerValue { Number = b, Trace = trace };
            var comparer = EqualityComparer<ComparerValue>.Default; int result = 0;
            try { result = comparer.Equals(x,y) ? 17 : 31; } catch (InvalidOperationException) { result = 43; }
            return unchecked(result + comparer.GetHashCode(x) + trace.Value * 7 + (int)x.Number);
        }
        public static int References(int a, int b)
        {
            var trace = new ReferenceKeyTrace(); var x = a < 0 ? null : new TypedReferenceKey { Id = a, Trace = trace }; var y = b < 0 ? null : new TypedReferenceKey { Id = b, Trace = trace };
            var comparer = EqualityComparer<TypedReferenceKey>.Default;
            return unchecked((comparer.Equals(x,y) ? 17 : 31) + comparer.GetHashCode(x) + trace.Value * 7);
        }
        public static int Typed(int a, int b) { IEqualityComparer<int> c = EqualityComparer<int>.Default; return c.GetHashCode(a) ^ (c.Equals(a,b) ? 17 : 31); }
        public static int Custom(int a, int b) { var selected = EqualityComparer<int>.Default; EqualityComparer<int> c = a < 0 ? (EqualityComparer<int>)new CustomEqualityComparer() : selected; return c.GetHashCode(a) ^ (c.Equals(a,b) ? 17 : 31); }
        public static int Null(int a, int b) { var selected = EqualityComparer<int>.Default; EqualityComparer<int> c = a < 0 ? null : selected; try { return c.Equals(a,b) ? 17 : 31; } catch (NullReferenceException) { return 43; } }
        public static int Identity(int a, int b)
        {
            object signed = EqualityComparer<int>.Default, unsigned = EqualityComparer<uint>.Default;
            object wide = EqualityComparer<long>.Default, unsignedWide = EqualityComparer<ulong>.Default;
            return signed == (object)EqualityComparer<int>.Default && unsigned == (object)EqualityComparer<uint>.Default
                && wide == (object)EqualityComparer<long>.Default && unsignedWide == (object)EqualityComparer<ulong>.Default
                && signed != unsigned && signed != wide && wide != unsignedWide ? 17 : 0;
        }
        public static int PrimitiveEquals(int a, int b) => (a.Equals(b) ? 1 : 0) + (((uint)a).Equals((uint)b) ? 3 : 0) + (((long)a).Equals((long)b) ? 5 : 0) + (((ulong)(uint)a).Equals((ulong)(uint)b) ? 7 : 0);
        public static int UnsupportedString(int a, int b) => EqualityComparer<string>.Default == null ? 0 : 1;
        public static int UnsupportedNullable(int a, int b) => EqualityComparer<int?>.Default == null ? 0 : 1;
        public static int UnsupportedEnum(int a, int b) => EqualityComparer<ComparerEnum>.Default == null ? 0 : 1;
        public static int UnsupportedByte(int a, int b) => EqualityComparer<byte>.Default == null ? 0 : 1;
        public static int UnsupportedFallback(int a, int b) => EqualityComparer<PlainReferenceKey>.Default == null ? 0 : 1;
        public static int UnsupportedObject(int a, int b) => ((System.Collections.IEqualityComparer)EqualityComparer<int>.Default).Equals(a,b) ? 1 : 0;
        public static int UnsupportedHash(int a, int b) => EqualityComparer<int>.Default.GetHashCode();
    }
}
