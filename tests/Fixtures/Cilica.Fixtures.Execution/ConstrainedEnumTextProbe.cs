using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public enum OtherTextEnum { Zero = 0, ΔOne = 1, High = int.MinValue }
    public sealed class EnumTextHolder { public TextEnum Value; }
    public static class ConstrainedEnumTextProbe
    {
        private enum HiddenEnum { Zero=0, Negative=-1 }
        public static int Hidden(int a,int b)=>Hash(Generic((HiddenEnum)a),b);
        private static int Hash(string text,int seed) { for(int i=0;i<text.Length;i++)seed=unchecked(seed*31+text[i]);return seed; }
        private static string Generic<T>(T value) where T:struct => value.ToString();
        private static void Warm() { TextEnum.Zero.ToString(); }
        public static int Direct(int a,int b)=>Hash(((TextEnum)a).ToString(),b);
        public static int Empty(int a,int b)=>Hash(((EmptyTextEnum)a).ToString(),b);
        public static int Identity(int a,int b)
        {
            string first=((TextEnum)a).ToString(),second=((TextEnum)a).ToString();
            return ((object)first==(object)second?1:0)+((object)first==(object)a.ToString(CultureInfo.InvariantCulture)?2:0)+((object)first==(object)"Zero"?4:0);
        }
        public static int GenericCall(int a,int b)=>Hash(Generic((TextEnum)a),b);
        public static int Storage(int a,int b)
        {
            var holder=new EnumTextHolder();holder.Value=(TextEnum)a;
            var array=new TextEnum[]{(TextEnum)b};
            string first=holder.Value.ToString();var copied=array[0];string second=copied.ToString();
            holder.Value=(TextEnum)b;array[0]=(TextEnum)a;
            copied=array[0];return Hash(first,Hash(second,Hash(holder.Value.ToString(),Hash(copied.ToString(),17))));
        }
        public static int Faults(int a,int b)
        {
            Warm();
            EnumTextHolder holder=a==0?null:new EnumTextHolder();
            try { return Hash(holder.Value.ToString(),b); } catch(NullReferenceException) { return 47; }
        }
        public static int CacheSeparation(int a,int b)
        {
            string first=((TextEnum)a).ToString(),second=((OtherTextEnum)a).ToString();
            return ((object)first==(object)second?1:0)+Hash(first,Hash(second,b));
        }
        public static int Lowercase(int a,int b)=>Hash(((TextEnum)a).ToString().ToLowerInvariant(),b);
        public static int UnsupportedArrayAddress(int a,int b) { var values=new TextEnum[]{(TextEnum)a};return Hash(values[0].ToString(),b); }
        public static int UnsupportedFlags(int a,int b)=>Hash(((FlagTextEnum)a).ToString(),b);
        public static int UnsupportedAliases(int a,int b)=>Hash(((AliasTextEnum)a).ToString(),b);
        public static int UnsupportedWide(int a,int b)=>Hash(((WideTextEnum)a).ToString(),b);
        public static int UnsupportedBoxed(int a,int b) { object value=(TextEnum)a;return Hash(value.ToString(),b); }
    }
}
