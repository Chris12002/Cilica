using System;
namespace Cilica.Fixtures.Execution
{
    public static class StringSplitProbe
    {
        private static int Text(string value)
        { if(value==null)return 13;int result=value.Length;for(int i=0;i<value.Length;i++)result=unchecked(result*31+value[i]);return result; }
        private static int Read(string source,char separator,int flags)
        {
            try {
                var result=source.Split(separator,(StringSplitOptions)flags);int hash=result.Length;
                hash=unchecked(hash*31+(ReferenceEquals(result,Array.Empty<string>())?1:2));
                for(int i=0;i<result.Length;i++)hash=unchecked(hash*31+Text(result[i])*17+(ReferenceEquals(result[i],source)?3:5)+(ReferenceEquals(result[i],string.Empty)?7:11));
                return hash;
            }
            catch(ArgumentException e){return unchecked(Text(e.ParamName)*31+Text(e.Message)*17);}
            catch(NullReferenceException){return 19;}
        }
        public static int Basic(int a,int b)
        {
            string value=a==0?"":a==1?":":a==2?"::":a==3?"a:b":a==4?":a::b:":a==5?"plain":a==6?" \t : \u00a0 :x ":a==7?"\ud800:\udc00":a==8?"a\0b\0":null;
            return Read(value,':',b);
        }
        public static int Separators(int a,int b)
        { return Read(" a:b\t\u0085\u00a0\ud800x\udc00\0y ",(char)a,b); }
        public static int Whitespace(int a,int b)
        { char unit=(char)a;string value=new string(new[]{unit,'x',unit,':',unit,':','y',unit});return Read(value,':',b); }
        public static int Dynamic(int a,int b)
        { string value=a.ToString()+":"+b.ToString()+"::"+a.ToString();return Read(value,':',b&3); }
        public static int Identity(int a,int b)
        {
            string value=new string(new[]{' ',(char)a,' ',':',' ',(char)b,' '});var first=value.Split(':',(StringSplitOptions)(b&3));var second=value.Split(':',(StringSplitOptions)(b&3));
            int result=ReferenceEquals(first,second)?1:2;
            for(int i=0;i<first.Length;i++)result=unchecked(result*31+Text(first[i])+(ReferenceEquals(first[i],second[i])?3:5));return result;
        }
        public static int EmptyIdentity(int a,int b)
        { return unchecked(Read("::",':',b)+Read("",':',b)*31+Read(" \t",':',b)*17); }
        public static int UnsupportedCount(int a,int b) => "a:b".Split(':',a,StringSplitOptions.None).Length;
        public static int UnsupportedArray(int a,int b) => "a:b".Split(new[]{':'},StringSplitOptions.None).Length;
    }
}
