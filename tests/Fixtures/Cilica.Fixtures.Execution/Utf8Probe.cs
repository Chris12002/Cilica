using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public static class Utf8Probe
    {
        private static int Hash(byte[] bytes)
        { int result=bytes.Length; for(int i=0;i<bytes.Length;i++) result=unchecked(result*31+bytes[i]); return result; }
        private static string Pair(int a,int b)
        { var chars=new char[2]; chars[0]=unchecked((char)a); chars[1]=unchecked((char)b); return new string(chars); }
        public static int Units(int a,int b) => Hash(Encoding.UTF8.GetBytes(Pair(a,b)));
        public static int ByteAt(int a,int b)
        {
            var chars=new char[1]; chars[0]=unchecked((char)b);
            var bytes=Encoding.UTF8.GetBytes(new string(chars));
            int index=(int)((uint)a%4); return bytes.Length*256+(index<bytes.Length?bytes[index]:0);
        }
        public static int PairByte(int a,int b)
        {
            var chars=new char[2]; chars[0]=(char)(0xd800+((uint)b>>10&1023)); chars[1]=(char)(0xdc00+((uint)b&1023));
            var bytes=Encoding.UTF8.GetBytes(new string(chars)); return bytes.Length*256+bytes[(int)((uint)a%4)];
        }
        public static int Empty(int a,int b)
        { var bytes=Encoding.UTF8.GetBytes(""); return ((object)bytes==Array.Empty<byte>()?11:17)+((object)bytes==(object)Encoding.UTF8.GetBytes("")?23:29); }
        public static int Singleton(int a,int b) => (object)Encoding.UTF8==(object)Encoding.UTF8?17:-1;
        public static int Independent(int a,int b)
        {
            string text=Pair(a,b); var first=Encoding.UTF8.GetBytes(text); var second=Encoding.UTF8.GetBytes(text);
            first[0]=(byte)a; return unchecked(Hash(second)+((object)first==(object)second?-71:37));
        }
        public static int Repeated(int a,int b)
        {
            Encoding encoding=Encoding.UTF8; var first=encoding.GetBytes(Pair(a,b)); byte[] last=first;
            for(int i=0;i<10;i++) last=encoding.GetBytes(Pair(a+i,b));
            return unchecked(Hash(first)+Hash(last)+((object)encoding==(object)Encoding.UTF8?43:17));
        }
        public static int Long(int a,int b)
        {
            var chars=new char[40]; for(int i=0;i<chars.Length;i++) chars[i]=unchecked((char)(i%2==0?a:b));
            return Hash(Encoding.UTF8.GetBytes(new string(chars)));
        }
        public static int Fault(int a,int b)
        {
            Encoding encoding=a<0?null:Encoding.UTF8; string text=b<0?null:"\0a\ufeff";
            try { return Hash(encoding.GetBytes(text)); }
            catch(ArgumentNullException e) { return e.ParamName=="s" && e.Message=="Value cannot be null. (Parameter 's')"?73:79; }
            catch(NullReferenceException) { return 83; }
        }
        public static int Budget(int a,int b) => Encoding.UTF8.GetBytes("\ud800a").Length;
        public static int CastFault(int a,int b)
        {
            object encoding=Encoding.UTF8;
            try { return ((string)encoding).Length; }
            catch(InvalidCastException e) { return e.Message=="Unable to cast object of type 'UTF8EncodingSealed' to type 'System.String'."?89:97; }
        }
        public static int UnsupportedDecoder(int a,int b) => Encoding.UTF8.GetString(new byte[1]).Length;
        public static int UnsupportedOtherEncoding(int a,int b) => Encoding.ASCII.GetBytes("abc").Length;
        public static int UnsupportedSection(int a,int b) => Encoding.UTF8.GetBytes("abc",0,1).Length;
        public static int UnsupportedConfigurable(int a,int b) => new UTF8Encoding(false,true).GetBytes("abc").Length;
    }
}
