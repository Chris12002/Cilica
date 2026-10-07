using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class FindIndexState
    {
        public List<int> Source; public int Mode, Target, Calls, Trace;
        public bool Match(int value)
        {
            Calls++; Trace=unchecked(Trace*31+value);
            if(Calls==1)
            {
                if(Mode==1) Source.Add(73);
                if(Mode==2) Source.Clear();
                if(Mode==3) Source.RemoveAt(0);
                if(Mode==4) Source.Insert(0,79);
                if(Mode==5) { Source.Clear(); Source.Capacity=0; }
                if(Mode==6) Source[1]=83;
                if(Mode==7) throw new InvalidOperationException("match");
                if(Mode==8) { Source.Capacity=32; Source[1]=89; }
            }
            var pressure=new ValueVectorBox(value); Trace=unchecked(Trace+pressure.Number-value);
            return value==Target;
        }
    }
    public sealed class FindIndexReferenceState
    {
        public List<ValueVectorBox> Source; public int Mode, Target, Calls, Trace;
        public bool Match(ValueVectorBox item)
        {
            int value=item==null?-1:item.Number; Calls++; Trace=unchecked(Trace*31+value);
            if(Calls==1)
            {
                if(Mode==1) Source.Add(new ValueVectorBox(73));
                if(Mode==2) Source.Clear();
                if(Mode==3) Source.RemoveAt(0);
                if(Mode==4) Source[1]=new ValueVectorBox(79);
                if(Mode==5) { Source.Clear(); Source.Capacity=0; }
                if(Mode==6) throw new InvalidOperationException("match");
            }
            var pressure=new ValueVectorBox(97); Trace=unchecked(Trace+pressure.Number);
            return value==Target;
        }
    }
    public sealed class FindIndexValueState
    {
        public List<ValueVectorCell> Source; public int Mode, Target, Calls, Trace;
        public bool Match(ValueVectorCell item)
        {
            int value=item.Number; Calls++; Trace=unchecked(Trace*31+value+(int)(item.Wide>>32)+(item.Reference==null?-1:item.Reference.Number));
            if(Calls==1)
            {
                if(Mode==1) Source.Add(default);
                if(Mode==2) Source.Clear();
                if(Mode==3) Source.RemoveAt(0);
                if(Mode==4) Source[1]=default;
                if(Mode==5) { Source.Clear(); Source.Capacity=0; }
                if(Mode==6) throw new InvalidOperationException("match");
            }
            item.Number=997;var pressure=new ValueVectorBox(101);Trace=unchecked(Trace+pressure.Number);
            return value==Target;
        }
    }
    public static class ListFindIndexProbe
    {
        private static int Hash(string s) { int h=17; for(int i=0;i<s.Length;i++) h=unchecked(h*31+s[i]);return h; }
        private static FindIndexState State(int mode,int target)
        {
            var s=new FindIndexState();s.Mode=mode;s.Target=target;s.Source=new List<int>();for(int i=0;i<4;i++)s.Source.Add(i*2+1);return s;
        }
        private static int BoundsCall(FindIndexState s,int start,int count,int kind)
        {
            try
            {
                if(kind==0)return s.Source.FindIndex(start,count,s.Match);
                if(kind==1)return s.Source.FindIndex(start,count,null);
                if(kind==2)return s.Source.FindIndex(start,s.Match);
                if(kind==3)return s.Source.FindIndex(start,null);
                return s.Source.FindIndex((Predicate<int>)null);
            }
            catch(ArgumentException e){return unchecked(Hash(e.Message)+Hash(e.ParamName));}
        }
        public static int Bounds(int start,int count)
        {
            var s=State(0,5);int result=0;for(int kind=0;kind<5;kind++)result=unchecked(result*7+BoundsCall(s,start,count,kind));return unchecked(result+s.Trace+s.Calls);
        }
        public static int Empty(int start,int count)
        {
            var s=State(0,5);s.Source.Clear();int result=s.Source.FindIndex(s.Match);
            for(int kind=0;kind<5;kind++)result=unchecked(result*7+BoundsCall(s,start,count,kind));return unchecked(result+s.Trace+s.Calls);
        }
        public static int Integers(int mode,int target)
        {
            var s=State(mode,target);int result;
            try{result=s.Source.FindIndex(s.Match);}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
        public static int References(int mode,int target)
        {
            var s=new FindIndexReferenceState();s.Mode=mode;s.Target=target;s.Source=new List<ValueVectorBox>();for(int i=0;i<4;i++)s.Source.Add(new ValueVectorBox(i*2+1));int result;
            try{result=s.Source.FindIndex(s.Match);}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
        public static int Values(int mode,int target)
        {
            var s=new FindIndexValueState();s.Mode=mode;s.Target=target;s.Source=new List<ValueVectorCell>();
            for(int i=0;i<4;i++){ValueVectorCell v=default;v.Number=i*2+1;v.Wide=(long)i<<32;v.Reference=new ValueVectorBox(i+11);s.Source.Add(v);}int result;
            try{result=s.Source.FindIndex(s.Match);}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
    }
}
