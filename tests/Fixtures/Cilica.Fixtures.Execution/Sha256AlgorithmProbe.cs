using System;
using Cilica.Library;
namespace Cilica.Fixtures.Execution
{
    public static class Sha256AlgorithmProbe
    {
        public static byte[] Input(int length)
        {
            if (length == -1) return new byte[] { 97, 98, 99 };
            if (length == -2)
            {
                string text = "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq";
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++) bytes[i] = (byte)text[i];
                return bytes;
            }
            var input = new byte[length];
            for (int i = 0; i < length; i++) input[i] = unchecked((byte)(i * 131 + length));
            return input;
        }
        public static int Byte(int length, int position)
        {
            var input = Input(length); var hash = ManagedSha256.Digest(input);
            return position < 0 ? hash.Length : position < 32 ? hash[position] : input[length - 1];
        }
        public static int Repeated(int a, int b)
        {
            var input = Input(a & 127); var first = ManagedSha256.Digest(input);
            var second = ManagedSha256.Digest(input);
            if (ReferenceEquals(first, second)) return -100;
            int result = first[b & 31]; first[b & 31] = (byte)(result ^ 255);
            return result + second[b & 31] * 257 + ManagedSha256.Digest(input)[b & 31] * 65537;
        }
        public static int Null(int a, int b)
        {
            try { ManagedSha256.Digest(null); return -100; }
            catch (ArgumentNullException e) { return e.ParamName == "input" ? 1 : -101; }
        }
    }
}
