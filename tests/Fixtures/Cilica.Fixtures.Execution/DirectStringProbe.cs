using System;
namespace Cilica.Fixtures.Execution
{
    public static class DirectStringProbe
    {
        public static int Length(int a, int b) => unchecked("A\0\ud800\udfffZ".Length + a + b);
        public static int Chars(int a, int b)
        {
            try { return unchecked("A\0\ud800\udfffZ"[b] + a); }
            catch (IndexOutOfRangeException) { return -10002; }
        }
        public static int RawLengthOracle(int a, int b)
        {
            string text = a == 0 ? null : "A\0\ud800\udfffZ";
            try { return text.Length; }
            catch (NullReferenceException) { return -10001; }
        }
        public static int RawCharsOracle(int a, int b)
        {
            string text = a == 0 ? null : "A\0\ud800\udfffZ";
            try { return text[b]; }
            catch (NullReferenceException) { return -10001; }
            catch (IndexOutOfRangeException) { return -10002; }
        }
    }
}
