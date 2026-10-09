using System;
using Cilica.Fixtures.Execution;
namespace Cilica.CandidateValidation
{
    // Owned reduced reference workload only. No CircularBuffer dependency.
    public static class SegmentReferenceCases
    {
        public static int Run(int scenario, int observation)
        {
            if (scenario < 0 || scenario >= 6) throw new ArgumentOutOfRangeException("scenario");
            if (observation == -1) return 7;
            int a, b;
            switch (observation)
            {
                case 0: a = 0; b = 0; break;
                case 1: a = 2; b = 7; break;
                case 2: a = -3; b = 5; break;
                case 3: a = 7; b = -2; break;
                case 4: a = int.MinValue; b = 1; break;
                case 5: a = int.MaxValue; b = -1; break;
                case 6: a = -1; b = int.MinValue; break;
                default: throw new ArgumentOutOfRangeException("observation");
            }
            switch (scenario)
            {
                case 0: return SegmentCompositionProbe.SegmentList(a,b);
                case 1: return SegmentCompositionProbe.CopyViews(a,b);
                case 2: return SegmentCompositionProbe.DirectPropertyCopy(a,b);
                case 3: return SegmentCompositionProbe.LocalPropertyCopy(a,b);
                case 4: return SegmentCompositionProbe.IteratorViews(a,b);
                default: return SegmentCompositionProbe.CursorViews(a,b);
            }
        }
    }
}
