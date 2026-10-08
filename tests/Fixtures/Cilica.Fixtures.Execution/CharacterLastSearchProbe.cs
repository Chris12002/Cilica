using System;
namespace Cilica.Fixtures.Execution
{
    public static class CharacterLastSearchProbe
    {
        private static int Hash(string text) { int h = 17; for (int i = 0; i < text.Length; i++) h = unchecked(h * 31 + text[i]); return h; }
        private static int Fault(ArgumentOutOfRangeException error) => unchecked(Hash(error.Message) * 31 + Hash(error.ParamName));
        public static int Basic(int a, int b) => "a\0b,a\ud800\udc00z\uffff".LastIndexOf(unchecked((char)a));
        public static int Empty(int a, int b) => "".LastIndexOf(unchecked((char)a));
        public static int Start(int a, int b)
        { try { return "ababa\0\ud800".LastIndexOf(unchecked((char)a), b); } catch (ArgumentOutOfRangeException error) { return Fault(error); } }
        public static int Range(int a, int b)
        { try { return "aba\0a\ud800a".LastIndexOf('a', a, b); } catch (ArgumentOutOfRangeException error) { return Fault(error); } }
        public static int EmptyRange(int a, int b)
        { try { return "".LastIndexOf('a', a, b); } catch (ArgumentOutOfRangeException error) { return Fault(error); } }
        public static int Dynamic(int a, int b)
        {
            string text = new string(new char[] { unchecked((char)a), '\0', unchecked((char)b), unchecked((char)a) });
            return text.LastIndexOf(unchecked((char)a)) + text.LastIndexOf(unchecked((char)a),1) * 7 + text.LastIndexOf(unchecked((char)b),1,2) * 31;
        }
        private static char Needle(int a) { if (a < 0) throw new InvalidOperationException(); return unchecked((char)a); }
        public static int NullOrder(int a, int b)
        {
            string text = b < 0 ? null : "aba";
            try { return text.LastIndexOf(Needle(a),b); }
            catch (InvalidOperationException) { return 101; } catch (NullReferenceException) { return 103; } catch (ArgumentOutOfRangeException error) { return Fault(error); }
        }
        public static int UnsupportedString(int a, int b) => "abc".LastIndexOf("a");
        public static int UnsupportedComparison(int a, int b) => "abc".LastIndexOf("a", StringComparison.Ordinal);
    }
}
