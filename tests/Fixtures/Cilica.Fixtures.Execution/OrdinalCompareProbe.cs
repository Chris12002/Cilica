using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class OrdinalCompareState
    {
        public int Order;
        public string Text;
        public StringComparison Mode;
        public string Read(int digit) { Order=unchecked(Order*10+digit); return Text; }
    }
    public static class OrdinalCompareProbe
    {
        public static int Values(int a,int b) => String.Compare(OrdinalTextProbe.Pick(a),OrdinalTextProbe.Pick(b),StringComparison.Ordinal);
        public static int Units(int a,int b)
        {
            var first=new string(new char[]{'a',(char)a,'x'}); var second=new string(new char[]{'a',(char)b,'x'});
            return String.Compare(first,second,StringComparison.Ordinal);
        }
        public static int Evaluation(int a,int b)
        {
            var state=new OrdinalCompareState { Text=OrdinalTextProbe.Pick(a&15) };
            int compared=String.Compare(state.Read(1),state.Read(2),StringComparison.Ordinal);
            return unchecked(compared*31+state.Order+b);
        }
        public static int Identity(int a,int b)
        {
            var first=new string(new char[]{(char)a,(char)b}); var second=new string(new char[]{(char)a,(char)b});
            return String.Compare(first,second,StringComparison.Ordinal)+String.Compare(first,first,StringComparison.Ordinal);
        }
        public static int UnsupportedCulture(int a,int b) => String.Compare("a","b",StringComparison.InvariantCulture);
        public static int UnsupportedIgnoreCase(int a,int b) => String.Compare("a","b",StringComparison.OrdinalIgnoreCase);
        public static int UnsupportedUnknown(int a,int b) => String.Compare(null,null,(StringComparison)99);
        public static int UnsupportedParameter(int a,int b) => String.Compare("a","b",(StringComparison)a);
        public static int UnsupportedAlias(int a,int b)
        {
            var owner=new OrdinalCompareState {Mode=StringComparison.Ordinal};
            return String.Compare("a","b",owner.Mode);
        }
    }
}
