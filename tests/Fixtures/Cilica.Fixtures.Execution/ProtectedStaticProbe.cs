using System;
namespace Cilica.Fixtures.Execution
{
    public class StaticFamilyRoot
    {
        protected static int Compute(int a,int b) { var box=new ValueVectorBox(a);return unchecked(box.Number+b); }
        protected static void Store(ValueVectorBox box,int a) { var pressure=new ValueVectorBox(a);box.Number=pressure.Number; }
        protected int Instance(int a) => a;
    }
    public class StaticFamilyMiddle : StaticFamilyRoot { }
    public sealed class StaticFamilyLeaf : StaticFamilyMiddle
    {
        public int Number;
        public StaticFamilyLeaf(int a,int b) { Number=Compute(a,b); }
        public static int Invoke(int a,int b) => Compute(a,b);
        public static int Write(int a,int b) { var box=new ValueVectorBox(b);Store(box,a);return unchecked(box.Number+b); }
        public int UnsupportedInstance(int a) => Instance(a);
        public static class Nested { public static int Invoke(int a,int b) => Compute(a,b); }
    }
    public class StaticIntersectionRoot
    {
        private protected static string Require(string text)
        { if(text==null)throw new ArgumentException("missing","text");return text; }
    }
    public sealed class StaticIntersectionLeaf : StaticIntersectionRoot
    { public static string Invoke(string text) => Require(text); }
    public class StaticGenericRoot<T> { protected static T Keep(T value) => value; }
    public sealed class StaticGenericLeaf<T> : StaticGenericRoot<T>
    { public static T Invoke(T value) => Keep(value); }
    internal class StaticHiddenRoot { protected static int Compute(int a,int b) => unchecked(a-b); }
    internal sealed class StaticHiddenLeaf : StaticHiddenRoot
    { public static int Invoke(int a,int b) => Compute(a,b); }
    public static class ProtectedStaticProbe
    {
        public static int Constructor(int a,int b) => new StaticFamilyLeaf(a,b).Number;
        public static int Void(int a,int b) => StaticFamilyLeaf.Write(a,b);
        public static int Deep(int a,int b) => StaticFamilyLeaf.Invoke(a,b);
        public static int Intersection(int a,int b)
        { try{return StaticIntersectionLeaf.Invoke((a&1)==0?null:"a\0z").Length+b;}catch(ArgumentException e){return unchecked(e.Message.Length+e.ParamName.Length+b);} }
        public static int Generic(int a,int b)
        { var value=new ValueVectorBox(a);var result=StaticGenericLeaf<ValueVectorBox>.Invoke(value);var pressure=new ValueVectorBox(b);return result==value?unchecked(result.Number+pressure.Number):-999; }
        public static int Hidden(int a,int b) => StaticHiddenLeaf.Invoke(a,b);
        public static int UnsupportedNested(int a,int b) => StaticFamilyLeaf.Nested.Invoke(a,b);
        public static int UnsupportedInstance(int a,int b) => new StaticFamilyLeaf(a,b).UnsupportedInstance(a);
    }
}
