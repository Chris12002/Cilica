using System;
using System.Collections.Generic;
using Force.Crc32;
namespace Cilica.CandidateValidation
{
    // Authored adapter only. The two pinned upstream files remain unchanged.
    public static class Crc32Probe
    {
        private static int Length(int n)
        {
            switch(n) { case 0:return 0;case 1:return 1;case 2:return 15;case 3:return 16;
                case 4:return 17;case 5:return 31;case 6:return 32;default:return 257; }
        }
        private static byte Pattern(int index,int scenario)
            => scenario==40?(byte)(49+index):unchecked((byte)(index*37+scenario*19));
        private static void Text(List<int> values,string text)
        {
            if(text==null){values.Add(-1);return;}
            values.Add(text.Length);for(int i=0;i<text.Length;i++)values.Add(text[i]);
        }
        public static int Run(int scenario,int observation)
        {
            int length=scenario<16?Length(scenario&7)+(scenario<8?0:6):scenario<24?8:scenario<32?4:scenario<40?33:9;
            byte[] input=scenario==24||scenario==25?null:new byte[length];
            if(input!=null)for(int i=0;i<input.Length;i++)input[i]=Pattern(i,scenario);
            uint seed=unchecked(0x80000000u+(uint)scenario*0x01010101u),crc=0;
            int fault=0;string message=null,parameter=null;
            try
            {
                if(scenario<8||scenario==24||scenario==40)crc=Crc32Algorithm.Compute(input);
                else if(scenario<16)crc=Crc32Algorithm.Append(seed,input,3,length-6);
                else if(scenario<24)crc=Crc32Algorithm.Append(seed,input,scenario==23?8:scenario-16,0);
                else if(scenario==25)crc=Crc32Algorithm.Append(seed,input,-1,-1);
                else if(scenario==26)crc=Crc32Algorithm.Append(seed,input,-1,1);
                else if(scenario==27)crc=Crc32Algorithm.Append(seed,input,0,-1);
                else if(scenario==28)crc=Crc32Algorithm.Append(seed,input,4,1);
                else if(scenario==29)crc=Crc32Algorithm.Append(seed,input,int.MaxValue,1);
                else if(scenario==30)crc=Crc32Algorithm.Append(seed,input,int.MaxValue,int.MaxValue);
                else if(scenario==31)crc=Crc32Algorithm.Append(seed,input,int.MaxValue,0);
                else
                {
                    int cut=scenario==39?33:Length(scenario-32);
                    uint prefix=Crc32Algorithm.Append(seed,input,0,cut);
                    crc=Crc32Algorithm.Append(prefix,input,cut,length-cut);
                    if(crc!=Crc32Algorithm.Append(seed,input,0,length))fault=4;
                }
            }
            catch(ArgumentNullException e){fault=1;message=e.Message;parameter=e.ParamName;}
            catch(ArgumentOutOfRangeException e){fault=2;message=e.Message;parameter=e.ParamName;}
            catch(IndexOutOfRangeException e){fault=3;message=e.Message;}
            int unchanged=1;
            if(input!=null)for(int i=0;i<input.Length;i++)if(input[i]!=Pattern(i,scenario))unchanged=0;
            var result=new List<int>();result.Add(unchecked((int)crc));result.Add(unchanged);result.Add(fault);
            Text(result,message);Text(result,parameter);
            return observation<0?result.Count:result[observation];
        }
    }
}
