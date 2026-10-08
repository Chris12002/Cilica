using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class ListReverseProbe
    {
        private static List<int> Make(int a,int b)
        { var list=new List<int>(11);list.Add(a);list.Add(b);list.Add(3);list.Add(4);list.Add(5);return list; }
        private static int Read(List<int> list)
        { int result=list.Count*17+list.Capacity;for(int i=0;i<list.Count;i++)result=unchecked(result*31+list[i]);return result; }
        private static int Text(string text)
        { if(text==null)return -1;int result=0;for(int i=0;i<text.Length;i++)result=unchecked(result*31+text[i]);return result; }
        public static int Whole(int a,int b)
        { var list=Make(a,b);list.Reverse();int result=Read(list);list.Reverse();return unchecked(result+Read(list)*31); }
        public static int Range(int a,int b)
        { var list=Make(a,b);int start=(a&7)%6;int count=(b&7)%(6-start);list.Reverse(start,count);return Read(list); }
        public static int Faults(int a,int b)
        {
            var list=Make(a,b);var cursor=list.GetEnumerator();cursor.MoveNext();int result=0;
            try {list.Reverse(a,b);result=7;}
            catch(ArgumentOutOfRangeException error){result=unchecked(Text(error.Message)*31+Text(error.ParamName)*17+3);}
            catch(ArgumentException error){result=unchecked(Text(error.Message)*31+Text(error.ParamName)*17+13);}
            try {result=unchecked(result*7+(cursor.MoveNext()?cursor.Current:11));}
            catch(InvalidOperationException error){result=unchecked(result*7+Text(error.Message));}
            cursor.Dispose();return unchecked(result+Read(list)*17);
        }
        public static int Version(int a,int b)
        {
            var list=new List<int>();for(int i=0;i<(a&3);i++)list.Add(b+i);
            var cursor=list.GetEnumerator();cursor.MoveNext();int result=cursor.Current;
            if(b<0)list.Reverse();else list.Reverse(list.Count,0);
            result=unchecked(result*31+cursor.Current);
            try {cursor.MoveNext();result+=3;}catch(InvalidOperationException){result+=5;}
            try {((IEnumerator)cursor).Reset();result+=7;}catch(InvalidOperationException){result+=11;}
            cursor.Dispose();return unchecked(result+Read(list));
        }
        public static int References(int a,int b)
        {
            var first=new ValueVectorBox(a);var last=new ValueVectorBox(b);
            var list=new List<ValueVectorBox>();list.Add(first);list.Add(null);list.Add(last);
            var view=list.AsReadOnly();list.Reverse();var allocation=new ValueVectorBox(7);
            return unchecked((ReferenceEquals(view[0],last)?3:5)+(ReferenceEquals(view[2],first)?7:11)+(view[1]==null?13:17)+view[0].Number*31+view[2].Number+allocation.Number);
        }
        public static int Values(int a,int b)
        {
            var list=new List<ValueVectorCell>();
            list.Add(new ValueVectorCell {Number=a,Reference=new ValueVectorBox(b)});
            list.Add(new ValueVectorCell {Number=b,Reference=new ValueVectorBox(a)});
            list.Add(new ValueVectorCell {Number=7,Reference=null});
            list.Reverse(0,2);var allocation=new ValueVectorBox(13);var first=list[0];first.Number=17;
            return unchecked(list[0].Number*31+list[0].Reference.Number+list[1].Number*17+list[1].Reference.Number+(list[2].Reference==null?3:5)+allocation.Number);
        }
        public static int WideNullable(int a,int b)
        {
            var wide=new List<long>();wide.Add((long)a*4294967296L+b);wide.Add((long)b*4294967296L+a);wide.Reverse();
            var nullable=new List<int?>();nullable.Add(a);nullable.Add(null);nullable.Add(b);nullable.Reverse();
            return unchecked((int)(wide[0]>>32)+(int)wide[1]+nullable[0].Value*31+nullable[2].Value+(nullable[1].HasValue?3:5));
        }
        public static int Query(int a,int b)
        {
            var list=Make(a,b);var query=list.Select(x=>unchecked(x*17));var cursor=query.GetEnumerator();cursor.MoveNext();
            int result=cursor.Current;list.Reverse();
            try {cursor.MoveNext();result+=3;}catch(InvalidOperationException){result+=5;}
            cursor.Dispose();var values=query.ToArray();for(int i=0;i<values.Length;i++)result=unchecked(result*31+values[i]);return result;
        }
        public static int NullReceiver(int a,int b)
        { List<int> list=null;try{if(a<0)list.Reverse(a,b);else list.Reverse();return 3;}catch(NullReferenceException){return 7;} }
    }
}
