using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class CandidateIndexBox { public int Value; }
    public struct CandidateIndexValue { public int Value; public CandidateIndexBox Box; }
    public sealed class CandidateHashAlgorithmOwner : System.Security.Cryptography.HashAlgorithm
    {
        private static readonly CandidateIndexBox State=new CandidateIndexBox{Value=19};
        public static int StaticRoot(int a,int b){var values=new uint[]{unchecked((uint)a),unchecked((uint)b)};return unchecked((int)values[(long)(a&1)]+State.Value);}
        public override void Initialize(){ }
        protected override void HashCore(byte[] values,int offset,int count){ }
        protected override byte[] HashFinal()=>new byte[4];
    }
    public static class CandidateNativeIndexProbe
    {
        private static long Edge(int a)
        {
            switch(a&15){case 0:return 0;case 1:return 1;case 2:return 2;case 3:return -1;case 4:return int.MinValue;case 5:return int.MaxValue;case 6:return (long)int.MaxValue+1;case 7:return uint.MaxValue;case 8:return 1L<<32;case 9:return (1L<<32)+1;case 10:return long.MaxValue;case 11:return long.MinValue;case 12:return -4294967295L;case 13:return 3;case 14:return 255;default:return 4095;}
        }
        private static int[] Receiver(ref int order,int b){order=order*10+1;return b<0?null:new[]{17,29};}
        private static long Index(ref int order,int a){order=order*10+2;return Edge(a);}
        private static int Value(ref int order,int b){order=order*10+3;if(b==0)throw new ArgumentException();return b;}
        public static int SignedLoad(int a,int b)
        {int order=0;try{return Receiver(ref order,b)[Index(ref order,a)]+order*100;}catch(OverflowException){return order*100+1;}catch(NullReferenceException){return order*100+2;}catch(IndexOutOfRangeException){return order*100+3;}}
        public static int SignedStore(int a,int b)
        {int order=0;try{Receiver(ref order,b)[Index(ref order,a)]=Value(ref order,b);return order*100;}catch(OverflowException){return order*100+1;}catch(NullReferenceException){return order*100+2;}catch(IndexOutOfRangeException){return order*100+3;}catch(ArgumentException){return order*100+4;}}
        public static int UnsignedLoad(int a,int b)
        {int order=0;try{return Receiver(ref order,b)[unchecked((ulong)Index(ref order,a))]+order*100;}catch(OverflowException){return order*100+1;}catch(NullReferenceException){return order*100+2;}catch(IndexOutOfRangeException){return order*100+3;}}
        public static int UnsignedStore(int a,int b)
        {int order=0;try{Receiver(ref order,b)[unchecked((ulong)Index(ref order,a))]=Value(ref order,b);return order*100;}catch(OverflowException){return order*100+1;}catch(NullReferenceException){return order*100+2;}catch(IndexOutOfRangeException){return order*100+3;}catch(ArgumentException){return order*100+4;}}
        public static int ReferenceStore(int a,int b)
        {object[] v=new CandidateIndexBox[2];try{v[Edge(a)]=new object();return 0;}catch(IndexOutOfRangeException){return 1;}catch(ArrayTypeMismatchException){return 2;}catch(OverflowException){return 3;}}
        public static int ValueAddress(int a,int b)
        {var v=new CandidateIndexValue[2];try{v[Edge(a)].Value=b;v[Edge(a)].Box=new CandidateIndexBox{Value=a};return v[Edge(a)].Value+v[Edge(a)].Box.Value;}catch(IndexOutOfRangeException){return -1;}catch(OverflowException){return -2;}}
        public static int FillTable(int a,int b)
        {var table=new uint[4096];for(int t=0;t<16;t++)for(uint i=0;i<256;i++)table[t*256+i]=(i^unchecked((uint)a))>>(t&31);return unchecked((int)table[(b&15)*256+(unchecked((uint)a)&255)]);}
        public static int NativeLocal(int a,int b){nint index=checked((nint)Edge(a));return (new int[2])[index];}
    }
}
