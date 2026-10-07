using System;
namespace Cilica.Fixtures.Execution
{
    internal static class AssemblyNumbers { internal static int Number; internal static long Wide; }
    internal static class AssemblyReference { internal static ValueVectorBox Value; }
    internal static class AssemblyValue { internal static ValueVectorCell Value; }
    internal static class AssemblyGeneric<T> { internal static T Value; }
    public class FamilyAssemblyStatic { protected internal static int Value; }
    internal static class AssemblyReadonly
    {
        internal static readonly ValueVectorBox Root = new ValueVectorBox(19);
        internal static readonly int Seed = 17;
    }
    internal static class AssemblyFailedInitialization
    {
        internal static readonly int Value = Fail();
        private static int Fail() { throw new InvalidOperationException("initialization"); }
    }
    public static class AssemblyStaticOuter
    {
        private static class Hidden { internal static int Value; }
        public static int ReadHidden(int a,int b) { Hidden.Value=a; return unchecked(Hidden.Value+b); }
    }
    public static class AssemblyStaticProbe
    {
        public static int Numbers(int a,int b)
        {
            AssemblyNumbers.Number=a; AssemblyNumbers.Wide=((long)b<<32)+(uint)a;
            return unchecked(AssemblyNumbers.Number+(int)(AssemblyNumbers.Wide>>32));
        }
        public static int References(int a,int b)
        {
            AssemblyReference.Value=new ValueVectorBox(a);var saved=AssemblyReference.Value;
            var pressure=new ValueVectorBox(b);saved.Number=unchecked(saved.Number+pressure.Number);
            int result=AssemblyReference.Value.Number;AssemblyReference.Value=null;return result;
        }
        private static ValueVectorCell Zero() => default;
        public static int Values(int a,int b)
        {
            ValueVectorCell v=default;v.Number=a;v.Wide=(long)b<<32;v.Reference=new ValueVectorBox(b);AssemblyValue.Value=v;
            v.Number=b;var pressure=new ValueVectorBox(31);var copy=AssemblyValue.Value;AssemblyValue.Value=Zero();
            return unchecked(copy.Number+(int)(copy.Wide>>32)+copy.Reference.Number+pressure.Number);
        }
        public static int Closed(int a,int b)
        {
            AssemblyGeneric<int>.Value=a; AssemblyGeneric<ValueVectorBox>.Value=new ValueVectorBox(b);
            var saved=AssemblyGeneric<ValueVectorBox>.Value;AssemblyGeneric<ValueVectorBox>.Value=null;
            return unchecked(AssemblyGeneric<int>.Value+saved.Number);
        }
        public static int Readonly(int a,int b)
        {
            var first=AssemblyReadonly.Root;var pressure=new ValueVectorBox(b);first.Number=a;
            return unchecked(AssemblyReadonly.Seed+AssemblyReadonly.Root.Number+pressure.Number+(ReferenceEquals(first,AssemblyReadonly.Root)?7:-100));
        }
        public static int FamilyAssembly(int a,int b) { FamilyAssemblyStatic.Value=a;return unchecked(FamilyAssemblyStatic.Value+b); }
        public static int Nested(int a,int b) => AssemblyStaticOuter.ReadHidden(a,b);
        public static int FailedInitialization(int a,int b)
        {
            try { return AssemblyFailedInitialization.Value; }
            catch(TypeInitializationException e) { return e.InnerException is InvalidOperationException && e.InnerException.Message=="initialization" ? unchecked(a+b) : -100; }
        }
    }
}
