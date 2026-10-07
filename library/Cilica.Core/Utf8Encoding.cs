// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded default UTF-8 adaptation; see library/provenance.json.
namespace System.Text
{
    public abstract class Encoding
    {
        protected Encoding() { }
        public static Encoding UTF8 => Cilica.Runtime.Utf8EncodingCache.Instance;
        public virtual byte[] GetBytes(string s)
        {
            if (s == null) throw new ArgumentNullException("s");
            return Encode(s);
        }
        // Public bridge metadata uses the existing bounded abstract dispatch contract.
        // General Encoding fallback machinery remains outside this owned surface.
        public abstract byte[] Encode(string s);
    }

    public class UTF8Encoding : Encoding
    {
        public UTF8Encoding() { }
        public override byte[] Encode(string s)
        {
            if (s.Length == 0) return Array.Empty<byte>();
            long count = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int scalar = ScalarAt(s, i);
                count += scalar < 0x80 ? 1 : scalar < 0x800 ? 2 : scalar < 0x10000 ? 3 : 4;
                if (scalar >= 0x10000) i++;
            }
            if (count > 2147483647) throw new ArgumentException("Conversion buffer overflow.");
            var result = new byte[(int)count];
            int output = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int scalar = ScalarAt(s, i);
                if (scalar < 0x80) result[output++] = (byte)scalar;
                else if (scalar < 0x800)
                {
                    result[output++] = (byte)(0xc0 | (scalar >> 6));
                    result[output++] = (byte)(0x80 | (scalar & 0x3f));
                }
                else if (scalar < 0x10000)
                {
                    result[output++] = (byte)(0xe0 | (scalar >> 12));
                    result[output++] = (byte)(0x80 | ((scalar >> 6) & 0x3f));
                    result[output++] = (byte)(0x80 | (scalar & 0x3f));
                }
                else
                {
                    result[output++] = (byte)(0xf0 | (scalar >> 18));
                    result[output++] = (byte)(0x80 | ((scalar >> 12) & 0x3f));
                    result[output++] = (byte)(0x80 | ((scalar >> 6) & 0x3f));
                    result[output++] = (byte)(0x80 | (scalar & 0x3f));
                    i++;
                }
            }
            return result;
        }

        internal sealed class UTF8EncodingSealed : UTF8Encoding
        {
            public UTF8EncodingSealed() { }
        }

        private static int ScalarAt(string text, int index)
        {
            int first = text[index];
            if (first < 0xd800 || first > 0xdfff) return first;
            if (first <= 0xdbff && index < text.Length - 1)
            {
                int second = text[index + 1];
                if (second >= 0xdc00 && second <= 0xdfff)
                    return 0x10000 + ((first - 0xd800) << 10) + second - 0xdc00;
            }
            return 0xfffd;
        }
    }
}
namespace Cilica.Runtime
{
    internal static class Utf8EncodingCache
    {
        public static readonly System.Text.Encoding Instance = new System.Text.UTF8Encoding.UTF8EncodingSealed();
    }
}
