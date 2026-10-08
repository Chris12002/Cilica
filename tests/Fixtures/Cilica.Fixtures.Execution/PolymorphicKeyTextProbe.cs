using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public class PolyTextPlain { }
    public class PolyTextDerived : PolyTextPlain { }
    public class PolyTextGeneric<T> : PolyTextPlain { }
    public class PolyTextOverride : PolyTextPlain
    {
        public int Mode;
        public List<int> Trace;
        public Dictionary<PolyTextPlain,int> Owner;
        public override string ToString()
        {
            Trace.Add(17);
            var pressure = new int[4]; pressure[0] = Mode;
            if (Owner != null) { Owner.Clear(); Owner.Add(new PolyTextPlain(), 71); }
            if (pressure[0] == 1) return null;
            if (pressure[0] == 2) throw new InvalidOperationException("callback\0\ud800");
            return string.Concat("derived\0", "\ud800");
        }
    }
    public class PolyTextInherited : PolyTextOverride { }
    public class PolyTextHidden : PolyTextDerived { public new string ToString() => "hidden slot"; }
    public class PolyTextNewSlot : PolyTextDerived { public new virtual string ToString() => "new virtual slot"; }
    public sealed class PolyTextNewSlotLeaf : PolyTextNewSlot { public override string ToString() => "new slot override"; }
    public class PolyTextBaseCall : PolyTextPlain { public override string ToString() => base.ToString(); }
    public class PolyTextBaseCallLeaf : PolyTextBaseCall { }
    public class PolyTextFormattable : PolyTextPlain, IFormattable
    { public string ToString(string format, IFormatProvider provider) => "formatted"; }
    public static class PolymorphicKeyTextProbe
    {
        private class Hidden : PolyTextPlain { }
        private static int Observe(string text)
        { if (text == null) return 0; int h=17; for(int i=0;i<text.Length;i++) h=unchecked(h*31+text[i]); return h; }
        private static PolyTextPlain Plain(int mode)
        {
            switch(mode & 7) {
                case 0:return new PolyTextPlain(); case 1:return new PolyTextDerived();
                case 2:return new PolyTextGeneric<int>(); case 3:return new PolyTextGeneric<string>();
                case 4:return new Hidden(); case 5:return new PolyTextHidden();
                case 6:return new PolyTextNewSlot(); default:return new PolyTextNewSlotLeaf();
            }
        }
        public static int DefaultNames(int a,int b)
        {
            PolyTextPlain value=Plain(a); var map=new Dictionary<PolyTextPlain,int>();map.Add(value,b);
            try {map.Add(value,a);return -1;} catch(ArgumentException e){return unchecked(Observe(e.Message)+Observe(e.ParamName)+map.Count);}
        }
        public static int Missing(int a,int b)
        {
            var map=new Dictionary<PolyTextPlain,int>();map.Add(Plain(b),a);
            try{return map[Plain(a)];}catch(KeyNotFoundException e){return Observe(e.Message);}
        }
        public static int Callbacks(int a,int b)
        {
            var trace=new List<int>();var map=new Dictionary<PolyTextPlain,int>();
            PolyTextOverride value=(a&1)==0?new PolyTextOverride():new PolyTextInherited();
            value.Mode=b&3;value.Trace=trace;map.Add(value,a);if((a&2)!=0)value.Owner=map;
            int result;
            try{map.Add(value,b);result=-1;}catch(ArgumentException e){result=Observe(e.Message);}
            catch(InvalidOperationException e){result=Observe(e.Message);}
            return unchecked(result+trace.Count*97+map.Count*101);
        }
        public static int SortedList(int a,int b)
        {
            var map=new SortedDictionary<string,List<string>>(StringComparer.Ordinal);
            var value=new List<string>();value.Add("a");map.Add("key",value);
            try{map.Add("key",(a&1)==0?value:null);return -1;}catch(ArgumentException e){return unchecked(Observe(e.Message)+Observe(e.ParamName)+map.Count);}
        }
        public static int SortedDerived(int a,int b)
        {
            var map=new SortedDictionary<string,PolyTextPlain>(StringComparer.Ordinal);
            map.Add("key",Plain(a));
            try{map.Add("key",Plain(b));return -1;}catch(ArgumentException e){return Observe(e.Message);}
        }
        public static int NullOrder(int a,int b)
        {
            var map=new Dictionary<PolyTextPlain,int>();
            try{map.Add(null,a);return -1;}catch(ArgumentNullException e){return Observe(e.Message);}
        }
        public static int BaseCall(int a,int b)
        {
            PolyTextBaseCall value=(a&1)==0?new PolyTextBaseCall():new PolyTextBaseCallLeaf();
            return Observe(value.ToString());
        }
        public static int UnsupportedObjectArray(int a,int b) { object value=new int[1]; return value.ToString().Length; }
        public static int UnsupportedObjectBox(int a,int b) { object value=true; return value.ToString().Length; }
        public static int UnsupportedRuntimeType(int a,int b) => typeof(PolyTextPlain).ToString().Length;
        public static int UnsupportedDerivedFormattable(int a,int b)
        {
            var map=new Dictionary<PolyTextPlain,int>();map.Add(new PolyTextFormattable(),a);return map.Count;
        }
    }
}
