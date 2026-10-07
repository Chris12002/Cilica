using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class OrdinalDictionaryCapacityProbe
    {
        private static string Word(int index)
        {
            switch(index&7) {
                case 0:return "";case 1:return "a";case 2:return "A";case 3:return "a\0z";
                case 4:return "é";case 5:return "e\u0301";case 6:return "\ud800";default:return "\ud83d\ude00";
            }
        }
        private static int Hash(string text) { int hash=17;for(int i=0;i<text.Length;i++)hash=unchecked(hash*31+text[i]);return hash; }
        public static int Cases(int a,int b)
        {
            var map=new Dictionary<string,int>((a & 127), StringComparer.Ordinal);
            for(int i=0;i<12;i++)map[Word(i+a)]=unchecked(i+b);
            int result=map.Count;map.Remove(Word(b));map.Add(Word(b),a);
            foreach(var item in map)result=unchecked(result*31+Hash(item.Key)+item.Value);
            try{map.Add(Word(a),b);}catch(ArgumentException e){result=unchecked(result+Hash(e.Message));}
            try{map.ContainsKey(null);}catch(ArgumentNullException e){result=unchecked(result+Hash(e.Message)+Hash(e.ParamName));}
            return result;
        }
        public static int Identity(int a,int b)
        {
            var first=new string(new char[]{'a',(char)(a&127)});var second=new string(new char[]{'a',(char)(a&127)});
            var map=new Dictionary<string,ValueVectorBox>((a & 127), StringComparer.Ordinal);var original=new ValueVectorBox(a);
            map.Add(first,original);var replacement=new ValueVectorBox(b);map[second]=replacement;
            var cursor=map.GetEnumerator();cursor.MoveNext();var pair=cursor.Current;
            return unchecked(pair.Value.Number+(Object.ReferenceEquals(pair.Key,first)?43:47)+(map[second]==replacement?53:59));
        }
        public static int Values(int a,int b)
        {
            var reference=new ValueVectorBox(b);var value=new HiddenReadonlyPayload(a,reference);
            var map=new Dictionary<string,HiddenReadonlyPayload>((a & 127), StringComparer.Ordinal);map.Add(Word(a),value);
            map.TryGetValue(Word(a),out var copied);map.Clear();var pressure=new ValueVectorBox(61);
            return unchecked(copied.Number+copied.Reference.Number+pressure.Number+(copied.Reference==reference?67:71));
        }
        public static int Version(int a,int b)
        {
            var map=new Dictionary<string,int>((a & 127), StringComparer.Ordinal);map.Add("a",a);var cursor=map.GetEnumerator();cursor.MoveNext();
            int result=cursor.Current.Value;try{map.Add("a",b);}catch(ArgumentException){result=unchecked(result+73);}
            map["a"]=b;result=unchecked(result+(cursor.MoveNext()?79:83));map.Add("b",a);
            try{cursor.MoveNext();return -997;}catch(InvalidOperationException){return result;}
        }
        public static int Negative(int a,int b)
        {
            try { return new Dictionary<string,int>(a < 0 ? a : -1, StringComparer.Ordinal).Count; }
            catch(ArgumentOutOfRangeException e) { return unchecked(Hash(e.Message) + Hash(e.ParamName) + b); }
        }
        private static int Capacity(int a) { if(a < 0) throw new InvalidOperationException("capacity argument"); return a & 127; }
        public static int Order(int a,int b)
        {
            try { var map = new Dictionary<string,int>(Capacity(a), StringComparer.Ordinal); map.Add("a",b); return map["a"]; }
            catch(InvalidOperationException e) { return Hash(e.Message); }
        }
        public static int Empty(int a,int b) => new Dictionary<string,int>(a, StringComparer.Ordinal).Count + b;
        public static int UnsupportedNull(int a,int b)=>new Dictionary<string,int>(a, (IEqualityComparer<string>)null).Count;
        public static int UnsupportedAlias(int a,int b)
        {var comparer=StringComparer.Ordinal;int value=comparer.Compare(Word(a),Word(b));return value+new Dictionary<string,int>(a, comparer).Count;}
        public static int UnsupportedCustom(int a,int b)=>new Dictionary<string,int>(a, new OrdinalSetCustom()).Count;
        public static int UnsupportedGetter(int a,int b)=>new Dictionary<string,int>(a, ForgedOrdinalComparer.Ordinal).Count;
        public static int UnsupportedJoin(int a,int b)=>new Dictionary<string,int>(a, a>0?StringComparer.Ordinal:null).Count;
        public static int UnsupportedInteger(int a,int b)=>new Dictionary<int,int>(a, (IEqualityComparer<int>)null).Count;
    }
}
