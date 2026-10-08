using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class CandidateUInt32Holder { public uint[] Values; }
    public static class CandidateUInt32VectorProbe
    {
        public static int Values(int a,int b) { var v=new uint[4];v[0]=unchecked((uint)a);v[1]=unchecked((uint)b);return unchecked((int)((v[0]>>1)^v[1]^v[2])); }
        private static T Read<T>(T[] a,int i)=>a[i];
        private static void Write<T>(T[] a,int i,T v) { a[i]=v; }
        public static int Generic(int a,int b) { var v=new uint[2];Write(v,0,unchecked((uint)a));Write(v,1,unchecked((uint)b));return unchecked((int)(Read(v,0)/2+Read(v,1))); }
        public static int NullLoad(int a,int b) { try{return unchecked((int)((uint[])null)[a]);}catch(NullReferenceException){return 7;} }
        public static int Widen(int a,int b) { var v=new uint[]{unchecked((uint)a)};long x=v[0];return x>int.MaxValue ? 17 : (int)(x>>1); }
        public static int Heap(int a,int b) { var h=new CandidateUInt32Holder { Values=new uint[]{unchecked((uint)a)} };var alias=h.Values;for(int i=0;i<8;i++){var p=new uint[2];p[0]=unchecked((uint)(b+i));alias[0]=alias[0]^p[0];}return unchecked((int)h.Values[0]); }
        public static int Types(int a,int b) { object v=a<0?(object)new uint[1]:new int[1];return (v is uint[]?1:0)+(v is int[]?2:0)+(v is ArrayEnum[]?4:0)+(v is IList<uint>?8:0)+(v is IList<int>?16:0)+(v is IList<ArrayEnum>?32:0)+(v is byte[]?64:0)+(v.GetType()==new uint[0].GetType()?128:0); }
        public static int Aliases(int a,int b) { object v=new uint[]{unchecked((uint)a),unchecked((uint)b)};var signed=(int[])v;var enums=(ArrayEnum[])v;signed[0]=b;enums[1]=(ArrayEnum)a;return unchecked((int)((uint[])v)[0]+signed[1]); }
        public static int Interfaces(int a,int b) { var v=new uint[]{unchecked((uint)a),unchecked((uint)b)};IList<uint> own=v;var signed=(IList<int>)(object)v;own[0]=unchecked((uint)b);signed[1]=a;return unchecked((int)own[1]+signed[0]+own.Count); }
        public static int Copy(int a,int b) { ICollection<uint> source=new uint[]{unchecked((uint)a),unchecked((uint)b)};var target=(uint[])(object)new int[3];source.CopyTo(target,1);return unchecked((int)(target[1]+target[2])); }
        public static int Search(int a,int b) { IList<uint> v=new uint[]{unchecked((uint)a),unchecked((uint)b)};return v.IndexOf(unchecked((uint)b))+(v.Contains(unchecked((uint)a))?10:0); }
        public static int Enumerate(int a,int b) { var v=new uint[]{unchecked((uint)a),unchecked((uint)b)};var e=((IEnumerable<uint>)v).GetEnumerator();e.MoveNext();uint first=e.Current;v[1]=unchecked((uint)(b+1));e.MoveNext();uint second=e.Current;e.Dispose();return unchecked((int)(first+second)); }
        public static int Boxes(int a,int b) { var v=new uint[]{unchecked((uint)a)};var e=((IEnumerable<int>)(object)v).GetEnumerator();e.MoveNext();object box=((IEnumerator)e).Current;return (int)box+(box is int?7:0)+(box is uint?100:0); }
        public static int ActualBoxes(int a,int b) { var v=new uint[]{unchecked((uint)a)};var e=((IEnumerable)v).GetEnumerator();e.MoveNext();object box=e.Current;return unchecked((int)(uint)box)+(box is uint?7:0)+(box is int?100:0); }
        public static int RequestedBoxes(int a,int b) { var v=new int[]{a};var e=((IEnumerable<uint>)(object)v).GetEnumerator();e.MoveNext();object box=((IEnumerator)e).Current;return unchecked((int)(uint)box)+(box is uint?7:0)+(box is int?100:0); }
        public static int AllocationFaults(int a,int b) { int length=a<0?a:a&3;try{return new uint[length].Length;}catch(OverflowException){return -17;} }
        public static int Empty(int a,int b) { var x=Array.Empty<uint>();return (ReferenceEquals(x,Array.Empty<uint>())?1:0)+(ReferenceEquals(x,Array.Empty<int>())?10:0)+(x.GetType()==new uint[0].GetType()?100:0); }
        public static int Static(int a,int b) { var v=new uint[]{0,1,0x80000000,0xffffffff,0x12345678,0x87654321,2,3};return unchecked((int)v[a&7]); }
        public static int Clear(int a,int b) { var v=new uint[]{unchecked((uint)a)};((IList)v).Clear();return unchecked((int)v[0]); }
        private static uint[] Receiver(ref int order,int mode) { order=order*10+1;return mode<0?null:new uint[2]; }
        private static int Index(ref int order,int index) { order=order*10+2;return index; }
        private static uint Value(ref int order,int mode) { order=order*10+3;if(mode==0)throw new ArgumentException();return unchecked((uint)mode); }
        public static int Faults(int a,int b) { int order=0;try { Receiver(ref order,a)[Index(ref order,b)]=Value(ref order,a);return order*10; }catch(ArgumentException){return order*10+1;}catch(NullReferenceException){return order*10+2;}catch(IndexOutOfRangeException){return order*10+3;} }
        public static int CastMessage(int a,int b) { object v=new uint[1];try{return ((byte[])v).Length;}catch(InvalidCastException e){return e.Message=="Unable to cast object of type 'System.UInt32[]' to type 'System.Byte[]'."?7:-1;} }
        public static int UnsupportedBorrow(int a,int b) { var v=new uint[1];ref uint x=ref v[0];x=unchecked((uint)a);return unchecked((int)x); }
        public static int WideIndex(int a,int b) { var v=new uint[4096];uint i=unchecked((uint)a)&255;int t=b&15;v[t*256+i]=i;return unchecked((int)v[t*256+i]); }
    }
}
