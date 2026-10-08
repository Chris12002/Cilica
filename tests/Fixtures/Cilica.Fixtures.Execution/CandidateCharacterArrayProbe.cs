using System;
using System.Linq;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    // Owned API/composition probes only; no external decoder implementation.
    public static class CandidateCharacterArrayProbe
    {
        public static string Source(int scenario)
        {
            if (scenario == 0) return "";
            if (scenario == 1) return "\0";
            if (scenario == 2) return "A\0\ud800\udfffZ\uffff";
            if (scenario == 3) return "\ud800x\udfff";
            if (scenario == 4) return "aAaA==";
            if (scenario == 5) return " \t\u00a0\u3000";
            return "copy independent";
        }
        public static int Payload(int scenario, int index)
        {
            string source = Source(scenario);
            char[] first = source.ToCharArray();
            char[] second = source.ToCharArray();
            string later = new StringBuilder().Append('!').ToString();
            if (index == -1) return first.Length;
            if (index == -2) return (object)first == (object)second ? 1 : 0;
            if (index == -3) return (object)first == (object)Array.Empty<char>() ? 1 : 0;
            if (index == -4) return source.Length;
            return index >= 0 && index < first.Length ? first[index] : later[0];
        }
        public static int Independent(int scenario, int index)
        {
            string source = Source(scenario);
            char[] first = source.ToCharArray();
            char[] second = source.ToCharArray();
            for (int i = 0; i < first.Length; ++i) first[i] = '\uffff';
            string later = new StringBuilder().Append('!').ToString();
            if (index == -1) return second.Length;
            if (index == -2) return (object)first == (object)second ? 1 : 0;
            if (index >= 0 && index < second.Length)
                return second[index] == source[index] ? second[index] : -100;
            return later[0];
        }
        public static int AllUnits(int first, int unused)
        {
            var source = new StringBuilder();
            for (int i = 0; i < 32; ++i) source.Append(unchecked((char)(first + i)));
            string text = source.ToString();
            char[] result = text.ToCharArray();
            if (result.Length != 32) return -1;
            for (int i = 0; i < result.Length; ++i)
                if (result[i] != unchecked((char)(first + i))) return i + 1;
            return 0;
        }
        public static string Receiver(ref int trace, int mode)
        {
            trace = unchecked(trace * 10 + 1);
            if (mode == 0) return null;
            if (mode == 1) throw new InvalidOperationException("receiver");
            return "ab";
        }
        public static int ReceiverFault(int mode, int value)
        {
            int trace = value;
            try { return unchecked(trace * 31 + Receiver(ref trace, mode).ToCharArray().Length + trace * 17); }
            catch (NullReferenceException) { return unchecked(trace * 23); }
            catch (InvalidOperationException) { return unchecked(trace * 29); }
        }
        public static int ReadOnlyReference(int scenario, int index)
        {
            string input = Source(scenario);
            return Read(in input, index);
        }
        public static int Read(in string input, int index)
        {
            char[] units = input.TrimEnd(new char[] { '=' }).ToCharArray();
            return index < 0 ? units.Length : index < units.Length ? units[index] : -1;
        }
        public static bool IsLetterA(char value) => value == 'A';
        public static int CharacterPredicate(int scenario, int unused)
        {
            char[] units = new char[] { unchecked((char)scenario), 'b' };
            Func<char, bool> predicate = new Func<char, bool>(IsLetterA);
            return units.Any(predicate) ? 1 : 0;
        }
        public static int MissingSection(int start, int length) => "abcd".ToCharArray(start, length).Length;
    }
}
