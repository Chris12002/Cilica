#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public class OrdinalOrderRow
    {
        public int Id, Key;
        public string Text;
        public OrdinalOrderRow(int id,int key,string text) { Id=id;Key=key;Text=text; }
    }
    public sealed class DerivedOrdinalRow : OrdinalOrderRow { public DerivedOrdinalRow(int id):base(id,0,"x") {} }
    public sealed class OrdinalOrderTrace
    {
        public int Trace, Fault=-1, Mode;
        public string Text(OrdinalOrderRow row) { Trace=unchecked(Trace*31+row.Id);if(row.Id==Fault)throw new InvalidOperationException("selector");return row.Text; }
        public int Key(OrdinalOrderRow row) { Trace=unchecked(Trace*37+row.Id);return row.Key; }
        public int DerivedKey(DerivedOrdinalRow row) => row.Key;
        public int Id(OrdinalOrderRow row) { Trace=unchecked(Trace*41+row.Id);return row.Id; }
        public IEnumerable<OrdinalOrderRow> Source(OrdinalOrderRow[] rows)
        {
            try { for(int i=0;i<rows.Length;i++) { Trace=unchecked(Trace*43+i);if(Mode==1&&i==3)throw new InvalidOperationException("source");yield return rows[i]; } }
            finally { Trace=unchecked(Trace*47+1);if(Mode==2)throw new InvalidOperationException("dispose"); }
        }
    }
    public sealed class CustomOrdinalOrdered : IOrderedEnumerable<OrdinalOrderRow>
    {
        public IOrderedEnumerable<OrdinalOrderRow> CreateOrderedEnumerable<T>(Func<OrdinalOrderRow,T> selector,IComparer<T> comparer,bool descending) => this;
        public IEnumerator<OrdinalOrderRow> GetEnumerator() => ((IEnumerable<OrdinalOrderRow>)new OrdinalOrderRow[0]).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public static class OrdinalOrderingProbe
    {
        private static OrdinalOrderRow[] Rows(int a,int b)
        {
            var text=new string[]{null,"z","a","a","é","\ud800","\ud834\udd1e","a\0z"};
            var rows=new OrdinalOrderRow[16];for(int i=0;i<rows.Length;i++)rows[i]=new OrdinalOrderRow(i,unchecked(i+a)%3,text[(i+b)&7]);return rows;
        }
        private static int Hash(string text) { if(text==null)return 13;int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        private static int Fold(IEnumerable<OrdinalOrderRow> rows,int seed) { foreach(var row in rows)seed=unchecked(seed*31+row.Id*7+row.Key+Hash(row.Text));return seed; }
        public static int Ascending(int a,int b) { var trace=new OrdinalOrderTrace();var query=Rows(a,b).OrderBy(trace.Text,StringComparer.Ordinal);return unchecked(Fold(query,17)+trace.Trace); }
        public static int Descending(int a,int b) { var trace=new OrdinalOrderTrace();var query=Rows(a,b).OrderByDescending(trace.Text,StringComparer.Ordinal);return unchecked(Fold(query.ToArray(),17)+trace.Trace); }
        public static int Chain(int a,int b) { var trace=new OrdinalOrderTrace();var query=Rows(a,b).OrderBy(trace.Key).ThenBy(trace.Text,StringComparer.Ordinal).ThenByDescending(trace.Id);return unchecked(Fold(query,17)+trace.Trace); }
        public static int StringChain(int a,int b) { var trace=new OrdinalOrderTrace();var query=Rows(a,b).OrderBy(trace.Text,StringComparer.Ordinal).ThenByDescending(trace.Key).ThenByDescending(trace.Text,StringComparer.Ordinal);return unchecked(Fold(query.ToArray(),17)+trace.Trace); }
        public static int Repeated(int a,int b) { var trace=new OrdinalOrderTrace();var rows=Rows(a,b);var query=rows.OrderByDescending(trace.Key).ThenByDescending(trace.Text,StringComparer.Ordinal);int h=Fold(query,17);rows[0].Text="changed";h=Fold(query.ToList(),h);return unchecked(h+trace.Trace); }
        public static int Terminals(int a,int b) { var trace=new OrdinalOrderTrace();var rows=Rows(a,b);var query=rows.OrderBy(trace.Key).ThenBy(trace.Text,StringComparer.Ordinal);int h=trace.Trace;h=unchecked(h*31+query.Count()+(query.Any()?1:0));h=unchecked(h*31+query.First().Id);h=unchecked(h*31+query.FirstOrDefault().Id);return unchecked(h+trace.Trace); }
        public static int PrimaryFirst(int a,int b) { var trace=new OrdinalOrderTrace();var query=Rows(a,b).OrderByDescending(trace.Text,StringComparer.Ordinal);return unchecked(query.First().Id+trace.Trace); }
        public static int SourceFault(int a,int b) { var trace=new OrdinalOrderTrace{Mode=b&3};int h=0;try{h=Fold(trace.Source(Rows(a,b)).OrderBy(trace.Text,StringComparer.Ordinal),17);}catch(InvalidOperationException e){h=Hash(e.Message);}return unchecked(h+trace.Trace); }
        public static int SelectorFault(int a,int b) { var trace=new OrdinalOrderTrace{Fault=b&15};int h=0;try{h=Fold(Rows(a,b).OrderBy(trace.Key).ThenBy(trace.Text,StringComparer.Ordinal),17);}catch(InvalidOperationException e){h=Hash(e.Message);}return unchecked(h+trace.Trace); }
        public static int Cursor(int a,int b) { var trace=new OrdinalOrderTrace();var query=Rows(a,b).OrderBy(trace.Text,StringComparer.Ordinal);var first=query.GetEnumerator();var second=query.GetEnumerator();int h=0;while(first.MoveNext()){h=unchecked(h*31+first.Current.Id);if(second.MoveNext())h=unchecked(h*17+second.Current.Id);}first.Dispose();second.Dispose();return unchecked(h+trace.Trace); }
        public static int Validation(int a,int b)
        {
            var trace=new OrdinalOrderTrace();IEnumerable<OrdinalOrderRow> source=a<0?null:Rows(a,b);Func<OrdinalOrderRow,string> selector=b<0?null:new Func<OrdinalOrderRow,string>(trace.Text);
            try{return Fold(source.OrderBy(selector,StringComparer.Ordinal),17);}catch(ArgumentNullException e){return unchecked(Hash(e.ParamName)*31+Hash(e.Message));}
        }
        public static int ThenValidation(int a,int b)
        {
            var trace=new OrdinalOrderTrace();IOrderedEnumerable<OrdinalOrderRow> source=a<0?null:Rows(a,b).OrderBy(trace.Key);Func<OrdinalOrderRow,string> selector=b<0?null:new Func<OrdinalOrderRow,string>(trace.Text);
            // Warm the default integer comparer so exact retained roots do not depend on the branch.
            var warm=Comparer<int>.Default;
            try{return Fold(source.ThenByDescending(selector,StringComparer.Ordinal),17);}catch(ArgumentNullException e){return unchecked(Hash(e.ParamName)*31+Hash(e.Message));}
        }
        public static int EmptySingle(int a,int b)
        {
            var empty=((IEnumerable<OrdinalOrderRow>)new OrdinalOrderRow[0]).GetEnumerator();empty.Dispose();
            var trace=new OrdinalOrderTrace();var rows=new OrdinalOrderRow[a&1];if(rows.Length!=0)rows[0]=new OrdinalOrderRow(0,b,null);
            var query=rows.OrderBy(trace.Text,StringComparer.Ordinal);int h=Fold(query,17);h=Fold(query.ToArray(),h);var first=query.FirstOrDefault();return unchecked(h+(first==null?11:first.Id)+trace.Trace);
        }
        public static int CovariantSource(int a,int b)
        {
            var trace=new OrdinalOrderTrace();IOrderedEnumerable<OrdinalOrderRow> source=new[]{new DerivedOrdinalRow(a)}.OrderBy(trace.DerivedKey);
            return Fold(source.ThenBy(trace.Text,StringComparer.Ordinal),17);
        }
        public static int Aliased(int a,int b) { var trace=new OrdinalOrderTrace();var comparer=StringComparer.Ordinal;return Fold(Rows(a,b).OrderBy(trace.Text,comparer),17); }
        public static int NullComparer(int a,int b) { var trace=new OrdinalOrderTrace();return Fold(Rows(a,b).OrderBy(trace.Text,(IComparer<string>)null),17); }
        public static int CustomSource(int a,int b) { var trace=new OrdinalOrderTrace();return Fold(new CustomOrdinalOrdered().ThenBy(trace.Text,StringComparer.Ordinal),17); }
    }
}
