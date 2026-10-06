using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class RemainderComparer : Comparer<int>
    {
        public override int Compare(int x, int y) { x %= 7; y %= 7; return x < y ? -1 : x > y ? 1 : 0; }
    }
    public enum ComparerEnum { Zero, One }
    public static class IntegerComparerProbe
    {
        public static int Signed(int a, int b) => Comparer<int>.Default.Compare(a, b);
        public static int Unsigned(int a, int b) => Comparer<uint>.Default.Compare((uint)a, (uint)b);
        public static int Wide(int a, int b) => Comparer<long>.Default.Compare(((long)a << 32) | (uint)b, ((long)b << 32) | (uint)a);
        public static int UnsignedWide(int a, int b) => Comparer<ulong>.Default.Compare(((ulong)(uint)a << 32) | (uint)b, ((ulong)(uint)b << 32) | (uint)a);
        public static int Typed(int a, int b) { IComparer<int> c = Comparer<int>.Default; return c.Compare(a, b); }
        public static int Custom(int a, int b) { Comparer<int> c = a < 0 ? (Comparer<int>)new RemainderComparer() : Comparer<int>.Default; return c.Compare(a, b); }
        public static int Null(int a, int b) { Comparer<int> c = a < 0 ? null! : Comparer<int>.Default; try { return c.Compare(a, b); } catch (NullReferenceException) { return 13; } }
        public static int Identity(int a, int b)
        {
            object signed = Comparer<int>.Default, unsigned = Comparer<uint>.Default;
            object wide = Comparer<long>.Default, unsignedWide = Comparer<ulong>.Default;
            return signed == (object)Comparer<int>.Default && unsigned == (object)Comparer<uint>.Default
                && wide == (object)Comparer<long>.Default && unsignedWide == (object)Comparer<ulong>.Default
                && signed != unsigned && signed != wide && wide != unsignedWide ? 17 : 0;
        }
        public static int UnsupportedString(int a, int b) => Comparer<string>.Default.Compare("a", "b");
        public static int UnsupportedNullable(int a, int b) => Comparer<int?>.Default == null ? 0 : 1;
        public static int UnsupportedEnum(int a, int b) => Comparer<ComparerEnum>.Default.Compare((ComparerEnum)a, (ComparerEnum)b);
        public static int UnsupportedByte(int a, int b) => Comparer<byte>.Default.Compare((byte)a, (byte)b);
        public static int UnsupportedGuid(int a, int b) => Comparer<Guid>.Default == null ? 0 : 1;
        public static int UnsupportedFactoryOnly(int a, int b) => Comparer<object>.Default == null ? 0 : 1;
        public static int UnsupportedObject(int a, int b) => ((System.Collections.IComparer)Comparer<int>.Default).Compare(a, b);
        public static int UnsupportedHash(int a, int b) => Comparer<int>.Default.GetHashCode();
    }
}
