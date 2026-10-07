using System;
namespace Cilica.Fixtures.Execution
{
    internal struct HiddenValuePayload
    {
        public int Number;
        public long Wide;
        public ValueVectorBox Reference;
    }
    internal readonly struct HiddenReadonlyPayload
    {
        internal HiddenReadonlyPayload(int number,ValueVectorBox reference) { Number=number;Reference=reference; }
        internal int Number { get; }
        internal ValueVectorBox Reference { get; }
    }
    internal struct HiddenValueHolder<T> { public T Value; }
    public static class HiddenValueGenericProbe
    {
        private struct Payload { public int Number; public ValueVectorBox Reference; }
        private static HiddenValuePayload Echo(HiddenValuePayload value) => EnumGenericTransport<HiddenValuePayload>.Copy(value);
        public static int Copy(int a,int b)
        {
            HiddenValuePayload value=default;value.Number=a;value.Wide=((long)a<<32)|(uint)b;value.Reference=new ValueVectorBox(b);
            var result=EnumGenericTransport<HiddenValuePayload>.Copy(value);value.Number=17;
            return unchecked(result.Number+(int)(result.Wide>>32)+(int)result.Wide+result.Reference.Number+(result.Reference==value.Reference?19:23));
        }
        public static int Delegate(int a,int b)
        {
            HiddenValuePayload value=default;value.Number=a;value.Wide=((long)a<<32)|(uint)b;value.Reference=new ValueVectorBox(b);
            Func<HiddenValuePayload,HiddenValuePayload> callback=Echo;var result=callback(value);var pressure=new ValueVectorBox(29);
            return unchecked(result.Number+(int)result.Wide+result.Reference.Number+pressure.Number);
        }
        public static int Constructed(int a,int b)
        {
            HiddenValueHolder<ValueVectorBox> value=default;value.Value=new ValueVectorBox(a);
            var result=EnumGenericTransport<HiddenValueHolder<ValueVectorBox>>.Copy(value);
            return result.Value==value.Value?unchecked(result.Value.Number+b):-997;
        }
        public static int PrivateNullable(int a,int b)
        {
            Payload value=default;value.Number=a;value.Reference=new ValueVectorBox(b);
            Payload? optional=a<0?(Payload?)null:value;
            var result=EnumGenericTransport<Payload>.RoundTrip(optional);
            return result.HasValue?unchecked(result.Value.Number+result.Value.Reference.Number):b;
        }
        public static int PrivateBox(int a,int b)
        {
            Payload value=default;value.Number=a;value.Reference=new ValueVectorBox(b);
            object boxed=EnumGenericTransport<Payload>.Box(value);var pressure=new ValueVectorBox(31);var result=(Payload)boxed;
            return unchecked(result.Number+result.Reference.Number+pressure.Number+(result.Reference==value.Reference?37:41));
        }
        private static ValueVectorBox Bind(ValueVectorBox node,HiddenReadonlyPayload context)
            => new ValueVectorBox(unchecked(context.Number+context.Reference.Number+(node==context.Reference?43:47)));
        public static int ReadonlyDelegate(int a,int b)
        {
            var node=new ValueVectorBox(b);var context=new HiddenReadonlyPayload(a,node);
            Func<ValueVectorBox,HiddenReadonlyPayload,ValueVectorBox> bind=Bind;
            return bind(node,context).Number;
        }
        public static int PublicFields(int a,int b) => HiddenEnumGenericProbe.StructCopy(a,b);
    }
}
