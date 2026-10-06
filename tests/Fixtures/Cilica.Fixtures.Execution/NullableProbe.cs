using System;

namespace Cilica.Fixtures.Execution
{
    public enum NullableKind { Zero, One }
    public enum NullableSmallKind : byte { Zero, One }
    internal enum NullableInternalKind { Zero, One }
    public sealed class NullableInternalEnumHolder { internal NullableInternalKind Value; }
    public static class NullableStaticValues { public static readonly NullableId Value = new NullableId(17); }
    public struct NullableId
    {
        public int Value;
        public NullableId(int value) { Value = value; }
    }
    public sealed class NullableNode
    {
        public int Value;
        public NullableNode(int value) { Value = value; }
    }
    public struct NullablePayload
    {
        public NullableNode Node;
        public int Value;
        public NullablePayload(NullableNode node, int value) { Node = node; Value = value; }
    }
    public sealed class NullableHolder { public NullablePayload? Value; }
    public struct NullableContainer<T> where T : struct { public T? Value; }

    public static class NullableProbe
    {
        private static T? Copy<T>(T? value) where T : struct => value;
        private static NullablePayload? Make(int value) => new NullablePayload(new NullableNode(value), value);
        private static int Fallback(ref int calls, int value) { calls++; return value; }

        public static int Scalars(int a, int b)
        {
            int? empty = default;
            int? present = new int?(a);
            int? copy = Copy(present);
            present = default;
            return unchecked((empty.HasValue ? 1 : 0) + (present.HasValue ? 2 : 0)
                + empty.GetValueOrDefault() + empty.GetValueOrDefault(b) + copy.Value + copy.GetValueOrDefault(b));
        }
        public static int WideAndNarrow(int a, int b)
        {
            long? wide = new long?(unchecked(((long)a << 32) | (uint)b));
            long? empty = default;
            bool? flag = new bool?(a < b);
            byte? small = new byte?(unchecked((byte)a));
            char? letter = new char?(unchecked((char)b));
            return unchecked((int)(wide.Value >> 32) + (int)wide.GetValueOrDefault()
                + (int)empty.GetValueOrDefault(b) + (flag.Value ? 1 : 0) + small.Value + letter.Value);
        }
        public static int UnsignedAndSmall(int a, int b)
        {
            uint? unsigned = new uint?(unchecked((uint)a));
            ulong? wide = new ulong?(((ulong)(uint)a << 32) | (uint)b);
            sbyte? signedByte = new sbyte?(unchecked((sbyte)a));
            short? signedShort = new short?(unchecked((short)b));
            ushort? unsignedShort = new ushort?(unchecked((ushort)a));
            return unchecked((int)unsigned.Value + (int)(wide.Value >> 32) + (int)wide.Value
                + signedByte.Value + signedShort.Value + unsignedShort.Value);
        }
        public static int BoxContainer(int a, int b)
        {
            var container = new NullableContainer<NullablePayload>();
            container.Value = Make(a);
            object boxed = container;
            container.Value = null;
            var noise = new NullableNode(b);
            var copy = (NullableContainer<NullablePayload>)boxed;
            return unchecked(copy.Value.Value.Node.Value + copy.Value.Value.Value + noise.Value);
        }
        public static int Enums(int a, int b)
        {
            NullableKind? value = new NullableKind?((NullableKind)a);
            NullableKind? empty = default;
            return unchecked((int)Copy(value).Value + (int)empty.GetValueOrDefault((NullableKind)b));
        }
        public static int IdsAndGenerics(int a, int b)
        {
            var container = new NullableContainer<NullableId>();
            container.Value = new NullableId(a);
            var copy = Copy(container.Value);
            var payload = copy.Value;
            payload.Value = b;
            container.Value = null;
            return unchecked(copy.Value.Value + payload.Value + container.Value.GetValueOrDefault().Value);
        }
        public static int GuidValues(int a, int b)
        {
            var expected = new Guid(a, unchecked((short)b), unchecked((short)(a >> 16)),
                unchecked((byte)b), unchecked((byte)(b >> 8)), unchecked((byte)a), unchecked((byte)(a >> 8)),
                unchecked((byte)(a >> 16)), unchecked((byte)(a >> 24)), unchecked((byte)(b >> 16)), unchecked((byte)(b >> 24)));
            Guid? present = new Guid?(expected);
            Guid? copy = Copy(present);
            present = null;
            Guid? empty = default;
            Guid? zero = new Guid?(default(Guid));
            return !present.HasValue && copy.HasValue && copy.Value == expected
                && !empty.HasValue && empty.GetValueOrDefault(expected) == expected
                && zero.HasValue && zero.Value == empty.GetValueOrDefault() ? a : unchecked(a + 1);
        }
        public static int ReferenceCopies(int a, int b)
        {
            var value = Make(a);
            var copy = Copy(value);
            value = null;
            var noise = new NullableNode(b);
            var payload = copy.Value;
            payload.Value = noise.Value;
            payload.Node.Value = unchecked(payload.Node.Value + b);
            var second = copy.GetValueOrDefault();
            return unchecked(second.Node.Value + second.Value + payload.Value + (second.Node == payload.Node ? 1 : 0));
        }
        public static int FieldsAndArrays(int a, int b)
        {
            var holder = new NullableHolder();
            var original = Make(a);
            holder.Value = original;
            original = null;
            var array = new NullablePayload?[2];
            array[0] = holder.Value;
            holder.Value = null;
            var noise = new NullableNode(b);
            var copy = array[0].Value;
            array[0] = null;
            return unchecked(copy.Node.Value + copy.Value + noise.Value + (array[1].HasValue ? 1 : 0));
        }
        public static int ReferenceDefaults(int a, int b)
        {
            NullablePayload? empty = default;
            NullablePayload? present = new NullablePayload(null, a);
            var fallback = new NullablePayload(new NullableNode(b), b);
            return unchecked((present.HasValue ? 1 : 0) + (empty.GetValueOrDefault().Node == null ? 2 : 0)
                + empty.GetValueOrDefault(fallback).Node.Value + present.GetValueOrDefault(fallback).Value);
        }
        public static int ValueFault(int a, int b)
        {
            int? value = a == 0 ? default(int?) : new int?(a);
            try { return value.Value; }
            catch (InvalidOperationException error)
            {
                return error.Message == "Nullable object must have a value." ? b : unchecked(b + 1);
            }
        }
        public static int ArgumentOrder(int a, int b)
        {
            int? present = new int?(a);
            int calls = 0;
            int selected = present.GetValueOrDefault(Fallback(ref calls, b));
            return unchecked(selected + calls);
        }
        public static int BoxLookalike(int a, int b)
        {
            object value = new Decoy.Nullable<int> { Value = a };
            return unchecked(((Decoy.Nullable<int>)value).Value + b);
        }
        public static int UnsupportedHeapReturn(int a, int b)
        {
            var holder = new NullableHolder();
            holder.Value = Make(a);
            return holder.Value.Value.Value;
        }
        private static int ReadId(NullableId value) => value.Value;
        public static int UnsupportedStaticInline(int a, int b) => ReadId(NullableStaticValues.Value);
        public static int UnsupportedEnumVisibility(int a, int b) { var holder = new NullableInternalEnumHolder(); holder.Value = (NullableInternalKind)a; return (int)holder.Value; }
        public static int UnsupportedEnumWidth(int a, int b) { NullableSmallKind? value = new NullableSmallKind?((NullableSmallKind)a); return (int)value.Value; }
        public static int UnsupportedBox(int a, int b) { int? value = a; object boxed = value; return boxed == null ? a : b; }
        public static int UnsupportedEmptyBox(int a, int b) { int? value = default; object boxed = value; return boxed == null ? a : b; }
        public static int UnsupportedUnbox(int a, int b) { object value = new NullableNode(a); return ((int?)value).GetValueOrDefault(b); }
        public static int UnsupportedText(int a, int b) { int? value = a; return value.ToString().Length; }
        public static int UnsupportedHash(int a, int b) { int? value = a; return value.GetHashCode(); }
        public static int UnsupportedEquality(int a, int b) { int? value = a; return value.Equals(null) ? a : b; }
    }
}

namespace Cilica.Fixtures.Decoy
{
    public struct Nullable<T> { public T Value; }
}
