using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class LinqSkipProbe
    {
        private static int Fold(IEnumerable<int> values)
        { int code=0;foreach(int item in values)code=unchecked(code*17+item);return code; }
        private static int Hash(string text)
        { if(text==null)return -1;int code=0;for(int i=0;i<text.Length;i++)code=unchecked(code*31+text[i]);return code; }
        private static int Error(Exception error)
        { return unchecked(Hash(error.Message)*17+(error is ArgumentException argument?Hash(argument.ParamName):0)); }
        public static int Arrays(int a,int b)
        {
            WarmEmpty();
            var source=new[]{a,17,b,29};var query=source.Skip(b);
            int result=Fold(query);var array=query.ToArray();var list=query.ToList();
            return unchecked(result+Fold(array)*3+Fold(list)*7+list.Capacity*11+query.Count()*13+(query.Any()?19:23)+query.FirstOrDefault());
        }
        private static void WarmEmpty()
        { ((IEnumerable<int>)Array.Empty<int>()).GetEnumerator().Dispose(); }
        private static int ReadQuery(IEnumerable<int> query, int operation)
        {
            try
            {
                switch(operation)
                {
                    case 0: return Fold(query);
                    case 1: return query.Count();
                    case 2: return query.Any()?1:0;
                    case 3: return query.FirstOrDefault();
                    case 4: return Fold(query.ToArray());
                    default: var list=query.ToList();return Fold(list)+list.Capacity;
                }
            }
            catch(InvalidOperationException error) { return Error(error); }
        }
        public static int Generic(int a,int b)
        {
            WarmEmpty();
            var trace=new ManyTrace();var query=new ManySequence(trace,4,17,10,b%5).Skip(a);
            int result=trace.Code;
            for(int operation=0;operation<6;++operation)result=unchecked(result*17+ReadQuery(query,operation));
            return unchecked(result+trace.Code*31);
        }
        public static int Collection(int a,int b)
        {
            WarmEmpty();
            var trace=new ManyTrace();var query=new ManyCollection(trace,3,b,10,0).Skip(a);
            int result=query.Count()+trace.Code;result=unchecked(result*17+(query.Any()?1:0)+trace.Code);
            var list=query.ToList();return unchecked(result*31+Fold(list)+list.Capacity+trace.Code);
        }
        public static int CustomList(int a,int b)
        {
            WarmEmpty();
            var trace=new ManyTrace();var source=new SingleProbeList {Trace=trace,Length=4,Value=17,Fault=b%3};
            var query=source.Skip(a);int result=trace.Code;
            try { result=Fold(query);result=unchecked(result*17+query.Count()+query.FirstOrDefault());
                result=unchecked(result*17+Fold(query.ToArray())+Fold(query.ToList())); }
            catch(InvalidOperationException e){result=unchecked(result*17+Error(e));}
            return unchecked(result+trace.Code*31);
        }
        public static int Mutation(int a,int b)
        {
            var list=new List<int>(new[]{a,17,b});var query=list.Skip(1);var cursor=query.GetEnumerator();
            list.Add(29);cursor.MoveNext();int result=cursor.Current;list[2]=31;
            cursor.MoveNext();result=unchecked(result*17+cursor.Current);list.RemoveAt(0);
            while(cursor.MoveNext())result=unchecked(result*17+cursor.Current);
            cursor.Dispose();return unchecked(result+cursor.Current+Fold(query)*31);
        }
        public static int Identity(int a,int b)
        {
            var array=new[]{a,b};var query=array.Skip(0);var first=query.GetEnumerator();var second=query.GetEnumerator();
            int result=(ReferenceEquals(array,query)?1:2)+(ReferenceEquals(query,first)?4:8)+(ReferenceEquals(query,second)?16:32);
            first.MoveNext();result=unchecked(result*17+first.Current);second.MoveNext();first.Dispose();
            result=unchecked(result*17+first.Current+second.Current);while(second.MoveNext())result=unchecked(result*17+second.Current);
            result=unchecked(result*17+second.Current);second.Dispose();
            return result+(ReferenceEquals(query,query.Skip(-1))?64:128)+(ReferenceEquals(Array.Empty<int>().Skip(a),Array.Empty<int>())?256:512);
        }
        public static int Repeated(int a,int b)
        {
            WarmEmpty();
            var trace=new ManyTrace();var source=new ManySequence(trace,4,b,10,0);
            var list=new List<int>(new[]{a,17,b});
            return unchecked(Fold(source.Skip(a).Skip(b))+Fold(list.Skip(a).Skip(b))*17+trace.Code);
        }
        public static int Interfaces(int a,int b)
        {
            var query=new[]{a,17,b}.Skip(1);var list=(IList<int>)query;var read=(IReadOnlyList<int>)query;
            int result=list.Count+read.Count+read[0]+(list.IsReadOnly?3:5)+list.IndexOf(b)+(list.Contains(b)?7:11);
            try { result=unchecked(result*17+list[b]); }catch(ArgumentException e){result=unchecked(result*17+Error(e));}
            try { list[b]=a; }catch(NotSupportedException e){result=unchecked(result*17+Error(e));}
            try { list.Add(a); }catch(NotSupportedException e){result=unchecked(result*17+Error(e));}
            try { list.RemoveAt(b); }catch(NotSupportedException e){result=unchecked(result*17+Error(e));}
            var copy=new int[5];list.CopyTo(copy,2);return unchecked(result+Fold(copy)*31);
        }
        public static int CopyFailures(int a,int b)
        {
            var list=(ICollection<int>)new[]{1,2,3}.Skip(a);
            int[] target=b<0?null:new int[3];int result=0;
            try { list.CopyTo(target,b);result=target==null?7:Fold(target); }
            catch(ArgumentException e){result=Error(e);}
            return result;
        }
        public static int References(int a,int b)
        {
            var source=new[]{"ABC",null,"XYZ"};var query=source.Skip(a);var copy=query.ToArray();
            int result=copy.Length;foreach(var value in copy)result=unchecked(result*17+(value==null?31:value.Length));
            return result;
        }
        public static int Values(int a,int b)
        {
            var source=new[]{new ManyPayload {Text="abc",Number=a},new ManyPayload {Text="xyz",Number=b}};
            var copy=source.Skip(1).ToArray();source[1]=default;
            return unchecked(copy[0].Text.Length+(int)copy[0].Number);
        }
        public static int MutableEquality(int a,int b)
        {
            var array=new[]{new SearchCell {Number=13},new SearchCell {Number=a},new SearchCell {Number=b}};
            var view=(IList<SearchCell>)array.Skip(1);int result=view.IndexOf(new SearchCell {Number=b});
            result=unchecked(result*17+array[1].Number+array[2].Number);
            var list=new List<SearchCell>(array);view=(IList<SearchCell>)list.Skip(1);
            result=unchecked(result*17+view.IndexOf(new SearchCell {Number=b})+list[1].Number+list[2].Number);
            var readonlyList=new ReadOnlyCollection<SearchCell>(list);view=(IList<SearchCell>)readonlyList.Skip(1);
            return unchecked(result*17+view.IndexOf(new SearchCell {Number=b})+list[1].Number+list[2].Number);
        }
        public static int AllocatingEquality(int a,int b)
        {
            var array=new[]{new AllocatingSearchCell {Number=13},new AllocatingSearchCell {Number=a},new AllocatingSearchCell {Number=b}};
            var view=(IList<AllocatingSearchCell>)array.Skip(1);int result=0;
            try { result=view.IndexOf(new AllocatingSearchCell {Number=b}); }
            catch(InvalidOperationException error) { result=Error(error); }
            return result+(array[1].Payload==null?17:31)+(array[2].Payload==null?37:41);
        }
        public static int Covariance(int a,int b)
        {
            object[] source=new string[]{"first","second"};
            var view=(ICollection<object>)source.Skip(a);object[] target=b<0?(object[])new string[3]:new object[3];
            int result=0;
            try { view.CopyTo(target,b);result=17; }
            catch(ArrayTypeMismatchException error) { result=Error(error); }
            catch(ArgumentException error) { result=Error(error); }
            return result;
        }
        public static int NullCopy(int a,int b)
        {
            var trace=new ManyTrace();IEnumerable<int> source=b<0
                ? (IEnumerable<int>)new SingleProbeList {Trace=trace,Length=3,Value=17} : new[]{1,2,3};
            var view=(ICollection<int>)source.Skip(a);int result=0;
            try { view.CopyTo(null,0);result=17; }
            catch(ArgumentException error) { result=Error(error); }
            return unchecked(result+trace.Code*31);
        }
        public static int Legacy(int a,int b)
        {
            IEnumerable query=new[]{a,17,b}.Skip(1);var cursor=query.GetEnumerator();int result=(int)cursor.Current;
            while(cursor.MoveNext())result=unchecked(result*17+(int)cursor.Current);
            result=unchecked(result*17+(int)cursor.Current);
            try { cursor.Reset(); }catch(NotSupportedException error){result=unchecked(result+Error(error));}
            ((IDisposable)cursor).Dispose();return unchecked(result+(int)cursor.Current);
        }
        public static int Null(int a,int b)
        { try { IEnumerable<int> source=null;source.Skip(a);return 0; }catch(ArgumentNullException e){return Error(e);} }
        public static int UnsupportedConcat(int a,int b) => new[]{a}.Concat(new[]{b}).Skip(1).Count();
        public static int UnsupportedDistinct(int a,int b) => new[]{a,b}.Distinct().Skip(1).Count();
        public static int UnsupportedOfType(int a,int b) => new object[]{a,b}.OfType<int>().Skip(1).Count();
        public static int UnsupportedCast(int a,int b) => new object[]{a,b}.Cast<int>().Skip(1).Count();
        public static int UnsupportedMany(int a,int b) => new[]{a,b}.SelectMany(x=>new[]{x}).Skip(1).Count();
        public static int UnsupportedWhere(int a,int b) => new[]{a,b}.Where(x=>x!=0).Skip(1).Count();
        public static int UnsupportedSelect(int a,int b) => new[]{a,b}.Select(x=>x+1).Skip(1).Count();
        public static int UnsupportedOrdered(int a,int b) => new[]{a,b}.OrderBy(x=>x).Skip(1).Count();
    }
}
