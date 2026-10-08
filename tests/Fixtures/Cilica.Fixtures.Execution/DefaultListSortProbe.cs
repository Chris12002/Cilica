using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class DefaultListSortProbe
    {
        private static int Bits(int a,int b,int i,int n)
        {
            int mode=b&7;
            if(mode==0)return i;if(mode==1)return n-i;if(mode==2)return a;
            if(mode==3)return (i&3)-2;if(mode==4)return (i&1)==0?int.MinValue:int.MaxValue;
            if(mode==5)return i<n/2?i:n-i;return unchecked((a+i)*1103515245+b);
        }
        public static int Signed32(int a,int b)
        {
            int n=(a&63)+2;var list=new List<int>(n);for(int i=0;i<n;i++)list.Add(Bits(a,b,i,n));list.Sort();
            int h=17;foreach(var x in list)h=unchecked(h*31+x);return h;
        }
        public static int Unsigned32(int a,int b)
        {
            int n=(a&63)+2;var list=new List<uint>(n);for(int i=0;i<n;i++)list.Add(unchecked((uint)Bits(a,b,i,n)));list.Sort();
            int h=17;foreach(var x in list)h=unchecked(h*31+(int)x);return h;
        }
        public static int Signed64(int a,int b)
        {
            int n=(a&63)+2;var list=new List<long>(n);for(int i=0;i<n;i++)list.Add(unchecked(((long)Bits(a,b,i,n)<<32)|(uint)Bits(b,a,n-i,n)));list.Sort();
            int h=17;foreach(var x in list)h=unchecked(h*31+(int)(x^(x>>32)));return h;
        }
        public static int Unsigned64(int a,int b)
        {
            int n=(a&63)+2;var list=new List<ulong>(n);for(int i=0;i<n;i++)list.Add(unchecked(((ulong)(uint)Bits(a,b,i,n)<<32)|(uint)Bits(b,a,n-i,n)));list.Sort();
            int h=17;foreach(var x in list)h=unchecked(h*31+(int)(x^(x>>32)));return h;
        }
        public static int ShortLists(int a,int b)
        {
            int h=17;
            for(int count=0;count<2;count++)
            {
                var list=new List<long>();if(count==1)list.Add(unchecked(((long)a<<32)|(uint)b));var cursor=list.GetEnumerator();cursor.MoveNext();list.Sort();
                try{cursor.MoveNext();h=-100;}catch(InvalidOperationException e){h=unchecked(h*31+e.Message.Length);}
                h=unchecked(h*31+list.Count);if(count==1)h=unchecked(h*31+(int)list[0]);cursor.Dispose();
            }
            return h;
        }
        public static int Null(int a,int b) { List<long> list=null;try{list.Sort();return -100;}catch(NullReferenceException e){return e.Message.Length;} }
        public static int Repeat(int a,int b)
        {
            var list=new List<long>{a,b,long.MinValue,long.MaxValue,0,a};list.Sort();list.Reverse();list.Sort();
            int h=17;foreach(var x in list)h=unchecked(h*31+(int)(x^(x>>32)));list.Clear();list.Sort();list.Add(a);list.Sort();return unchecked(h+(int)list[0]);
        }
        public static int UnsupportedText(int a,int b) { var list=new List<string>{"z","a"};list.Sort();return list.Count; }
        public static int UnsupportedEnum(int a,int b) { var list=new List<ArrayEnum>{ArrayEnum.One,ArrayEnum.Zero};list.Sort();return list.Count; }
        public static int UnsupportedNullable(int a,int b) { var list=new List<int?>{a,null};list.Sort();return list.Count; }
        public static int UnsupportedSection(int a,int b) { var list=new List<int>{a,b};list.Sort(0,2,null);return list[0]; }
    }
}
