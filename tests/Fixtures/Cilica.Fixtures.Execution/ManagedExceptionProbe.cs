using System;

namespace Cilica.Fixtures.Execution
{
    public class ProbeException : Exception
    {
        public ProbeException() { }
        public ProbeException(string message) : base(message) { }
    }
    public static class ManagedExceptionProbe
    {
        public static int TypedCatch(int number)
        {
            try { return 100 / number; }
            catch (DivideByZeroException) { return 17; }
        }
        public static int Cleanup(int number)
        {
            int result = 0;
            try
            {
                try { result = 100 / number; }
                finally { result = unchecked(result + 5); }
            }
            catch (DivideByZeroException) { result = unchecked(result + 20); }
            return result;
        }
        public static int TypeFilter(int number)
        {
            try { if (number == 0) throw new ProbeException(); return 100 / number; }
            catch (Exception error) when (error is ProbeException || error is OverflowException) { return 31; }
        }
        public static int Rethrow(int number)
        {
            try
            {
                try { return 100 / number; }
                catch (DivideByZeroException) { throw; }
            }
            catch (Exception) { return 37; }
        }
        public static int GeneralFilter(int number)
        {
            try { throw new ProbeException(); }
            catch (Exception) when (number == 0) { return 41; }
        }
    }
}
