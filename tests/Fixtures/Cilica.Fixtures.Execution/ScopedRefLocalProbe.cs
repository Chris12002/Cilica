using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class RefLocalCell
    {
        public int Number;
        public long Wide;
        public RefLocalCell Child;
        public RefLocalCell(int value) { Number=value; Wide=((long)value<<32)|(uint)value; }
    }
    public static class ScopedRefLocalProbe
    {
        private static int Pressure(int value) { var cell=new RefLocalCell(value); cell.Child=new RefLocalCell(~value); return cell.Number ^ cell.Child.Number; }
        private static void Change(ref int value,int other) { value=unchecked(value+other+Pressure(other)); }
        public static int Frame(int a,int b)
        {
            int value=a; ref int slot=ref value;
            if(b<0)slot=~slot;else slot=unchecked(slot+b);
            Change(ref slot,b);
            return unchecked(value * 3 + slot);
        }
        public static int Aliases(int a,int b)
        {
            int value=a; ref int first=ref value; ref int second=ref first;
            if(b<0)Change(ref first,b);else Change(ref second,~b);
            second=unchecked(first+b); return value+first+second;
        }
        public static int Heap(int a,int b)
        {
            var owner=new RefLocalCell(a); ref int slot=ref owner.Number; owner=null;
            if(b<0)slot=unchecked(slot+Pressure(b));else Change(ref slot,b);
            for(int i=0;i<4;++i)slot=unchecked(slot+i+Pressure(i));
            return slot;
        }
        public static int Wide(int a,int b)
        {
            var owner=new RefLocalCell(a); ref long slot=ref owner.Wide; owner=null;
            if(b<0)slot=~slot; else slot^=((long)b<<32)|(uint)b;
            Pressure(b); return (int)slot^(int)(slot>>32);
        }
        public static int Reference(int a,int b)
        {
            var owner=new RefLocalCell(a); owner.Child=new RefLocalCell(b);
            ref RefLocalCell slot=ref owner.Child; owner=null;
            if(a<0)slot=new RefLocalCell(a);else slot.Number=b;
            Pressure(b); var prior=slot; slot=new RefLocalCell(a^b);
            return prior.Number+slot.Number;
        }
        private static int Borrow(ref int input,int b)
        {
            ref int slot=ref input;
            if(b<0)slot=~slot;else slot=unchecked(slot+Pressure(b));
            Change(ref slot,b);return slot;
        }
        public static int Parameter(int a,int b) { int result=Borrow(ref a,b);return unchecked((a * 7 + result)^b); }
        private static int Throwing(int a,int b)
        {
            var owner=new RefLocalCell(a); ref int slot=ref owner.Number;
            if(b<0)slot=Pressure(a);else slot=b;
            if(slot!=0)throw new InvalidOperationException("borrow");
            return slot;
        }
        public static int FaultCleanup(int a,int b)
        {
            try { return Throwing(a,b); }
            catch(InvalidOperationException){return Heap(a,b);}
        }
        public static int UnsupportedReassignment(int a,int b)
        {
            int x=a,y=b; ref int slot=ref x;
            if(b<0)slot=ref y;
            slot=17;return x+y;
        }
        public static int UnsupportedRegion(int a,int b)
        {
            ref int slot=ref a;
            try { slot+=b; } finally { slot++; }
            return slot;
        }
        public static int UnsupportedValue(int a,int b)
        {
            ValueVectorCell item=default;ref ValueVectorCell slot=ref item;
            if(a<0)slot.Number=b;else slot.Number=a;
            return slot.Number;
        }
    }
}
