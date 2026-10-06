using System;

namespace Cilica.Fixtures.Execution
{
    public enum NullableKind { Zero, One }
    public enum NullableSmallKind : byte { Zero, One }
    internal enum NullableInternalKind { Zero, One }
    public sealed class NullableInternalEnumHolder { internal NullableInternalKind Value; }
    public static class NullableStaticValues { public static readonly NullableId Value = new NullableId(17); }
    public struct NullableEmpty { }
    public struct NullableWide { public long Value; }
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
        public static int NamedBoxRoundtrip(int a, int b)
        {
            NullableId? value = a == 0 ? default(NullableId?) : new NullableId(a);
            object boxed = value;
            value = null;
            var noise = new NullableNode(b);
            NullableId? copy = (NullableId?)boxed;
            return unchecked((boxed == null ? 1 : 0) + (copy.HasValue ? 2 : 0)
                + copy.GetValueOrDefault().Value + noise.Value);
        }
        public static int NamedBoxIdentity(int a, int b)
        {
            NullableId? value = new NullableId(a);
            object first = value;
            object second = value;
            object ordinary = new NullableId(b);
            return first != second && first.GetType() == ordinary.GetType()
                ? unchecked(((NullableId)first).Value + ((NullableId?)ordinary).Value.Value) : 999;
        }
        public static int ReferenceBoxRoundtrip(int a, int b)
        {
            var value = Make(a);
            object boxed = value;
            value = null;
            var noise = new NullableNode(b);
            var copy = ((NullablePayload?)boxed).Value;
            copy.Value = b;
            copy.Node.Value = unchecked(a + b);
            var again = ((NullablePayload?)boxed).Value;
            return unchecked(again.Value + again.Node.Value + copy.Value + noise.Value);
        }
        public static int NullableBoxArray(int a, int b)
        {
            var array = new object[3];
            array[0] = Make(a);
            array[1] = default(NullablePayload?);
            array[2] = new NullablePayload(new NullableNode(b), b);
            var first = (NullablePayload?)array[0];
            var empty = (NullablePayload?)array[1];
            var second = (NullablePayload?)array[2];
            array[0] = null; array[2] = null;
            var noise = new NullableNode(a);
            return unchecked(first.Value.Node.Value + second.Value.Node.Value + noise.Value + (empty.HasValue ? 1 : 0));
        }
        public static int EmptyBoxIdentity(int a, int b)
        {
            NullableEmpty? present = new NullableEmpty();
            object first = present;
            object second = present;
            object empty = default(NullableEmpty?);
            return first != null && first != second && empty == null
                && ((NullableEmpty?)first).HasValue && !((NullableEmpty?)empty).HasValue ? a : b;
        }
        public static int WideBoxRoundtrip(int a, int b)
        {
            NullableWide? value = new NullableWide { Value = unchecked(((long)a << 32) | (uint)b) };
            object boxed = value;
            var copy = (NullableWide?)boxed;
            return unchecked((int)(copy.Value.Value >> 32) + (int)copy.Value.Value);
        }
        public static int WrongNullableBox(int a, int b)
        {
            object boxed = a == 0 ? (object)new NullableNode(b) : (object)new NullableWide { Value = a };
            try { return ((NullableId?)boxed).Value.Value; }
            catch (InvalidCastException error)
            {
                string expected = a == 0
                    ? "Unable to cast object of type 'Cilica.Fixtures.Execution.NullableNode' to type 'System.Nullable`1[Cilica.Fixtures.Execution.NullableId]'."
                    : "Unable to cast object of type 'Cilica.Fixtures.Execution.NullableWide' to type 'System.Nullable`1[Cilica.Fixtures.Execution.NullableId]'.";
                return error.Message == expected ? b : unchecked(b + 1);
            }
        }
        public static int NullableGuidBox(int a, int b)
        {
            var expected = new Guid(a, unchecked((short)b), 17, 1, 2, 3, 4, 5, 6, 7, 8);
            Guid? value = expected;
            object boxed = value;
            value = null;
            return ((Guid?)boxed).Value == expected && (Guid)boxed == expected ? a : b;
        }
        public static int LiftedConversions(int a, int b)
        {
            int? input = a == 0 ? default(int?) : a;
            long? widened = input;
            int? narrowed = unchecked((int?)widened);
            int? sum = input + b;
            bool less = input < b;
            return unchecked(narrowed.GetValueOrDefault(b) + sum.GetValueOrDefault()
                + (less ? 1 : 0) + (input == narrowed ? 2 : 0));
        }
        public static int CheckedNullableConversion(int a, int b)
        {
            long? value = a == 0 ? default(long?) : unchecked(((long)a << 32) | (uint)b);
            try { return checked((int?)value).GetValueOrDefault(b); }
            catch (OverflowException) { return a; }
        }
        public static int MixedCastMessages(int a, int b)
        {
            object boxed = new NullableWide { Value = a };
            int result = 0;
            try { result = ((NullableId)boxed).Value; }
            catch (InvalidCastException error)
            {
                if (error.Message == "Unable to cast object of type 'Cilica.Fixtures.Execution.NullableWide' to type 'Cilica.Fixtures.Execution.NullableId'.") result = a;
            }
            try { result = ((NullableId?)boxed).Value.Value; }
            catch (InvalidCastException error)
            {
                if (error.Message == "Unable to cast object of type 'Cilica.Fixtures.Execution.NullableWide' to type 'System.Nullable`1[Cilica.Fixtures.Execution.NullableId]'.") result = unchecked(result + b);
            }
            return result;
        }
        public static int GenericNullableBox(int a, int b)
        {
            NullableContainer<NullablePayload>? value = new NullableContainer<NullablePayload> { Value = Make(a) };
            object boxed = value;
            value = null;
            var noise = new NullableNode(b);
            var copy = ((NullableContainer<NullablePayload>?)boxed).Value;
            return unchecked(copy.Value.Value.Node.Value + noise.Value);
        }
        public static int NullableBoxBudget(int a, int b)
        {
            NullableEmpty? value = b == 0 ? default(NullableEmpty?) : new NullableEmpty();
            return (object)value == null ? a : ~a;
        }
        public static int UnsupportedNullableEnumBox(int a, int b) { NullableKind? value = (NullableKind)a; return (object)value == null ? a : b; }
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
