using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ListFindProbe
    {
        private static int Hash(string s) { int h=17; for(int i=0;i<s.Length;i++) h=unchecked(h*31+s[i]);return h; }
        private static FindIndexState State(int mode,int target)
        {
            var s=new FindIndexState();s.Mode=mode;s.Target=target;s.Source=new List<int>();for(int i=0;i<4;i++)s.Source.Add(i*2+1);return s;
        }
        public static int NullEmpty(int mode,int target)
        {
            var s=State(0,target);
            if(mode==0||mode==3)s.Source.Clear();
            if(mode==2||mode==4)s.Source=null;
            try{return s.Source.Find(mode==1||mode==3||mode==4?null:s.Match)+s.Calls*13+s.Trace;}
            catch(ArgumentNullException e){return Hash(e.Message)+Hash(e.ParamName);}
            catch(NullReferenceException e){return Hash(e.Message);}
        }
        public static int Integers(int mode,int target)
        {
            var s=State(mode,target);int result;
            try{result=s.Source.Find(s.Match);}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
        public static int References(int mode,int target)
        {
            var s=new FindIndexReferenceState();s.Mode=mode;s.Target=target;s.Source=new List<ValueVectorBox>();for(int i=0;i<4;i++)s.Source.Add(new ValueVectorBox(i*2+1));int result;
            try{var found=s.Source.Find(s.Match);result=found==null?-991:found.Number;}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
        public static int Values(int mode,int target)
        {
            var s=new FindIndexValueState();s.Mode=mode;s.Target=target;s.Source=new List<ValueVectorCell>();
            for(int i=0;i<4;i++){ValueVectorCell v=default;v.Number=i*2+1;v.Wide=(long)i<<32;v.Reference=new ValueVectorBox(i+11);s.Source.Add(v);}int result;
            try{var found=s.Source.Find(s.Match);result=unchecked(found.Number+(int)(found.Wide>>32)+(found.Reference==null?-991:found.Reference.Number));}
            catch(InvalidOperationException e){result=Hash(e.Message);}
            catch(IndexOutOfRangeException e){result=Hash(e.Message);}
            return unchecked(result+s.Trace+s.Calls*13+s.Source.Count);
        }
    }
}
