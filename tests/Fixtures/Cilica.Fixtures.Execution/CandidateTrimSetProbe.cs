using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    // Independently authored string API reductions; no candidate algorithm.
    public static class CandidateTrimSetProbe
    {
        public static string Source(int selection)
        {
            if (selection == 0) return "";
            if (selection == 1) return "====";
            if (selection == 2) return "==a=b==";
            if (selection == 3) return "==word";
            if (selection == 4) return "word==";
            if (selection == 5) return " unchanged ";
            if (selection == 6) return " \tA\0\ud800\udfff B\u3000\r\n";
            if (selection == 7) return "\0\ud800x\udfff\0";
            if (selection == 8) return "\u200b\ufeff";
            if (selection == 9) return " \t\u0085\u00a0\u1680\u2000\u200a\u2028\u2029\u202f\u205f\u3000";
            if (selection == 10) return "\uffffA\uffff";
            return "abacab";
        }

        public static int Observe(string source, string result, int index)
        {
            // A fresh allocation with both the original and result still live.
            string later = new StringBuilder().Append('!').ToString();
            if (index == -1) return result.Length;
            if (index == -2) return (object)result == (object)source ? 1 : 0;
            if (index == -3) return (object)result == (object)string.Empty ? 1 : 0;
            return index >= 0 && index < result.Length ? result[index] : later[0];
        }

        public static int Single(int scenario, int index)
        {
            int selection = scenario / 3;
            string source = Source(selection);
            char unit = '=';
            if (selection == 7) unit = '\0';
            if (selection == 8) unit = '\u200b';
            if (selection == 9) unit = ' ';
            if (selection == 10) unit = '\uffff';
            if (selection == 11) unit = 'a';
            string result;
            if (scenario % 3 == 0) result = source.Trim(unit);
            else if (scenario % 3 == 1) result = source.TrimStart(unit);
            else result = source.TrimEnd(unit);
            return Observe(source, result, index);
        }

        public static int Set(int scenario, int index)
        {
            int selection = scenario / 3;
            string source = Source(selection);
            char[] units = new char[] { '=', '=', 'a', 'b' };
            if (selection == 5) units = new char[0];
            if (selection == 6 || selection == 9) units = null;
            if (selection == 7) units = new char[] { '\ud800', '\0', '\udfff' };
            if (selection == 8) units = new char[] { '\u200b', '\ufeff' };
            if (selection == 10) units = new char[] { '\uffff' };
            string result;
            if (scenario % 3 == 0) result = source.Trim(units);
            else if (scenario % 3 == 1) result = source.TrimStart(units);
            else result = source.TrimEnd(units);
            return Observe(source, result, index);
        }

        public static int Whitespace(int scenario, int index)
        {
            string source = Source(scenario / 3);
            string result;
            if (scenario % 3 == 0) result = source.Trim();
            else if (scenario % 3 == 1) result = source.TrimStart();
            else result = source.TrimEnd();
            return Observe(source, result, index);
        }

        public static string Receiver(ref int trace, int mode)
        {
            trace = trace * 10 + 1;
            if (mode == 0) return null;
            if (mode == 1) throw new InvalidOperationException("receiver");
            return "==x==";
        }
        public static char[] Argument(ref int trace, int mode)
        {
            trace = trace * 10 + 2;
            if (mode == 0) throw new ArgumentException("argument");
            if (mode == 1) return null;
            return new char[] { '=' };
        }
        public static int Evaluation(int scenario, int unused)
        {
            int trace = 0;
            try
            {
                string result = Receiver(ref trace, scenario / 3).TrimEnd(Argument(ref trace, scenario % 3));
                return trace * 100 + result.Length;
            }
            catch (NullReferenceException) { return trace * 100 + 71; }
            catch (InvalidOperationException) { return trace * 100 + 72; }
            catch (ArgumentException) { return trace * 100 + 73; }
        }

        public static int Mutation(int scenario, int index)
        {
            string source = Source(scenario);
            char[] units = new char[] { '=' };
            string first = source.Trim(units);
            units[0] = 'a';
            string second = source.Trim(units);
            int result = Observe(source, first, index);
            return unchecked(result * 65599 + Observe(source, second, index));
        }
        public static int MissingCharacters(int unused, int other) => "x".ToCharArray().Length;
    }
}
