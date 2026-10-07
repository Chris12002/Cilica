using System;
namespace Cilica.Fixtures.Execution
{
    public static class ReferenceArrayBorrowProbe
    {
        private static int Pressure(int a) { var c=new RefLocalCell(a);c.Child=new RefLocalCell(~a);return c.Number+c.Child.Number; }
        public static int Cache(int a,int b)
        {
            var values=new RefLocalCell[4];int index=a&3;
            ref RefLocalCell slot=ref values[index];
            slot??=new RefLocalCell(b);var first=slot;
            Pressure(a);slot??=new RefLocalCell(a);
            return slot.Number+(first==values[index]?17:-17);
        }
        public static int ReplaceArray(int a,int b)
        {
            var values=new RefLocalCell[4];ref RefLocalCell slot=ref values[a&3];
            values=new RefLocalCell[4];Pressure(a);
            if(b<0)slot=new RefLocalCell(a);else slot=new RefLocalCell(b);
            return slot.Number+(values[a&3]==null?31:-31);
        }
        private static void Replace(ref RefLocalCell slot,int value)
        {
            Pressure(value);slot=new RefLocalCell(value);Pressure(~value);slot.Number=unchecked(slot.Number+7);
        }
        public static int Reborrow(int a,int b)
        {
            var values=new RefLocalCell[4];ref RefLocalCell slot=ref values[a&3];
            if(b<0)Replace(ref slot,a);else Replace(ref slot,b);
            return slot.Number+values[a&3].Number;
        }
        public static int Strings(int a,int b)
        {
            var values=new string[2];ref string slot=ref values[a&1];
            if(b<0)slot="A\0\ud800";else slot="\uffffZ";
            Pressure(a);return slot.Length*31+values[a&1][0];
        }
        private static T Set<T>(T[] values,int index,T replacement) where T:class
        {
            ref T slot=ref values[index];
            if(slot==null)slot=replacement;
            Pressure(index);return slot;
        }
        public static int Generic(int a,int b)
        {
            var values=new RefLocalCell[2];var expected=new RefLocalCell(b);
            var actual=Set(values,a&1,expected);var other=Set(values,a&1,new RefLocalCell(a));
            return actual.Number+(other==expected?41:-41);
        }
        private static int Access(object[] values,int index)
        {
            ref object slot=ref values[index];
            Pressure(index);slot=new RefLocalCell(index);return ((RefLocalCell)slot).Number;
        }
        public static int Faults(int a,int b)
        {
            object[] values=b==0?new object[1]:b==3?new string[0]:new string[1];
            if(b==2)values=null;
            try{return Access(values,a)+101;}
            catch(NullReferenceException){return 103;}
            catch(IndexOutOfRangeException){return 107;}
            catch(ArrayTypeMismatchException){return 109;}
        }
    }
}
