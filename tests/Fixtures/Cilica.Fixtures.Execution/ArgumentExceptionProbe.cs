using System;

namespace Cilica.Fixtures.Execution
{
    public static class ArgumentExceptionProbe
    {
        private static string Name(int input) => input == 0 ? null : input == 1 ? "" : string.Concat("item", "\ud800\0name");
        private static string Text(int input) => input == 0 ? null : input == 1 ? "" : string.Concat("bad", " input");
        private static int Hash(string text)
        {
            if (text == null) return -97;
            int hash = 19;
            for (int index = 0; index < text.Length; ++index) hash = unchecked(hash * 31 + text[index]);
            return hash;
        }
        private static int Inspect(ArgumentException error, string expectedName)
        {
            string first = error.Message;
            string second = error.Message;
            var allocated = new WideCell(first.Length);
            return unchecked(Hash(first) ^ Hash(error.ParamName) ^ ((object)error.ParamName == (object)expectedName ? 71 : 37)
                ^ ((object)first == (object)second ? 113 : 197) ^ (int)allocated.Number);
        }
        public static int Argument(int input, int mode)
        {
            string name = Name(input); string message = Text(mode);
            if (mode == 0) return Inspect(new ArgumentException(), null);
            if (mode == 1) return Inspect(new ArgumentException(message), null);
            return Inspect(new ArgumentException(message, name), name);
        }
        public static int NullArgument(int input, int mode)
        {
            string name = Name(input);
            if (mode == 0) return Inspect(new ArgumentNullException(), null);
            if (mode == 1) return Inspect(new ArgumentNullException(name), name);
            return Inspect(new ArgumentNullException(name, Text(mode)), name);
        }
        public static int RangeArgument(int input, int mode)
        {
            string name = Name(input);
            if (mode == 0) return Inspect(new ArgumentOutOfRangeException(), null);
            if (mode == 1) return Inspect(new ArgumentOutOfRangeException(name), name);
            if (mode == 3) return Hash(new ArgumentOutOfRangeException(name, Text(mode)).Message);
            return Inspect(new ArgumentOutOfRangeException(name, mode == 2 ? null : Text(mode)), name);
        }
        public static int Plain(int input, int mode)
        {
            Exception error;
            if (mode == 0) error = new InvalidOperationException();
            else if (mode == 1) error = new NotSupportedException();
            else if (mode == 2) error = new InvalidOperationException(Text(input));
            else error = new NotSupportedException(Text(input));
            return Hash(error.Message);
        }
        public static int Caught(int input, int mode)
        {
            string name = Name(input);
            try
            {
                if (mode == 0) throw new ArgumentOutOfRangeException(name);
                if (mode == 1) throw new ArgumentNullException(name, "message");
                ArgumentException empty = null;
                return Hash(empty.ParamName);
            }
            catch (ArgumentNullException error) { return Inspect(error, name) ^ 17; }
            catch (ArgumentException error) { return Inspect(error, name) ^ 31; }
            catch (NullReferenceException) { return 73; }
        }
        public static int Unsupported(int input, int mode) => Hash(new ArgumentOutOfRangeException("name", new WideCell(input), "message").Message);
    }
}
