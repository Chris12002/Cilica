using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public static class NumericAppendProbe
    {
        private static int Digest(StringBuilder builder)
        {
            string text = builder.ToString(); int result = builder.Length * 31 + builder.Capacity;
            for (int i = 0; i < text.Length; i++) result = unchecked(result * 31 + text[i]);
            return result;
        }
        public static int Signed32(int input, int mode)
        {
            var b = new StringBuilder(); var same = b.Append(input); b.Append(':'); b.Append(mode);
            return unchecked(Digest(b) + (same == b ? 1 : -100));
        }
        public static int Unsigned32(int input, int mode)
        {
            var b = new StringBuilder(); b.Append((uint)input); b.Append('/'); b.Append((uint)mode); return Digest(b);
        }
        public static int Signed64(int input, int mode)
        {
            var b = new StringBuilder(); long value = ((long)input << 32) | (uint)mode;
            b.Append(value); b.Append(':'); b.Append(long.MinValue); b.Append(long.MaxValue); return Digest(b);
        }
        public static int Unsigned64(int input, int mode)
        {
            var b = new StringBuilder(); ulong value = ((ulong)(uint)input << 32) | (uint)mode;
            b.Append(value); b.Append(':'); b.Append(ulong.MaxValue); return Digest(b);
        }
        public static int Chunks(int input, int mode)
        {
            var b = new StringBuilder();
            for (int i = 0; i < 20; ++i) { b.Append(unchecked(input + i)); b.Append((long)mode * i); }
            string snapshot = b.ToString(); int result = Digest(b); b.Append(input);
            if (snapshot.Length >= b.Length) return -100;
            return unchecked(result + Digest(b) + snapshot.Length);
        }
        public static int NullOrder(int input, int mode)
        {
            StringBuilder b = null;
            try { b.Append(input / mode); return -100; }
            catch (DivideByZeroException) { return 3; } catch (OverflowException) { return 5; } catch (NullReferenceException) { return 7; }
        }
    }
}
