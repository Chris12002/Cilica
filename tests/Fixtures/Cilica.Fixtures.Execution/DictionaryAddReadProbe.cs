using System;
using System.Collections.Generic;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public sealed class DictionaryTextTrace { public int Value; public Dictionary<DictionaryTextKey,int> Owner; }
    public struct DictionaryTextKey : IEquatable<DictionaryTextKey>
    {
        public int Id, Value, Mode;
        public DictionaryTextTrace Trace;
        public bool Equals(DictionaryTextKey other)
        {
            Trace.Value=unchecked(Trace.Value*5+2); Value=unchecked(Value+11);
            if(Mode==4) throw new InvalidOperationException("equal callback");
            return Id==other.Id;
        }
        public override bool Equals(object other) => other is DictionaryTextKey key && Equals(key);
        public override int GetHashCode() { Trace.Value=unchecked(Trace.Value*5+1); Value=unchecked(Value+7); if(Mode==5)throw new InvalidOperationException("hash callback"); return 1; }
        public override string ToString()
        {
            Trace.Value=unchecked(Trace.Value*5+3); Value=unchecked(Value+13);
            if(Mode==6 && Trace.Owner!=null)
            {
                var inserted=new DictionaryTextKey();inserted.Id=~Id;inserted.Value=Value;inserted.Trace=Trace;
                Trace.Owner.Add(inserted,31);
            }
            if(Mode==2)throw new InvalidOperationException("text callback");
            if(Mode==3)return null;
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }
    public struct FormattableDictionaryKey : IEquatable<FormattableDictionaryKey>, IFormattable
    {
        public bool Equals(FormattableDictionaryKey other)=>true;
        public override int GetHashCode()=>1;
        public override string ToString()=>"ordinary";
        public string ToString(string format,IFormatProvider provider)=>"formatted";
    }
    public static class DictionaryAddReadProbe
    {
        private static int Observe(string text) { if(text==null)return 0; int h=17; for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]); return h; }
        public static int Int32(int a,int b)
        {
            var d=new Dictionary<int,int>();d.Add(a,b); int result=d[a];
            try{d.Add(a,a);}catch(ArgumentException e){result=unchecked(result+Observe(e.Message)+Observe(e.ParamName));}
            try{return unchecked(result+d[~a]);}catch(KeyNotFoundException e){return unchecked(result+Observe(e.Message)+d.Count);}
        }
        public static int UInt32(int a,int b)
        {
            uint key=(uint)a;var d=new Dictionary<uint,int>();d.Add(key,b); int result=d[key];
            try{d.Add(key,a);}catch(ArgumentException e){result=unchecked(result+Observe(e.Message));}
            try{return unchecked(result+d[~key]);}catch(KeyNotFoundException e){return unchecked(result+Observe(e.Message)+d.Count);}
        }
        public static int Int64(int a,int b)
        {
            long key=((long)a<<32)|(uint)b;var d=new Dictionary<long,int>();d.Add(key,b); int result=d[key];
            try{d.Add(key,a);}catch(ArgumentException e){result=unchecked(result+Observe(e.Message));}
            try{return unchecked(result+d[~key]);}catch(KeyNotFoundException e){return unchecked(result+Observe(e.Message)+d.Count);}
        }
        public static int UInt64(int a,int b)
        {
            ulong key=((ulong)(uint)a<<32)|(uint)b;var d=new Dictionary<ulong,int>();d.Add(key,b); int result=d[key];
            try{d.Add(key,a);}catch(ArgumentException e){result=unchecked(result+Observe(e.Message));}
            try{return unchecked(result+d[~key]);}catch(KeyNotFoundException e){return unchecked(result+Observe(e.Message)+d.Count);}
        }
        public static int Strings(int a,int b)
        {
            string key=a<0?"":"A\0\ud800"; var d=new Dictionary<string,int>();d.Add(key,b);int result=d[key];
            string copy=a<0?"":string.Concat("A","\0\ud800");
            try{d.Add(copy,a);}catch(ArgumentException e){result=unchecked(result+Observe(e.Message));}
            try{return unchecked(result+d["missing"]);}catch(KeyNotFoundException e){return unchecked(result+Observe(e.Message));}
        }
        public static int NullOrder(int a,int b)
        {
            var d=new Dictionary<string,int>();if(a<0)d.Add("existing",b);
            try{d.Add(null,b);return 0;}catch(ArgumentNullException e){
                int result=Observe(e.Message);
                try{return result+d[null];}catch(ArgumentNullException other){return unchecked(result+Observe(other.Message)+d.Count);}
            }
        }
        public static int Mutation(int a,int b)
        {
            var d=new Dictionary<int,int>(); for(int i=0;i<12;i++)d.Add(i,unchecked(a+i));
            for(int i=0;i<6;i++)d.Remove(i);for(int i=0;i<6;i++)d.Add(i,unchecked(b+i));
            int result=0;for(int i=0;i<12;i++)result=unchecked(result*3+d[i]);
            var cursor=d.GetEnumerator();cursor.MoveNext();
            try{d.Add(7,b);}catch(ArgumentException e){result=unchecked(result+Observe(e.Message));}
            while(cursor.MoveNext())result=unchecked(result+cursor.Current.Value);
            d[7]=a;return unchecked(result+d[7]+d.Count);
        }
        public static int Payloads(int a,int b)
        {
            var d=new Dictionary<int,(long,TupleFiveCell)>(); var cell=new TupleFiveCell(a);
            d.Add(a,(((long)a<<32)|(uint)b,cell));var copy=d[a];copy.Item2.Value=b;
            d.Remove(a);d.Add(a,(b,cell));return unchecked((int)(copy.Item1>>32)+copy.Item2.Value*3+(int)d[a].Item1*7);
        }
        public static int NamedDuplicate(int a,int b)
        {
            var trace=new DictionaryTextTrace();var key=new DictionaryTextKey();key.Id=a;key.Value=b;key.Trace=trace;
            var d=new Dictionary<DictionaryTextKey,int>();d.Add(key,17);key.Mode=b&7;
            int result;
            try{d.Add(key,19);result=0;}catch(ArgumentException e){result=Observe(e.Message);}catch(InvalidOperationException e){result=Observe(e.Message);}
            return unchecked(result+trace.Value*31+key.Value*7+d.Count);
        }
        public static int NamedMissing(int a,int b)
        {
            var trace=new DictionaryTextTrace();var key=new DictionaryTextKey();key.Id=a;key.Value=b;key.Trace=trace;
            var d=new Dictionary<DictionaryTextKey,int>();if(a<0)d.Add(key,17);key.Id=~a;key.Mode=b&7;
            int result;
            try{result=d[key];}catch(KeyNotFoundException e){result=Observe(e.Message);}catch(InvalidOperationException e){result=Observe(e.Message);}
            return unchecked(result+trace.Value*31+key.Value*7+d.Count);
        }
        public static int NamedReentrantDuplicate(int a,int b)
        {
            var trace=new DictionaryTextTrace();var key=new DictionaryTextKey();key.Id=a;key.Value=b;key.Trace=trace;key.Mode=6;
            var d=new Dictionary<DictionaryTextKey,int>();d.Add(key,17);trace.Owner=d;
            int result;
            try{d.Add(key,19);result=0;}catch(ArgumentException e){result=Observe(e.Message);}
            return unchecked(result+trace.Value*31+d.Count*7+key.Value*11);
        }
        public static int UnsupportedFormattable(int a,int b)
        {var d=new Dictionary<FormattableDictionaryKey,int>();d.Add(new FormattableDictionaryKey(),a);return d.Count;}
    }
}
