using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ListSortProbe
    {
        public sealed class Item { public int Key; public int Id; public Item(int key,int id){Key=key;Id=id;} }
        public struct Value { public int Key; public long Wide; public Item Reference; }
        public sealed class Observe
        {
            public int Trace, Calls, Fail, Mode;
            public Exception Fault;
            public List<int> List;
            public void Visit(int a,int b)
            {
                Calls++;Trace=unchecked((Trace*31+a)*31+b);
                var allocation=new Item(a,b); Trace=unchecked(Trace+allocation.Id);
                if(Calls==1 && List!=null)
                {
                    if(Mode==4) { for(int i=0;i<40;i++)List.Add(100+i); }
                    if(Mode==5) List.Clear();
                    if(Mode==6) List[0]=99;
                    if(Mode==7) List.RemoveAt(0);
                    if(Mode==8) List.RemoveAt(List.Count-1);
                    if(Mode==9) List.Capacity=List.Capacity+80;
                }
                if(Fail!=0 && Calls==Fail) throw Fault;
            }
            public int Compare(int a,int b){Visit(a,b);return Mode==1?-1:Mode==2?1:a.CompareTo(b);}
            public int References(Item a,Item b)
            {
                Visit(a==null?-999:a.Key,b==null?-999:b.Key);
                if(a==b)return 0;if(a==null)return -1;if(b==null)return 1;
                return a.Key.CompareTo(b.Key);
            }
            public int Values(Value a,Value b)
            {
                Visit(a.Key,b.Key);int result=a.Key.CompareTo(b.Key);a.Key=99;a.Reference=null;return result;
            }
        }
        private static List<int> Make(int length,int mode,int seed)
        {
            var list=new List<int>(length+3);
            for(int i=0;i<length;i++)
            { int value=mode==0?length-i:mode==1?i:mode==2?7:mode==3?i%5:mode==4?(i<length/2?i:length-i):unchecked((i*73+seed)%31);list.Add(value); }
            return list;
        }
        private static int Hash(List<int> list,int seed){for(int i=0;i<list.Count;i++)seed=unchecked(seed*31+list[i]);return seed;}
        private static int Text(string text,int seed){if(text==null)return seed;for(int i=0;i<text.Length;i++)seed=unchecked(seed*31+text[i]);return seed;}
        private static int Execute(List<int> list,Observe observe)
        {
            var enumerator=list.GetEnumerator();int seed=0;
            try{list.Sort(observe.Compare);seed=17;}
            catch(ArgumentException e){seed=Text(e.Message,23)+(e.InnerException==null?1:7);}
            catch(InvalidOperationException e){seed=Text(e.Message,31)+(e.InnerException==observe.Fault?11:19);}
            try {seed=unchecked(seed*31+(enumerator.MoveNext()?enumerator.Current:29));}
            catch(InvalidOperationException){seed=unchecked(seed*31+37);}
            return Hash(list,unchecked(seed+observe.Trace+observe.Calls*17));
        }
        public static int Integers(int a,int b){return Execute(Make((int)((uint)a%80),(int)((uint)b%6),b),new Observe());}
        public static int Adversarial(int a,int b){return Execute(Make(a<0?1024:512,4,b),new Observe());}
        public static int Faults(int a,int b)
        {
            var observe=new Observe();observe.Fail=1+(int)((uint)a%80);observe.Fault=b<0?(Exception)new IndexOutOfRangeException("index fault"):new InvalidOperationException("comparison fault");
            return Execute(Make(43,5,b),observe);
        }
        public static int Inconsistent(int a,int b){var observe=new Observe();observe.Mode=b<0?1:2;return Execute(Make(17+(a&31),5,b),observe);}
        public static int Reentrant(int a,int b){var list=Make(17+(a&7),0,b);var observe=new Observe();observe.List=list;observe.Mode=4+(int)((uint)b%6);return Execute(list,observe);}
        public static int Validation(int a,int b)
        {
            var list=Make(a&1,0,b);if(a<0)list=null;Comparison<int> compare=b<0?null:(Comparison<int>)(new Observe().Compare);
            try { list.Sort(compare);return 1; }
            catch(ArgumentNullException e){return Text(e.Message,Text(e.ParamName,2));}
            catch(NullReferenceException){return 3;}
        }
        public static int References(int a,int b)
        {
            var list=new List<Item>();var observe=new Observe();int length=3+(a&31);
            for(int i=0;i<length;i++)list.Add(i%7==0?null:new Item((i*73+b)%5,i));
            list.Sort(observe.References);int result=observe.Trace;
            for(int i=0;i<list.Count;i++)result=unchecked(result*31+(list[i]==null?-1:list[i].Id));return result;
        }
        public static int Values(int a,int b)
        {
            var list=new List<Value>();var observe=new Observe();int length=3+(a&31);
            for(int i=0;i<length;i++){Value v=default;v.Key=(i*73+b)%5;v.Wide=((long)b<<32)|(uint)i;v.Reference=new Item(i,i);list.Add(v);}
            list.Sort(observe.Values);int result=observe.Trace;
            for(int i=0;i<list.Count;i++){var v=list[i];result=unchecked(result*31+v.Reference.Id+(int)(v.Wide>>32)+(int)v.Wide+v.Key);}return result;
        }
        public static int ReferenceFault(int a,int b)
        {
            var list=new List<Item>();list.Add(new Item(a,b));list.Add(new Item(b,a));
            var observe=new Observe();observe.Fail=1;observe.Fault=new IndexOutOfRangeException();
            try{list.Sort(observe.References);return -1;}catch(ArgumentException e){return Text(e.Message,observe.Trace);}
        }
        public static int ValueFault(int a,int b)
        {
            var list=new List<Value>();Value v=default;v.Key=a;list.Add(v);v.Key=b;list.Add(v);
            var observe=new Observe();observe.Fail=1;observe.Fault=new IndexOutOfRangeException();
            try{list.Sort(observe.Values);return -1;}catch(ArgumentException e){return Text(e.Message,observe.Trace);}
        }
        public static int UnsupportedDefault(int a,int b){var list=Make(2,0,b);list.Sort();return list[0];}
    }
}
