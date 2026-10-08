using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class RemoveAllBox { public int Value; public RemoveAllBox(int value){Value=value;} }
    public struct RemoveAllValue {public int Number;public RemoveAllBox Box;}
    public sealed class RemoveAllPredicate
    {
        public List<int> List; public int Mode,Select,Calls,Trace;
        public bool Match(int value)
        {
            Calls++;Trace=unchecked(Trace*31+new RemoveAllBox(value).Value);
            if(Calls==3)
            {
                if(Mode==1)List.Add(9);
                if(Mode==2)List.Clear();
                if(Mode==3)List.RemoveAt(List.Count-1);
                if(Mode==4)List[0]=17;
                if(Mode==5){for(int i=0;i<9;i++)List.Add(i);}
                if(Mode==6)List.RemoveAll(Negative);
                if(Mode==7)throw new InvalidOperationException("predicate");
            }
            return (value&1)==(Select&1);
        }
        public static bool Negative(int value)=>value<0;
        public bool Ref(RemoveAllBox value)=>value==null||(value.Value&1)==(Select&1);
        public bool Value(RemoveAllValue value){int n=value.Number;value.Number=99;return (n&1)==(Select&1);}
        public bool Text(string value)=>value==null||value.Length==(Select&3);
    }
    public sealed class RemoveAllReferencePredicate
    {
        public List<RemoveAllBox> List; public int Calls,Mode;
        public bool Match(RemoveAllBox value)
        {
            Calls++;
            if(Calls==3)
            {
                if(Mode==1)List.Clear();
                if(Mode==2)List.RemoveAt(List.Count-1);
                if(Mode==3){for(int i=0;i<9;i++)List.Add(new RemoveAllBox(i));}
                if(Mode==4)throw new InvalidOperationException("predicate");
            }
            return value!=null&&(value.Value&1)==0;
        }
    }
    public static class ListRemoveAllProbe
    {
        private static int Hash(string text){int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h;}
        private static int Fold(List<int> list){int h=list.Count+list.Capacity;for(int i=0;i<list.Count;i++)h=unchecked(h*31+list[i]);return h;}
        public static int Integers(int a,int b)
        {
            var list=new List<int>(8);for(int i=0;i<(a&7);i++)list.Add(i-2);
            var cursor=list.GetEnumerator();var predicate=new RemoveAllPredicate{List=list,Select=b};
            int n=list.RemoveAll(predicate.Match),state=0;
            try{if(cursor.MoveNext())state=cursor.Current+1;}catch(InvalidOperationException){state=91;}
            return unchecked(Fold(list)*31+n+state+predicate.Trace);
        }
        public static int Mutation(int a,int b)
        {
            var list=new List<int>(6){1,2,3,4,5,6};var predicate=new RemoveAllPredicate{List=list,Mode=a&7,Select=b};var cursor=list.GetEnumerator();int n;
            try{n=list.RemoveAll(predicate.Match);}catch(InvalidOperationException e){n=Hash(e.Message);}
            int state=0;try{if(cursor.MoveNext())state=cursor.Current+1;}catch(InvalidOperationException){state=91;}
            return unchecked(Fold(list)*31+n+predicate.Trace+predicate.Calls+state);
        }
        public static int References(int a,int b)
        {
            var list=new List<RemoveAllBox>(6){new RemoveAllBox(1),new RemoveAllBox(2),new RemoveAllBox(3),new RemoveAllBox(4),null,new RemoveAllBox(6)};
            var predicate=new RemoveAllReferencePredicate{List=list,Mode=a%5};var cursor=list.GetEnumerator();int n;
            try{n=list.RemoveAll(predicate.Match);}catch(InvalidOperationException e){n=Hash(e.Message);}catch(IndexOutOfRangeException e){n=Hash(e.Message);}
            int state=0;try{if(cursor.MoveNext())state=cursor.Current==null?5:cursor.Current.Value;}catch(InvalidOperationException){state=91;}
            int h=unchecked(n+list.Count*31+predicate.Calls+state);for(int i=0;i<list.Count;i++)h=unchecked(h*31+(list[i]==null?-1:list[i].Value));return h;
        }
        public static int Values(int a,int b)
        {
            var list=new List<RemoveAllValue>(8);for(int i=0;i<(a&7);i++)list.Add(new RemoveAllValue{Number=i,Box=new RemoveAllBox(b+i)});
            var predicate=new RemoveAllPredicate{Select=b};int h=list.RemoveAll(predicate.Value);
            for(int i=0;i<list.Count;i++)h=unchecked(h*31+list[i].Number+list[i].Box.Value);return h;
        }
        public static int Strings(int a,int b)
        {
            var list=new List<string>{null,"","x","ab","cde"};var p=new RemoveAllPredicate{Select=b};int h=list.RemoveAll(p.Text);
            for(int i=0;i<list.Count;i++)h=unchecked(h*31+Hash(list[i]));return h;
        }
        public static int Null(int a,int b)
        {
            List<int> list=a==0?null:new List<int>();
            try{return list.RemoveAll(null);}catch(ArgumentNullException e){return unchecked(Hash(e.Message)*31+Hash(e.ParamName));}catch(NullReferenceException){return 29;}
        }
        public static List<RemoveAllBox> CreateTracked()=>new List<RemoveAllBox>(3){new RemoveAllBox(1),new RemoveAllBox(2)};
        private static bool RemoveTwo(RemoveAllBox box)=>box.Value==2;
        private static bool RemoveEvery(RemoveAllBox box)=>true;
        public static void RemoveTracked(List<RemoveAllBox> list)=>list.RemoveAll(RemoveTwo);
        public static void ClearTracked(List<RemoveAllBox> list)=>list.RemoveAll(RemoveEvery);
        public static List<RemoveAllValue> CreateTrackedValues()=>new List<RemoveAllValue>(3){new RemoveAllValue{Number=1,Box=new RemoveAllBox(1)},new RemoveAllValue{Number=2,Box=new RemoveAllBox(2)}};
        private static bool RemoveTwoValues(RemoveAllValue value)=>value.Number==2;
        private static bool RemoveEveryValues(RemoveAllValue value)=>true;
        public static void RemoveTrackedValues(List<RemoveAllValue> list)=>list.RemoveAll(RemoveTwoValues);
        public static void ClearTrackedValues(List<RemoveAllValue> list)=>list.RemoveAll(RemoveEveryValues);
        public static int TrackedValues(int a,int b){var list=CreateTrackedValues();RemoveTrackedValues(list);ClearTrackedValues(list);return list.Count;}
        public static int Tracked(int a,int b){var list=CreateTracked();RemoveTracked(list);ClearTracked(list);return list.Count;}
    }
}
