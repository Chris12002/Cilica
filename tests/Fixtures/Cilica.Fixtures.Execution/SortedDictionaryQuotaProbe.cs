using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class SortedDictionaryQuotaState
    {
        public SortedDictionary<string,int> Map;
        public SortedDictionary<string,int>.Enumerator Cursor;
        public SortedDictionaryQuotaState(SortedDictionary<string,int> map){Map=map;var cursor=map.GetEnumerator();Cursor=cursor;}
    }
    public static class SortedDictionaryQuotaProbe
    {
        public static SortedDictionaryQuotaState MakeEmpty()=>new SortedDictionaryQuotaState(new SortedDictionary<string,int>(StringComparer.Ordinal));
        public static SortedDictionaryQuotaState MakePopulated()
        {var map=new SortedDictionary<string,int>(StringComparer.Ordinal);map.Add("old",1);return new SortedDictionaryQuotaState(map);}
        public static int Warm()=>"new".Length;
        public static void Add(SortedDictionaryQuotaState state)=>state.Map.Add("new",2);
        public static int Count(SortedDictionaryQuotaState state)=>state.Map.Count;
        public static int Read(SortedDictionaryQuotaState state)=>state.Cursor.MoveNext()?state.Cursor.Current.Value:-1;
        public static int Rejected(SortedDictionaryQuotaState state)
        {try{state.Cursor.MoveNext();return 0;}catch(InvalidOperationException){return 1;}}
        public static int Run(int a,int b)
        {var state=a==0?MakeEmpty():MakePopulated();int value=Read(state);Add(state);return value+Count(state)+Rejected(state)+Warm();}
    }
}
