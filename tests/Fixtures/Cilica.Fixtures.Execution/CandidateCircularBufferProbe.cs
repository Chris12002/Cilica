using System;
namespace Cilica.Fixtures.Execution
{
    // Independent API reductions; no CircularBuffer implementation is copied.
    public static class CandidateCircularBufferProbe
    {
        public static int WholeCopy(int a,int b)
        {
            var source=new[]{a,b};var target=new int[3];
            Array.Copy(source,target,2);return unchecked(target[0]*31+target[1]);
        }
        public static int OverlapCopy(int a,int b)
        {
            var values=new[]{a,b,17,23};
            Array.Copy(values,0,values,1,3);
            return unchecked(values[1]*31+values[2]*7+values[3]);
        }
        public static int SectionClear(int a,int b)
        {
            var values=new[]{a,b,17,23};Array.Clear(values,1,2);
            return unchecked(values[0]*31+values[1]*7+values[2]+values[3]);
        }
        public static int SegmentView(int a,int b)
        {
            var values=new[]{a,b,17};var segment=new ArraySegment<int>(values,1,2);
            values[1]=unchecked(b+1);
            return unchecked(segment.Array[segment.Offset]*31+segment.Count);
        }
    }
}
