using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class ReplaceTrace { public int Value; public char Character(int n,int marker){Value=unchecked(Value*31+marker);if(n==7)throw new InvalidOperationException("argument");return unchecked((char)n);} }
    public static class CharacterReplaceProbe
    {
        private static int Hash(string value){int h=value.Length;for(int i=0;i<value.Length;i++)h=unchecked(h*31+value[i]);return h;}
        private static string Source(int a,int b)=>new string(new[]{'x',unchecked((char)a),'\0','\ud800','\udfff',unchecked((char)a),unchecked((char)b),'x'});
        public static int Text(int a,int b)
        {
            string source=Source(a,b);string result=source.Replace(unchecked((char)a),unchecked((char)b));
            return unchecked(Hash(result)*31+Hash(source));
        }
        public static int Identity(int a,int b)
        {
            string source=Source(a,b);char old=unchecked((char)a),replacement=unchecked((char)b);
            string equal=source.Replace(old,old);string changed=source.Replace(old,replacement);
            string absent=source.Replace('\u2222','a');string empty="".Replace(old,replacement);
            return (ReferenceEquals(source,equal)?1:0)+(ReferenceEquals(source,changed)?7:0)+(ReferenceEquals(source,absent)?31:0)+(ReferenceEquals(empty,"")?127:0)+Hash(changed);
        }
        public static int Repeated(int a,int b)
        {
            string source=Source(a,b),result=source;
            for(int i=0;i<9;i++)result=result.Replace(unchecked((char)(a+i)),unchecked((char)(b+i)));
            return unchecked(Hash(source)*31+Hash(result));
        }
        public static int Null(int a,int b)
        {
            string source=null;
            try{return source.Replace(unchecked((char)a),unchecked((char)b)).Length;}
            catch(NullReferenceException){return 19;}
        }
        public static int Evaluation(int a,int b)
        {
            var trace=new ReplaceTrace();int result;
            try{result=(a<0?null:Source(a,b)).Replace(trace.Character(a,1),trace.Character(b,2)).Length;}
            catch(NullReferenceException){result=17;}
            catch(InvalidOperationException){result=23;}
            return unchecked(trace.Value*31+result);
        }
        public static int UnsupportedString(int a,int b)=>"aaa".Replace("aa","b").Length;
        public static int UnsupportedComparison(int a,int b)=>"aaa".Replace("aa","b",StringComparison.Ordinal).Length;
    }
}
