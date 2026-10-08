using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class CandidateOrdinalIgnoreCaseProbe
    {
        public static string ScalarText(int scalar)
        {
            if(scalar<=0xffff)return new string(new char[]{(char)scalar});
            scalar-=0x10000;return new string(new char[]{(char)(0xd800+(scalar>>10)),(char)(0xdc00+(scalar&1023))});
        }
        public static string Text(int index)
        {
            switch(index)
            {
                case 0:return null;case 1:return "";case 2:return "a";case 3:return "A";
                case 4:return "i";case 5:return "I";case 6:return "\u0130";case 7:return "\u0131";
                case 8:return "s";case 9:return "S";case 10:return "\u017f";
                case 11:return "\u00df";case 12:return "\u1e9e";case 13:return "ss";
                case 14:return "\u03a3";case 15:return "\u03c3";case 16:return "\u03c2";
                case 17:return "\u00b5";case 18:return "\u039c";case 19:return "\u03bc";
                case 20:return "\u212a";case 21:return "k";case 22:return "K";
                case 23:return "\u00e9";case 24:return "E\u0301";case 25:return "\u00c9";
                case 26:return "a\0z";case 27:return "A\0Z";
                case 28:return "\ud801\udc28";case 29:return "\ud801\udc00";
                case 30:return "\ud803\udd70";case 31:return "\ud803\udd50";
                case 32:return "\ud800";case 33:return "\ud800A";case 34:return "\ud800a";
                case 35:return "\ud800\udc00";case 36:return "\ud800\ud800";
                case 37:return "\udc00";case 38:return "\udfff\ud800";
                case 39:return "\uffff";case 40:return "\udbff\udfff";
                case 41:return "x\ud801\udc28Y";case 42:return "X\ud801\udc00y";
                case 43:return "\ud801\udc28\ud800a\udfff";case 44:return "\ud801\udc00\ud800A\udfff";
                case 45:return "AAAAAAAAAAAAAAAAa\ud800z";case 46:return "aaaaaaaaaaaaaaaaA\ud800Z";
                default:return "\ud800\udc00\udfff";
            }
        }
        public static int Compare(int a,int b)=>StringComparer.OrdinalIgnoreCase.Compare(Text(a),Text(b));
        public static int EqualityHash(int a,int b)
        {
            var comparer=StringComparer.OrdinalIgnoreCase;string x=Text(a),y=Text(b);
            bool same=comparer.Equals(x,y);int result=same?1:0;
            if(x!=null&&y!=null)
            {
                int first=comparer.GetHashCode(x),second=comparer.GetHashCode(y);
                if(same&&first!=second)return -999;
                if(first!=comparer.GetHashCode(x))return -997;
            }
            return result;
        }
        public static int ScalarMapping(int a,int b)
        {return StringComparer.OrdinalIgnoreCase.Compare(ScalarText(a),a<=0xffff?"\0":"\ud800\udc00");}
        public static int ScalarPair(int a,int b)
        {
            string x=ScalarText(a),y=ScalarText(b);var comparer=StringComparer.OrdinalIgnoreCase;
            bool same=comparer.Equals(x,y);return (same?1:0)+(same&&comparer.GetHashCode(x)!=comparer.GetHashCode(y)?-1000:0);
        }
        public static int Dictionary(int a,int b)
        {
            var ordinal=StringComparer.Ordinal;var ignore=StringComparer.OrdinalIgnoreCase;
            IEqualityComparer<string> selected=a<0?ordinal:ignore;
            var map=new Dictionary<string,int>(selected);
            for(int i=1;i<48;i++)map[Text(i)]=i+b;
            int result=map.Count;
            for(int i=1;i<48;i++) {map.TryGetValue(Text(i),out var value);result=unchecked(result*31+value);}
            for(int i=1;i<48;i+=3)map.Remove(Text(i));
            foreach(var item in map)result=unchecked(result*31+item.Value);
            return result;
        }
        public static int IdentityFaults(int a,int b)
        {
            var first=StringComparer.OrdinalIgnoreCase;var second=StringComparer.OrdinalIgnoreCase;
            int result=ReferenceEquals(first,second)?1:0;result+=ReferenceEquals(first,StringComparer.Ordinal)?-100:3;
            try {first.GetHashCode((string)null);}catch(ArgumentNullException e){result+=e.ParamName=="obj"?5:-200;}
            IComparer<string> compare=first;IEqualityComparer<string> equal=second;
            return result+compare.Compare("a","A")+(equal.Equals("\u0131","I")?17:19);
        }
    }
}
