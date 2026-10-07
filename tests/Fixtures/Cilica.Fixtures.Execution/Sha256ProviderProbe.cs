using System;
using System.Security.Cryptography;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public sealed class CustomHashProvider : HashAlgorithm
    {
        public CustomHashProvider() { }
        public override void Initialize() { }
        protected override void HashCore(byte[] input, int offset, int count) { }
        protected override byte[] HashFinal() => new byte[] { 42 };
    }
    public sealed class CustomShaProvider : SHA256
    {
        public override void Initialize() { }
        protected override void HashCore(byte[] input, int offset, int count) { }
        protected override byte[] HashFinal() => new byte[] { 42 };
    }
    public static class Sha256ProviderProbe
    {
        public static int Byte(int length, int position)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Sha256AlgorithmProbe.Input(length));
                return position < 0 ? hash.Length : hash[position];
            }
        }
        public static int Reuse(int a, int b)
        {
            using (var first = SHA256.Create())
            using (var second = SHA256.Create())
            {
                if (ReferenceEquals(first, second)) return -100;
                var input = Sha256AlgorithmProbe.Input(a & 127);
                byte[] one = first.ComputeHash(input), two = first.ComputeHash(input), three = second.ComputeHash(input);
                if (ReferenceEquals(one, two) || ReferenceEquals(two, three)) return -101;
                int index = b & 31, result = one[index]; one[index] = (byte)(result ^ 255);
                result += two[index] * 257 + three[index] * 65537;
                first.Dispose(); first.Dispose();
                return result + second.ComputeHash(input)[index];
            }
        }
        public static int Faults(int a, int b)
        {
            var sha = SHA256.Create(); byte[] input = a < 0 ? null : new byte[0];
            if (b < 0) sha.Dispose(); int result = 0;
            try { result = sha.ComputeHash(input)[0]; }
            catch (ArgumentNullException e) { result = e.ParamName == "buffer" ? 257 : -100; }
            catch (ObjectDisposedException e) { result = e.ObjectName == "System.Security.Cryptography.SHA256+Implementation" ? 521 + e.Message.Length : -101; }
            sha.Dispose(); sha.Dispose(); return result;
        }
        public static int Recovery(int a, int b)
        {
            using (HashAlgorithm sha = SHA256.Create())
            {
                try { sha.ComputeHash((byte[])null); } catch (ArgumentNullException) { }
                return sha.ComputeHash(new byte[] { unchecked((byte)a), unchecked((byte)b) })[a & 31];
            }
        }
        public static int Interface(int a, int b)
        {
            HashAlgorithm sha = SHA256.Create();
            int result = sha is ICryptoTransform ? 1 : -100;
            ((ICryptoTransform)sha).Dispose();
            try { sha.ComputeHash((byte[])null); return -101; }
            catch (ObjectDisposedException) { return result + 2; }
        }
        public static int NullReceiver(int a, int b)
        {
            SHA256 sha = null; int result = 0;
            try { sha.ComputeHash((byte[])null); } catch (NullReferenceException) { result = 1; }
            try { sha.Dispose(); } catch (NullReferenceException) { result += 2; }
            return result;
        }
        public static int Utf8(int a, int b)
        {
            string text = (a & 3) == 0 ? "" : (a & 3) == 1 ? "abc" : (a & 3) == 2 ? "semantic\0\ud800" : "\ud83d\ude80\u00e9";
            using (var sha = SHA256.Create()) return sha.ComputeHash(Encoding.UTF8.GetBytes(text))[b & 31];
        }
        public static int UnsupportedOffset(int a, int b) { using (var sha = SHA256.Create()) return sha.ComputeHash(new byte[0], 0, 0)[0]; }
        public static int UnsupportedHashProperty(int a, int b) { using (var sha = SHA256.Create()) return sha.Hash == null ? 1 : 2; }
        public static int UnsupportedTransform(int a, int b) { using (var sha = SHA256.Create()) return sha.TransformFinalBlock(new byte[0], 0, 0).Length; }
        public static int UnsupportedCustom(int a, int b) { using (var sha = new CustomHashProvider()) return sha.ComputeHash(new byte[0])[0]; }
        public static int UnsupportedShaSubclass(int a, int b) { using (var sha = new CustomShaProvider()) return sha.ComputeHash(new byte[0])[0]; }
    }
}
