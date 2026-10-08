using System;
namespace Cilica.Fixtures.Execution
{
    public struct CandidateLengthValue { public string Text; public int Number; }
    // Independent reductions; no upstream source retained.
    public static class CandidateArrayLengthBranchProbe
    {
        public static int EmptyPair(int first, int second)
        {
            var left = new int[first & 3];
            var right = new int[second & 3];
            if (left.Length == 0 && right.Length == 0) return 1;
            return 0;
        }
        private static int[] Make(int length, ref int order)
        {
            order = unchecked(order * 10 + 1);
            return length < 0 ? null : new int[length & 3];
        }
        public static int ShortCircuit(int first, int second)
        {
            int order = 0;
            try
            {
                if (Make(first, ref order).Length != 0 || Make(second, ref order).Length != 0) return order * 10 + 1;
                return order * 10 + 2;
            }
            catch (NullReferenceException) { return order * 10 + 3; }
        }
        public static int Cleanup(int first, int second)
        {
            int order = 0, result = 0;
            try
            {
                try { if (Make(first, ref order).Length == 0 && Make(second, ref order).Length == 0) result = 7; else result = 8; }
                finally { order = order * 10 + 4; }
            }
            catch (NullReferenceException) { result = 9; }
            return order * 10 + result;
        }
        public static int ElementKinds(int first, int second)
        {
            var bytes = new byte[first & 3];
            var chars = new char[second & 3];
            var flags = new bool[first & 3];
            var references = new string[second & 3];
            var values = new CandidateLengthValue[first & 3];
            int result = 0;
            if (bytes.Length != 0) result += 1;
            if (chars.Length == 0) result += 2;
            if (flags.Length != 0) result += 4;
            if (references.Length == 0) result += 8;
            if (values.Length != 0) result += 16;
            return result;
        }
    }
}
