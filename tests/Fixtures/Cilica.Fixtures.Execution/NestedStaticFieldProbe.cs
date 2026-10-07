using System;
namespace Cilica.Fixtures.Execution
{
    internal static class PrivateStaticNumbers
    {
        private static int Number;
        private static long Wide;
        internal static class Reader
        {
            internal static int Run(int a,int b)
            { Number=a; Wide=((long)b<<32)+(uint)a; return unchecked(Number+(int)(Wide>>32)); }
        }
    }
    internal static class PrivateStaticReferences
    {
        private static ValueVectorBox Value;
        internal static class Reader
        {
            internal static int Run(int a,int b)
            { Value=new ValueVectorBox(a);var saved=Value;var pressure=new ValueVectorBox(b);saved.Number=unchecked(saved.Number+pressure.Number);int result=Value.Number;Value=null;return result; }
        }
    }
    internal static class PrivateStaticValues
    {
        private static ValueVectorCell Value;
        internal static class Reader
        {
            private static ValueVectorCell Zero()=>default;
            internal static int Unsupported(int a,int b) { Value=default;return a+b; }
            internal static int Run(int a,int b)
            { ValueVectorCell v=default;v.Number=a;v.Wide=(long)b<<32;v.Reference=new ValueVectorBox(b);Value=v;v.Number=b;var pressure=new ValueVectorBox(31);var copy=Value;Value=Zero();return unchecked(copy.Number+(int)(copy.Wide>>32)+copy.Reference.Number+pressure.Number); }
        }
    }
    internal static class PrivateStaticReadonly
    {
        private static readonly ValueVectorBox Root=new ValueVectorBox(19);
        private static readonly int Seed=17;
        internal static class Reader
        {
            internal static int Run(int a,int b)
            { var first=Root;var pressure=new ValueVectorBox(b);first.Number=a;return unchecked(Seed+Root.Number+pressure.Number+(ReferenceEquals(first,Root)?7:-100)); }
        }
    }
    internal static class PrivateStaticDeep
    {
        private static int Number;
        internal static class Middle
        {
            private static long Wide;
            internal static class Reader
            {
                internal static int Run(int a,int b) { Number=a;Wide=(long)b<<32;return unchecked(Number+(int)(Wide>>32)); }
            }
        }
    }
    internal static class PrivateStaticFault
    {
        private static readonly int Value=Fail();
        private static int Fail() { throw new InvalidOperationException("initialization"); }
        internal static class Reader
        {
            internal static int Run(int a,int b)
            { try { return Value; } catch(TypeInitializationException e) { return e.InnerException is InvalidOperationException && e.InnerException.Message=="initialization" ? unchecked(a+b) : -100; } }
        }
    }
    public static class GenericPrivateStatic<T>
    {
        private static int Value;
        public static class Reader
        {
            public static int Run(int a,int b) { Value=a;return unchecked(Value+b); }
        }
    }
    public static class NestedStaticFieldProbe
    {
        public static int UnsupportedGeneric(int a,int b)=>GenericPrivateStatic<int>.Reader.Run(a,b);
        public static int UnsupportedAddress(int a,int b)=>PrivateStaticValues.Reader.Unsupported(a,b);
        public static int Numbers(int a,int b)=>PrivateStaticNumbers.Reader.Run(a,b);
        public static int References(int a,int b)=>PrivateStaticReferences.Reader.Run(a,b);
        public static int Values(int a,int b)=>PrivateStaticValues.Reader.Run(a,b);
        public static int Readonly(int a,int b)=>PrivateStaticReadonly.Reader.Run(a,b);
        public static int Deep(int a,int b)=>PrivateStaticDeep.Middle.Reader.Run(a,b);
        public static int Fault(int a,int b)=>PrivateStaticFault.Reader.Run(a,b);
    }
}
