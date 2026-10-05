using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public static class StringBuilderProbe
    {
        private static int Observe(string value)
        {
            int hash = 17;
            for (int i = 0; i < value.Length; ++i) hash = unchecked(hash * 31 + value[i]);
            return unchecked(hash + value.Length);
        }
        public static int Text(int input, int mode)
        {
            var builder = new StringBuilder();
            builder.Append((string)null).Append("").Append("prefix:");
            for (int i = 0; i < 8 + (input & 7); ++i) builder.Append(mode < 0 ? "A\0\ud800B" : "tail|");
            return unchecked(Observe(builder.ToString()) + builder.Length + builder.Capacity + builder.MaxCapacity);
        }
        public static int Characters(int input, int mode)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < 65 + (mode & 7); ++i) builder.Append((char)unchecked(input + i));
            return unchecked(Observe(builder.ToString()) + builder.Length + builder.Capacity);
        }
        public static int Snapshots(int input, int mode)
        {
            var builder = new StringBuilder();
            string empty = builder.ToString();
            int bits = (object)empty == (object)"" ? 1 : 2;
            string source = input < 0 ? "\ud800\0\udfff" : "seed";
            object returned = builder.Append(source);
            string first = builder.ToString();
            string second = builder.ToString();
            bits += returned == builder ? 4 : 8;
            bits += (object)first == (object)source ? 16 : 32;
            bits += (object)first == (object)second ? 64 : 128;
            builder.Append((char)mode).Append("changed");
            return unchecked(Observe(first) + Observe(second) + Observe(builder.ToString()) + bits);
        }
        public static int NullReceiver(int input, int mode)
        {
            StringBuilder builder = mode < 0 ? null : new StringBuilder();
            int result = input;
            try { result = unchecked(result + builder.Append((string)null).Length); }
            catch (NullReferenceException) { result = unchecked(result + 11); }
            try { result = unchecked(result + Observe(builder.ToString())); }
            catch (NullReferenceException) { result = unchecked(result + 17); }
            return result;
        }
        public static int LargeChunks(int input, int mode)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < 9000 + (input & 15); ++i) builder.Append((char)unchecked(i + mode));
            string prefix = builder.ToString();
            builder.Append(prefix).Append(prefix);
            return unchecked(Observe(builder.ToString()) + Observe(prefix) + builder.Capacity + builder.Length);
        }
        public static int CodeUnitAt(int input, int index)
        {
            var builder = new StringBuilder();
            builder.Append("A\0\ud800\udfff|");
            for (int i = 0; i < 20; ++i) builder.Append((char)unchecked(input + i)).Append(':');
            builder.Append("Z");
            string result = builder.ToString();
            return (uint)index < (uint)result.Length ? result[index] : result.Length;
        }
        public static int UnsupportedLengthSetter(int input, int mode)
        {
            var builder = new StringBuilder();
            builder.Length = input;
            return builder.Length + mode;
        }
    }
}
