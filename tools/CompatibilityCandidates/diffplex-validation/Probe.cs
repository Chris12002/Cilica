// Authored validation adapter; compile/run only with approved external isolation.
using System;
using System.Collections.Generic;
using DiffPlex;
using DiffPlex.Model;
namespace Cilica.CandidateValidation
{
    public static class Probe
    {
        private static string Input(int index,bool second)
        {
            switch(index)
            {
                case 0:return "";
                case 1:return second?"ac":"ab";
                case 2:return second?"a\nB\r":"a\r\nb\n";
                case 3:return second?"\u03c3\n\u039c":" \u03a3\r\n\u00b5 ";
                case 4:return second?"A\ud801\udc00\ud800Z":"a\ud801\udc28\ud800z";
                case 5:return second?"I\nS\nss":"\u0131\n\u017f\n\u00df";
                case 6:return second?"x":null;
                default:return second?null:"x";
            }
        }
        private static void Text(List<int> output,string text)
        {output.Add(text.Length);for(int i=0;i<text.Length;i++)output.Add(text[i]);}
        private static void Pieces(List<int> output,IReadOnlyList<string> pieces)
        {output.Add(pieces.Count);for(int i=0;i<pieces.Count;i++)Text(output,pieces[i]);}
        public static int Run(int input,int observation)
        {
            var output=new List<int>();
            try
            {
                var differ=new Differ();string first=Input(input>>3,false),second=Input(input>>3,true);
                DiffResult result;
                if((input&1)==0)
                    result=(input&4)==0?differ.CreateCharacterDiffs(first,second,(input&2)!=0)
                        :differ.CreateCharacterDiffs(first,second,(input&2)!=0,true);
                else
                    result=(input&4)==0?differ.CreateLineDiffs(first,second,(input&2)!=0)
                        :differ.CreateLineDiffs(first,second,(input&2)!=0,true);
                output.Add(0);Pieces(output,result.PiecesOld);Pieces(output,result.PiecesNew);output.Add(result.DiffBlocks.Count);
                for(int i=0;i<result.DiffBlocks.Count;i++)
                {var block=result.DiffBlocks[i];output.Add(block.DeleteStartA);output.Add(block.DeleteCountA);output.Add(block.InsertStartB);output.Add(block.InsertCountB);}
            }
            catch(ArgumentNullException error){output.Add(1);Text(output,error.ParamName);Text(output,error.Message);}
            return observation<0?output.Count:output[observation];
        }
    }
}
