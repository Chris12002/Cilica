using System;
namespace Cilica.Fixtures.Execution
{
    public static class CharacterStringProbe
    {
        private static int Hash(string text)
        { int result=text.Length; for(int i=0;i<text.Length;i++) result=unchecked(result*31+text[i]); return result; }
        private static char[] Characters(int a, int b)
        { var chars=new char[5]; chars[0]=unchecked((char)a); chars[1]='\0'; chars[2]='\ud800'; chars[3]='\udfff'; chars[4]=unchecked((char)b); return chars; }
        public static int Copy(int a,int b) => Hash(new string(Characters(a,b)));
        public static int Independent(int a,int b)
        { var chars=Characters(a,b); var text=new string(chars); chars[0]=unchecked((char)b); chars[4]=unchecked((char)a); return unchecked(Hash(text)+Hash(new string(chars))); }
        public static int Identity(int a,int b)
        { var chars=Characters(a,b); var first=new string(chars); var second=new string(chars); return (object)first == (object)second ? a : unchecked(b+Hash(first)); }
        public static int Empty(int a,int b)
        { char[] chars=a<0?null:new char[0]; var text=new string(chars); return ((object)text==(object)string.Empty?3:5)+text.Length+b; }
        public static int Repeated(int a,int b)
        { var chars=Characters(a,b); string first=new string(chars), last=first; for(int i=0;i<9;i++){chars[0]=unchecked((char)(a+i));last=new string(chars);} return unchecked(Hash(first)+Hash(last)); }
        public static int Nested(int a,int b)
        { var text=new string(Characters(a,b)); return Hash(text.Substring(1,3)); }
        public static int UnsupportedSection(int a,int b) => new string(Characters(a,b),0,1).Length;
    }
}
