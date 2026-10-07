using System;
namespace Cilica.Fixtures.Execution
{
    public class CloneBase
    {
        private int hidden;
        public long Wide;
        public CloneBase(int value) { hidden=value;Wide=(long)value<<32; }
        public int Hidden()=>hidden;
    }
    public sealed class CloneCell { public int Number; public CloneCell(int n) { Number=n; } }
    public struct CloneValue { public int Number;public long Wide;public CloneCell Reference; }
    public sealed class ClonePayload : CloneBase
    {
        public int Number;
        public readonly CloneCell Cell;
        public CloneValue Value;
        public int[] Array;
        public ClonePayload Self;
        public ClonePayload(int a,int b,CloneCell count) : base(b)
        { count.Number++;Number=a;Cell=new CloneCell(b);Value.Number=a;Value.Wide=(long)b<<32;Value.Reference=Cell;Array=new[]{a,b};Self=this; }
        public ClonePayload Copy()=>(ClonePayload)MemberwiseClone();
        public static ClonePayload CopyOther(ClonePayload value)=>(ClonePayload)value.MemberwiseClone();
    }
    public sealed class EmptyClonePayload
    { public EmptyClonePayload Copy()=>(EmptyClonePayload)MemberwiseClone(); }
    public sealed class GenericClonePayload<T>
    { public T Item;public GenericClonePayload(T item) { Item=item; } public GenericClonePayload<T> Copy()=>(GenericClonePayload<T>)MemberwiseClone(); }
    public class OpenClonePayload
    { public OpenClonePayload Copy()=>(OpenClonePayload)MemberwiseClone(); }
    public static class MemberwiseCloneProbe
    {
        private static ClonePayload retained;
        public static int Quota(int a,int b)
        {
            if(a<0) { retained=null;return 0; }
            if(a==0) { retained=new ClonePayload(b,b,new CloneCell(0));return 1; }
            return retained.Copy().Number;
        }
        public static int Fields(int a,int b)
        { var count=new CloneCell(0);var source=new ClonePayload(a,b,count);var copy=source.Copy();copy.Number=b;copy.Wide=(long)a<<32;return unchecked(source.Number+copy.Number*3+(int)(source.Wide>>32)*5+(int)(copy.Wide>>32)*7+copy.Hidden()*11); }
        public static int References(int a,int b)
        { var source=new ClonePayload(a,b,new CloneCell(0));var copy=source.Copy();copy.Cell.Number=a;copy.Array[0]=b;return unchecked(source.Cell.Number+source.Array[0]*3+(ReferenceEquals(source.Cell,copy.Cell)?5:0)+(ReferenceEquals(copy.Self,source)?7:0)+(ReferenceEquals(copy,source)?0:11)); }
        public static int Values(int a,int b)
        { var source=new ClonePayload(a,b,new CloneCell(0));var copy=source.Copy();copy.Value.Number=b;copy.Value.Reference.Number=a;return unchecked(source.Value.Number+copy.Value.Number*3+(int)(copy.Value.Wide>>32)*5+source.Value.Reference.Number*7); }
        public static int NoConstructor(int a,int b)
        { var count=new CloneCell(0);var source=new ClonePayload(a,b,count);var copy=source.Copy();return count.Number+(copy.GetType()==source.GetType()?3:0)+(ReferenceEquals(copy,source)?0:5); }
        public static int Repeated(int a,int b)
        { var source=new ClonePayload(a,b,new CloneCell(0));for(int i=0;i<20;i++){var copy=source.Copy();copy.Number=unchecked(copy.Number+1);source=copy;}return unchecked(source.Number+source.Cell.Number); }
        public static int Null(int a,int b)
        { ClonePayload value=a<0?null:new ClonePayload(a,b,new CloneCell(0));try{return ClonePayload.CopyOther(value).Number;}catch(NullReferenceException){return b;} }
        public static int Empty(int a,int b)
        { var source=new EmptyClonePayload();var copy=source.Copy();return ReferenceEquals(source,copy)?a:unchecked(a+b); }
        public static int Generic(int a,int b)
        { var source=new GenericClonePayload<CloneCell>(new CloneCell(a));var copy=source.Copy();copy.Item.Number=b;var number=new GenericClonePayload<long>(((long)a<<32)|(uint)b);var numeric=number.Copy();numeric.Item=b;return unchecked(source.Item.Number+(int)(number.Item>>32)+(int)numeric.Item); }
        public static int UnsupportedOpen(int a,int b) { var value=new OpenClonePayload();return ReferenceEquals(value,value.Copy())?a:b; }
    }
}
