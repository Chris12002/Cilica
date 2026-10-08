using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public static class SortedDictionaryProbe
    {
        public sealed class TextValue
        {
            public string Text;
            public int Number;
            public override string ToString()=>Text;
        }
        public struct RecordValue
        {
            public TextValue Reference;
            public long Wide;
            public override string ToString()=>Reference.Text;
        }
        private static int Hash(string s) { int h=17;if(s==null)return 0;for(int i=0;i<s.Length;i++)h=unchecked(h*31+s[i]);return h; }
        private static string Key(int i) => i==0?"":i==1?"\ud800":i==2?"\udc00":i==3?"\0":i.ToString(CultureInfo.InvariantCulture);
        private static SortedDictionary<string,int> Build(int seed)
        {
            var map=new SortedDictionary<string,int>(StringComparer.Ordinal);
            for(int i=0;i<19;i++){int index=(i*7+(seed&15))%19;map.Add(Key(index),index);}
            return map;
        }
        public static int Ordering(int a,int b)
        {
            var map=Build(a);int h=map.Count;
            foreach(var pair in map)h=unchecked(h*31+Hash(pair.Key)*17+pair.Value);
            int value=91;bool found=map.TryGetValue(Key((b&31)%19),out value);
            return unchecked(h*31+value+(found?37:0)+(map.ContainsKey("missing")?71:0)+(ReferenceEquals(map.Comparer,StringComparer.Ordinal)?11:0));
        }
        public static int Replacement(int a,int b)
        {
            var map=Build(a);var cursor=map.GetEnumerator();cursor.MoveNext();string key=cursor.Current.Key;
            map[key]=b;int h=cursor.Current.Value;
            try{cursor.MoveNext();return -1;}catch(InvalidOperationException e){h=unchecked(h*31+Hash(e.Message));}
            map["new"]=a;return unchecked(h*31+map[key]*17+map.Count);
        }
        public static int Duplicate(int a,int b)
        {
            var map=Build(a);var cursor=map.GetEnumerator();cursor.MoveNext();int h=0;
            try{map.Add(Key((b&31)%19),b);}catch(ArgumentException e){h=unchecked(Hash(e.Message)*31+Hash(e.ParamName));}
            try{cursor.MoveNext();h=-1;}catch(InvalidOperationException e){h=unchecked(h*31+Hash(e.Message));}
            foreach(var p in map)h=unchecked(h*31+p.Value);return h;
        }
        public static int Copies(int a,int b)
        {
            var map=Build(a);var first=map.GetEnumerator();first.MoveNext();var second=first;int h=0;
            for(int i=0;i<25;i++)
            {
                bool move=(i&1)==0?first.MoveNext():second.MoveNext();
                var p=(i&1)==0?first.Current:second.Current;
                h=unchecked(h*31+p.Value+Hash(p.Key)+(move?7:0));
            }
            first.Dispose();return h;
        }
        public static int Interfaces(int a,int b)
        {
            var map=Build(a);IEnumerator cursor=((IEnumerable)map).GetEnumerator();int h=0;
            h+=((IDictionary)map).IsReadOnly?7:13;
            try{var x=cursor.Current;}catch(InvalidOperationException e){h=unchecked(h*31+Hash(e.Message));}
            cursor.MoveNext();h=unchecked(h*31+((KeyValuePair<string,int>)cursor.Current).Value);
            cursor.Reset();cursor.MoveNext();h=unchecked(h*31+((KeyValuePair<string,int>)cursor.Current).Value);
            while(cursor.MoveNext())h=unchecked(h*31+((KeyValuePair<string,int>)cursor.Current).Value);
            try{var x=cursor.Current;}catch(InvalidOperationException e){h=unchecked(h*31+Hash(e.Message));}
            return h;
        }
        public static int Empty(int a,int b)
        {
            var map=new SortedDictionary<string,int>(StringComparer.Ordinal);
            var concrete=map.GetEnumerator();int h=concrete.Current.Value;
            var first=((IEnumerable<KeyValuePair<string,int>>)map).GetEnumerator();
            ((IEnumerator)first).Reset();
            var array=((IEnumerable<KeyValuePair<string,int>>)Array.Empty<KeyValuePair<string,int>>()).GetEnumerator();
            h+=ReferenceEquals(first,array)?23:0;
            var other=((IEnumerable<KeyValuePair<string,int>>)new Dictionary<string,int>()).GetEnumerator();
            h+=ReferenceEquals(first,other)?13:0;
            try{var x=first.Current;}catch(InvalidOperationException e){h=unchecked(h*31+Hash(e.Message));}
            map.Add("later",a);h=unchecked(h*31+(first.MoveNext()?1:3));
            try{concrete.MoveNext();h=-1;}catch(InvalidOperationException e){h=unchecked(h*31+Hash(e.Message));}
            return h;
        }
        public static int LiveValues(int a,int b)
        {
            var map=new SortedDictionary<string,RecordValue>(StringComparer.Ordinal);
            for(int i=0;i<12;i++)map.Add(Key(i),new RecordValue{Reference=new TextValue{Text=Key(i),Number=i},Wide=unchecked(((long)a<<32)|(uint)i)});
            var cursor=map.GetEnumerator();map=null;int h=b;
            while(cursor.MoveNext())
            {
                var value=cursor.Current.Value;var garbage=new int[17];garbage[0]=a;
                h=unchecked(h*31+Hash(value.Reference.Text)+value.Reference.Number+(int)(value.Wide>>32)+garbage[0]);
            }
            return h;
        }
        public static int Faults(int a,int b)
        {
            var map=new SortedDictionary<string,int>(StringComparer.Ordinal);int h=0;int value=81;
            try{map.TryGetValue(null,out value);}catch(ArgumentNullException e){h=unchecked(Hash(e.Message)*31+Hash(e.ParamName));}
            h=unchecked(h*31+value);
            try{map.Add(null,b);}catch(ArgumentNullException e){h=unchecked(h*31+Hash(e.Message));}
            try{map[null]=b;}catch(ArgumentNullException e){h=unchecked(h*31+Hash(e.Message));}
            try{h=map["missing"];}catch(KeyNotFoundException e){h=unchecked(h*31+Hash(e.Message));}
            var cursor=default(SortedDictionary<string,int>.Enumerator);cursor.Dispose();h=unchecked(h*31+cursor.Current.Value);
            try{cursor.MoveNext();h=-1;}catch(NullReferenceException){h=unchecked(h*31+17);}
            return h;
        }
        public static int UnsupportedCursorTypeof(int a,int b)=>typeof(SortedSet<KeyValuePair<string,int>>.Enumerator)==null?1:0;
        public static int UnsupportedInteger(int a,int b)=>new SortedDictionary<int,int>(Comparer<int>.Default).Count;
        public static int UnsupportedDefault(int a,int b)=>new SortedDictionary<string,int>().Count;
        public static int UnsupportedNull(int a,int b)=>new SortedDictionary<string,int>((IComparer<string>)null).Count;
        public static int UnsupportedIgnoreCase(int a,int b)=>new SortedDictionary<string,int>(StringComparer.OrdinalIgnoreCase).Count;
        private static IComparer<string> Comparer()=>StringComparer.Ordinal;
        public static int UnsupportedAlias(int a,int b)=>new SortedDictionary<string,int>(Comparer()).Count;
        public static int UnsupportedRemove(int a,int b)=>new SortedDictionary<string,int>(StringComparer.Ordinal).Remove("a")?1:0;
        public static int UnsupportedSet(int a,int b)=>new SortedSet<string>(StringComparer.Ordinal).Count;
        public static int UnsupportedPolymorphicValue(int a,int b)
        {var map=new SortedDictionary<string,List<string>>(StringComparer.Ordinal);map.Add("key",new List<string>());return map.Count;}
    }
}
