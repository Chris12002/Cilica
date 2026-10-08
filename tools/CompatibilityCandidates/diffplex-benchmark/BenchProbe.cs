// Owned workload adapter; same approved DiffPlex sources and character/line roots.
using System.Collections.Generic;
using DiffPlex;
using DiffPlex.Model;
namespace Cilica.CandidateBenchmark
{
    public static class BenchProbe
    {
        private static readonly string[] Inputs = BuildInputs();
        private static string[] BuildInputs()
        {
            var result = new string[4];
            for (int work = 0; work < 2; work++)
            {
                int size = work == 0 ? 128 : 1024;
                var a = new char[size]; var b = new char[size];
                for (int i = 0; i < size; i++)
                {
                    char c = work == 1 && (i & 15) == 15 ? '\n' : (char)('a' + (i + (i >> 4)) % 26);
                    a[i] = c; b[i] = c;
                }
                b[work == 0 ? 31 : 130] = 'X'; b[work == 0 ? 95 : 770] = 'Y';
                result[work * 2] = new string(a); result[work * 2 + 1] = new string(b);
            }
            return result;
        }
        private static DiffResult Diff(int work)
        {
            var differ = new Differ();
            return work == 0 ? differ.CreateCharacterDiffs(Inputs[0], Inputs[1], false)
                : differ.CreateLineDiffs(Inputs[2], Inputs[3], false);
        }
        private static int Observe(int work, int index)
        {
            var result = Diff(work); var output = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                var pieces = side == 0 ? result.PiecesOld : result.PiecesNew;
                output.Add(pieces.Count);
                for (int i = 0; i < pieces.Count; i++)
                {
                    string text = pieces[i]; output.Add(text.Length);
                    for (int c = 0; c < text.Length; c++) output.Add(text[c]);
                }
            }
            output.Add(result.DiffBlocks.Count);
            for (int i = 0; i < result.DiffBlocks.Count; i++)
            {
                var block = result.DiffBlocks[i];
                output.Add(block.DeleteStartA); output.Add(block.DeleteCountA);
                output.Add(block.InsertStartB); output.Add(block.InsertCountB);
            }
            return index < 0 ? output.Count : output[index];
        }
        // Negative counts expose the complete result only for untimed validation.
        public static int Run(int work, int iterations)
        {
            if (iterations < 0) return Observe(work, -iterations - 2);
            int sum = 0;
            // Consume constant-size result metadata in timed loops. Full piece text
            // and every block are verified separately, outside the timed path.
            for (int i = 0; i < iterations; i++)
            {
                var result = Diff(work);
                sum = unchecked(sum + result.PiecesOld.Count * 31 + result.PiecesNew.Count * 17 + result.DiffBlocks.Count);
            }
            return sum;
        }
    }
}
