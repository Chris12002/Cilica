using System;
namespace Cilica.Fixtures.Execution
{
    // Independently authored reduction of DiffPlex's LineChunker requirement.
    public static class CandidateSeparatorProbe
    {
        public static string Source(int scenario)
        {
            if (scenario == 0) return "";
            if (scenario == 1) return "plain";
            if (scenario == 2) return "a\r\nb\nc\rd\r\n";
            if (scenario == 3 || scenario == 4) return ":a::b:::";
            if (scenario == 5 || scenario == 6) return "ababa";
            if (scenario == 7 || scenario == 8) return " \ta\u0085b\u00a0\u3000";
            if (scenario == 9) return " \ta b  ";
            if (scenario == 10) return "a\0b\0\0c\0";
            if (scenario == 11) return "\ud800x\udc00\ud800x";
            if (scenario == 12) return "aAbAa";
            if (scenario == 13) return "xyab";
            if (scenario == 14) return " :: \t::x ::";
            if (scenario == 15) return "::";
            if (scenario == 16) return " \t\u3000";
            return "a\r\nb";
        }

        public static string[] Separators(int scenario)
        {
            if (scenario == 3) return new[] { "::", ":" };
            if (scenario == 4) return new[] { ":", "::" };
            if (scenario == 5) return new[] { "aba", "ab" };
            if (scenario == 6) return new[] { "ab", "aba" };
            if (scenario == 7) return null;
            if (scenario == 8) return new string[0];
            if (scenario == 9) return new string[] { null, "" };
            if (scenario == 10) return new[] { "\0\0", "\0" };
            if (scenario == 11) return new[] { "\ud800x", "\udc00" };
            if (scenario == 12) return new[] { "A", "A" };
            if (scenario == 13) return new[] { "abc", "xyabx" };
            if (scenario >= 14 && scenario <= 16) return new[] { "::" };
            if (scenario == 17) return new string[] { null, "", "\r\n", "\r", "\n" };
            return new[] { "\r\n", "\r", "\n" };
        }

        public static int Payload(int packed, int query)
        {
            string source = Source(packed >> 2);
            var result = source.Split(Separators(packed >> 2), (StringSplitOptions)(packed & 3));
            // A later allocation keeps both array and substrings live across GC.
            var noise = new string[] { "live" };
            if (query == -1) return ReferenceEquals(result, Array.Empty<string>()) ? 1 : 0;
            if (query == 0) return result.Length;
            int cursor = 1;
            for (int i = 0; i < result.Length; ++i)
            {
                string item = result[i];
                if (query == cursor++) return item.Length;
                if (query == cursor++) return ReferenceEquals(item, source) ? 1 : 0;
                if (query == cursor++) return ReferenceEquals(item, string.Empty) ? 1 : 0;
                for (int j = 0; j < item.Length; ++j)
                    if (query == cursor++) return item[j];
            }
            return -noise.Length;
        }

        public static int WhitespaceUnits(int first, int unused)
        {
            int mask = 0;
            for (int i = 0; i < 32; ++i)
            {
                char unit = (char)(first + i);
                string source = new string(new[] { unit, 'x', unit });
                var result = source.Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                if (result.Length != 1) throw new InvalidOperationException("shape");
                if (result[0] == "x" && !ReferenceEquals(result[0], source)) mask |= 1 << i;
                else if (!ReferenceEquals(result[0], source)) throw new InvalidOperationException("payload/identity");
            }
            return mask;
        }

        public static int Repeated(int a, int flags)
        {
            string source = a == 0 ? "::" : a == 1 ? "" : a == 2 ? "plain" : "a::b:c";
            var separators = new[] { "::" };
            var first = source.Split(separators, (StringSplitOptions)(flags & 3));
            var again = source.Split(separators, (StringSplitOptions)(flags & 3));
            int result = ReferenceEquals(first, again) ? 1 : 2;
            for (int i = 0; i < first.Length; ++i)
                result = unchecked(result * 31 + (ReferenceEquals(first[i], again[i]) ? 3 : 5));
            separators[0] = ":";
            var changed = source.Split(separators, (StringSplitOptions)(flags & 3));
            result = unchecked(result * 31 + first.Length * 17 + changed.Length);
            for (int i = 0; i < first.Length; ++i)
                for (int j = 0; j < first[i].Length; ++j) result = unchecked(result * 31 + first[i][j]);
            return result;
        }

        public static int FaultOrder(int mode, int flags)
        {
            int order = 0;
            try
            {
                var result = Receiver(ref order, mode).Split(Argument(ref order, mode), Options(ref order, flags));
                return order * 100 + result.Length;
            }
            catch (InvalidOperationException) { return order * 100 + 11; }
            catch (NullReferenceException) { return order * 100 + 13; }
            catch (ArgumentException e)
            {
                if (e.ParamName != "options" || e.Message != "Value of flags is invalid. (Parameter 'options')")
                    return -1;
                return order * 100 + 17;
            }
        }
        public static string Receiver(ref int order, int mode)
        { order = order * 10 + 1; if (mode == 1) throw new InvalidOperationException(); return mode == 2 ? null : "a::b"; }
        public static string[] Argument(ref int order, int mode)
        { order = order * 10 + 2; if (mode == 3) throw new InvalidOperationException(); return mode == 4 ? null : new[] { "::" }; }
        public static StringSplitOptions Options(ref int order, int flags)
        { order = order * 10 + 3; return (StringSplitOptions)flags; }

        public static string CacheText(int count, int mode)
        {
            string source = "";
            for (int i = 0; i < count; ++i)
            {
                source = source + (mode == 2 || i % 5 == 0 ? " \t" : "x\ud800");
                source = source + (mode == 3 ? " \t" : i % 3 == 0 ? "::" : i % 3 == 1 ? ":" : "\r\n");
            }
            return source + (mode == 2 ? " \t" : " tail ");
        }
        public static int SplitHash(string[] result, string source)
        {
            int hash = result.Length * 17 + (ReferenceEquals(result, Array.Empty<string>()) ? 1 : 2);
            for (int i = 0; i < result.Length; ++i)
            {
                string value = result[i];
                hash = unchecked(hash * 31 + value.Length);
                hash = unchecked(hash * 31 + (ReferenceEquals(value, source) ? 3 : 5));
                hash = unchecked(hash * 31 + (ReferenceEquals(value, string.Empty) ? 7 : 11));
                for (int j = 0; j < value.Length; ++j) hash = unchecked(hash * 31 + value[j]);
            }
            return hash;
        }
        public static int CacheBoundary(int count, int packed)
        {
            int mode = packed >> 2;
            string source = CacheText(count, mode);
            string[] separators = mode == 3 ? null : mode == 1
                ? new string[] { null, "", ":", "::", "\r\n" }
                : new string[] { null, "", "::", ":", "\r\n" };
            var result = source.Split(separators, (StringSplitOptions)(packed & 3));
            var noise = new string[] { source };
            return SplitHash(result, noise[0]);
        }
        public static string CachedText;
        public static string[] CachedSeparators;
        public static int CacheQuota(int phase, int unused)
        {
            if (phase == 0)
            {
                CachedText = CacheText(65, 0);
                CachedSeparators = new[] { "::", ":", "\r\n" };
                return 1;
            }
            if (phase < 0) { CachedText = null; CachedSeparators = null; return 0; }
            return SplitHash(CachedText.Split(CachedSeparators, StringSplitOptions.None), CachedText);
        }

        public static int UnsupportedCount(int a, int b) => "a::b".Split(new[] { "::" }, a, StringSplitOptions.None).Length;
        public static int UnsupportedSingle(int a, int b) => "a::b".Split("::", StringSplitOptions.None).Length;
    }
}
