using System;
namespace Cilica.Fixtures.Execution
{
    public static class GeneratedCastNameProbe
    {
        public static int Text(int value, int mode)
        {
            object instance = new { Number = value };
            try { return ((ValueVectorBox)instance).Number; }
            catch (InvalidCastException error) {
                string message = error.Message; int hash = mode;
                for (int i = 0; i < message.Length; ++i) hash = unchecked(hash * 31 + message[i]);
                return hash;
            }
        }
    }
}
