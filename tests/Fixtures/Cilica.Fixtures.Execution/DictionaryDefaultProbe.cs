#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class LookupCallback : IReadOnlyDictionary<int,ProbeNode>
    {
        public int Calls,Mode;
        public ProbeNode Last;
        public bool TryGetValue(int key,out ProbeNode value)
        {
            Calls=unchecked(Calls*31+key);value=new ProbeNode(key);Last=value;
            if(Mode==2)throw new InvalidOperationException("lookup callback");
            return Mode!=0;
        }
        public int Count=>throw new InvalidOperationException("count");
        public ProbeNode this[int key]=>throw new InvalidOperationException("indexer");
        public bool ContainsKey(int key)=>throw new InvalidOperationException("contains");
        public IEnumerable<int> Keys=>throw new InvalidOperationException("keys");
        public IEnumerable<ProbeNode> Values=>throw new InvalidOperationException("values");
        public IEnumerator<KeyValuePair<int,ProbeNode>> GetEnumerator()=>throw new InvalidOperationException("enumerator");
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public static class DictionaryDefaultProbe
    {
        private static int Hash(string text) { if(text==null)return 13;int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        public static int Integers(int a,int b)
        {
            var map=new Dictionary<int,int>();map.Add(a,b);int h=map.GetValueOrDefault(a);h=unchecked(h*31+map.GetValueOrDefault(unchecked(a+1)));h=unchecked(h*31+map.GetValueOrDefault(a,123));return unchecked(h*31+map.GetValueOrDefault(unchecked(a+1),b));
        }
        public static int Wide(int a,int b)
        {
            var map=new Dictionary<long,long>();long key=unchecked(((long)a<<32)|(uint)b),value=unchecked(~key);map.Add(key,value);
            long h=map.GetValueOrDefault(key);h=unchecked(h+map.GetValueOrDefault(key+1)+map.GetValueOrDefault(key+1,value));return unchecked((int)h+(int)(h>>32));
        }
        public static int Values(int a,int b)
        {
            var map=new Dictionary<int,ValueTuple<ProbeNode,int>>();var value=new ValueTuple<ProbeNode,int>(new ProbeNode(a),b);map.Add(a,value);
            var found=map.GetValueOrDefault(a);var missing=map.GetValueOrDefault(unchecked(a+1));var fallback=map.GetValueOrDefault(unchecked(a+1),new ValueTuple<ProbeNode,int>(new ProbeNode(b),a));
            return unchecked(found.Item1.Initial*31+found.Item2+missing.Item2+(missing.Item1==null?13:17)+fallback.Item1.Initial*17+fallback.Item2);
        }
        public static int References(int a,int b)
        {
            var map=new Dictionary<int,ProbeNode>();var fallback=new ProbeNode(b);map.Add(a,null);var found=map.GetValueOrDefault(a,fallback);var missing=map.GetValueOrDefault(unchecked(a+1));var explicitMissing=map.GetValueOrDefault(unchecked(a+1),fallback);
            return (found==null?3:7)+(missing==null?11:13)+(explicitMissing==fallback?17:19);
        }
        public static int Strings(int a,int b)
        {
            var map=new Dictionary<string,int>();map.Add("a",a);int h=map.GetValueOrDefault("a",b)+map.GetValueOrDefault("b",b);
            try{h=map.GetValueOrDefault(null,b);}catch(ArgumentNullException e){h=unchecked(h*31+Hash(e.Message)+Hash(e.ParamName));}return h;
        }
        public static int Callbacks(int a,int b)
        {
            var map=new LookupCallback{Mode=b%3};var fallback=new ProbeNode(b);int h=17;
            try{var value=map.GetValueOrDefault(a,fallback);h=unchecked(h*31+value.Initial+(value==fallback?7:11)+(value==map.Last?13:17));}catch(InvalidOperationException e){h=Hash(e.Message);}
            return unchecked(h*31+map.Calls);
        }
        public static int CallbackDefaults(int a,int b)
        {
            var map=new LookupCallback{Mode=b%3};int h=17;
            try{var value=map.GetValueOrDefault(a);h=unchecked(h*31+(value==null?7:value.Initial));}catch(InvalidOperationException e){h=Hash(e.Message);}
            return unchecked(h*31+map.Calls);
        }
        public static int Null(int a,int b)
        {
            IReadOnlyDictionary<int,int> map=null;int h=17;
            try{h=map.GetValueOrDefault(a);}catch(ArgumentNullException e){h=unchecked(h*31+Hash(e.Message)+Hash(e.ParamName));}
            try{h=map.GetValueOrDefault(a,b);}catch(ArgumentNullException e){h=unchecked(h*31+Hash(e.Message)+Hash(e.ParamName));}return h;
        }
        public static int UnsupportedTryAdd(int a,int b)=>CollectionExtensions.TryAdd(new Dictionary<int,int>(),a,b)?1:0;
        public static int UnsupportedRemove(int a,int b)=>CollectionExtensions.Remove(new Dictionary<int,int>(),a,out int value)?value:b;
    }
}
