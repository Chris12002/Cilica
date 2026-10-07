using System;
namespace Cilica.Fixtures.Execution
{
    public static class BooleanParseProbe
    {
        public static string Input(int index)
        {
            switch (index & 127)
            {
                case 0: return null;
                case 1: return "";
                case 2: return "true";
                case 3: return "false";
                case 4: return "TRUE";
                case 5: return "FALSE";
                case 6: return "TrUe";
                case 7: return "fAlSe";
                case 8: return " true ";
                case 9: return " false ";
                case 10: return "\u0000true\u0000";
                case 11: return "\u0000 false \u0000";
                case 12: return "\u0000";
                case 13: return "  ";
                case 14: return "tru";
                case 15: return "false!";
                case 16: return "truefalse";
                case 17: return "1";
                case 18: return "0";
                case 19: return "yes";
                case 20: return "no";
                case 21: return "tr ue";
                case 22: return "tr\u0000ue";
                case 23: return "fa\u0000lse";
                case 24: return "true\u0000x";
                case 25: return "x\u0000true";
                case 26: return "\ufefftrue";
                case 27: return "true\u200b";
                case 28: return "fal\u017fe";
                case 29: return "\ud800true";
                case 30: return "false\udfff";
                case 31: return "true";
                case 32: return "True";
                case 33: return "tRue";
                case 34: return "TRue";
                case 35: return "trUe";
                case 36: return "TrUe";
                case 37: return "tRUe";
                case 38: return "TRUe";
                case 39: return "truE";
                case 40: return "TruE";
                case 41: return "tRuE";
                case 42: return "TRuE";
                case 43: return "trUE";
                case 44: return "TrUE";
                case 45: return "tRUE";
                case 46: return "TRUE";
                case 47: return "false";
                case 48: return "False";
                case 49: return "fAlse";
                case 50: return "FAlse";
                case 51: return "faLse";
                case 52: return "FaLse";
                case 53: return "fALse";
                case 54: return "FALse";
                case 55: return "falSe";
                case 56: return "FalSe";
                case 57: return "fAlSe";
                case 58: return "FAlSe";
                case 59: return "faLSe";
                case 60: return "FaLSe";
                case 61: return "fALSe";
                case 62: return "FALSe";
                case 63: return "falsE";
                case 64: return "FalsE";
                case 65: return "fAlsE";
                case 66: return "FAlsE";
                case 67: return "faLsE";
                case 68: return "FaLsE";
                case 69: return "fALsE";
                case 70: return "FALsE";
                case 71: return "falSE";
                case 72: return "FalSE";
                case 73: return "fAlSE";
                case 74: return "FAlSE";
                case 75: return "faLSE";
                case 76: return "FaLSE";
                case 77: return "fALSE";
                case 78: return "FALSE";
                case 79: return "\t\u0000TrUe\u0000\t";
                case 80: return "\n\u0000TrUe\u0000\n";
                case 81: return "\u000b\u0000TrUe\u0000\u000b";
                case 82: return "\f\u0000TrUe\u0000\f";
                case 83: return "\r\u0000TrUe\u0000\r";
                case 84: return " \u0000TrUe\u0000 ";
                case 85: return "\u0085\u0000TrUe\u0000\u0085";
                case 86: return "\u00a0\u0000TrUe\u0000\u00a0";
                case 87: return "\u1680\u0000TrUe\u0000\u1680";
                case 88: return "\u2000\u0000TrUe\u0000\u2000";
                case 89: return "\u2001\u0000TrUe\u0000\u2001";
                case 90: return "\u2002\u0000TrUe\u0000\u2002";
                case 91: return "\u2003\u0000TrUe\u0000\u2003";
                case 92: return "\u2004\u0000TrUe\u0000\u2004";
                case 93: return "\u2005\u0000TrUe\u0000\u2005";
                case 94: return "\u2006\u0000TrUe\u0000\u2006";
                case 95: return "\u2007\u0000TrUe\u0000\u2007";
                case 96: return "\u2008\u0000TrUe\u0000\u2008";
                case 97: return "\u2009\u0000TrUe\u0000\u2009";
                case 98: return "\u200a\u0000TrUe\u0000\u200a";
                case 99: return "\u2028\u0000TrUe\u0000\u2028";
                case 100: return "\u2029\u0000TrUe\u0000\u2029";
                case 101: return "\u202f\u0000TrUe\u0000\u202f";
                case 102: return "\u205f\u0000TrUe\u0000\u205f";
                case 103: return "\u3000\u0000TrUe\u0000\u3000";
                case 104: return "\u180efalse\u180e";
                case 105: return "\u200bfalse\u200b";
                case 106: return "\ufefffalse\ufeff";
                case 107: return "\ud800false\ud800";
                case 108: return "\udffffalse\udfff";
                case 109: return "\uff54false\uff54";
                case 110: return "\u017ffalse\u017f";
                default: return "\0 \0False\0 \0";
            }
        }
        public static int Parse(int a, int b)
        {
            bool value = b != 0;
            bool success = bool.TryParse(Input(a), out value);
            return (success ? 17 : 31) + (value ? 7 : 3);
        }
        public static int Parameter(int a, int b) => IntoParameter(a, b != 0);
        private static int IntoParameter(int index, bool value)
        {
            bool success = bool.TryParse(Input(index), out value);
            return (success ? 17 : 31) + (value ? 7 : 3);
        }
        public static int Twice(int a, int b)
        {
            bool value;
            bool.TryParse("true", out value);
            bool first = ReadAndParse(ref value, Input(a));
            int previous = (first ? 13 : 29) + (value ? 11 : 5);
            bool last = ReadAndParse(ref value, Input(b));
            return previous + (last ? 101 : 201) + (value ? 23 : 3);
        }
        private static bool ReadAndParse(ref bool value, string text) => bool.TryParse(text, out value);
        private static string Text(int index) { if (index < 0) throw new InvalidOperationException("argument"); return Input(index); }
        public static int ArgumentFault(int a, int b)
        {
            bool value = b != 0;
            try { bool ok = bool.TryParse(Text(a), out value); return (ok ? 10 : 20) + (value ? 1 : 2); }
            catch (InvalidOperationException) { return value ? 31 : 41; }
        }
        public static int UnsupportedParse(int a, int b) => bool.Parse(Input(a)) ? 1 : 0;
        private sealed class Box { internal bool Value; }
        public static int UnsupportedHeap(int a, int b) { var box = new Box(); return bool.TryParse(Input(a), out box.Value) ? 1 : 0; }
    }
}
