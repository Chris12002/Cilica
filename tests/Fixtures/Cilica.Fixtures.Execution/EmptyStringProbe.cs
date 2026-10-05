using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public static class EmptyStringProbe
    {
        private static string Read() => string.Empty;
        public static int Identity(int input, int mode)
        {
            object empty = Read();
            string joined = string.Concat(null, null);
            string built = new StringBuilder().ToString();
            int result = empty == (object)"" ? 1 : 0;
            if (empty == (object)Read()) result += 2;
            if (empty == (object)joined) result += 4;
            if (empty == (object)built) result += 8;
            if (string.Empty.Length == 0) result += 16;
            if (string.IsNullOrWhiteSpace(string.Empty)) result += 32;
            try { return result + string.Empty[input]; }
            catch (IndexOutOfRangeException) { return result + 64; }
        }
    }
}
