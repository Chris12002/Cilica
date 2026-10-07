using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ListExistsProbe
    {
        private static int Hash(string s) { int h=17; for(int i=0;i<s.Length;i++) h=unchecked(h*31+s[i]);return h; }
        private static FindIndexState State(int mode,int target)
        {
            var s=new FindIndexState();s.Mode=mode;s.Target=target;s.Source=new List<int>();for(int i=0;i<4;i++)s.Source.Add(i*2+1);return s;
        }
        public static int NullEmpty(int mode,int target)
        {
            var s=State(0,target);
            if(mode==0)s.Source.Clear();
            if(mode==2)s.Source=null;
            try{return (s.Source.Exists(mode==1?null:s.Match)?1:0)+s.Calls*13+s.Trace;}
            catch(ArgumentNullException e){return Hash(e.Message)+Hash(e.ParamName);}
            catch(NullReferenceException e){return Hash(e.Message);}
        }
        public static int Integers(int mode,int target)
        {
            var s=State(mode,target);int result;
            try{result=s.Source.Exists(s.Match)?1:0;}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
        public static int References(int mode,int target)
        {
            var s=new FindIndexReferenceState();s.Mode=mode;s.Target=target;s.Source=new List<ValueVectorBox>();for(int i=0;i<4;i++)s.Source.Add(new ValueVectorBox(i*2+1));int result;
            try{result=s.Source.Exists(s.Match)?1:0;}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
        public static int Values(int mode,int target)
        {
            var s=new FindIndexValueState();s.Mode=mode;s.Target=target;s.Source=new List<ValueVectorCell>();
            for(int i=0;i<4;i++){ValueVectorCell v=default;v.Number=i*2+1;v.Wide=(long)i<<32;v.Reference=new ValueVectorBox(i+11);s.Source.Add(v);}int result;
            try{result=s.Source.Exists(s.Match)?1:0;}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
    }
}
