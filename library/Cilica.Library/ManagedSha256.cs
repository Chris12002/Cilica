// Cilica-owned implementation of NIST FIPS 180-4 sections 4.1.2, 4.2.2,
// 5.1.1, 5.3.3 and 6.2. No platform crypto provider or unsafe memory is used.
using System;
namespace Cilica.Library
{
    public static class ManagedSha256
    {
        public static byte[] Digest(byte[] input)
        {
            if (input == null) throw new ArgumentNullException("input");
            // Int32 vectors store raw word bits; arithmetic uses UInt32 locals.
            // Working storage is fixed and private to each call.
            var constants = Constants();
            var hash = new int[] { unchecked((int)0x6a09e667), unchecked((int)0xbb67ae85), unchecked((int)0x3c6ef372), unchecked((int)0xa54ff53a), unchecked((int)0x510e527f), unchecked((int)0x9b05688c), unchecked((int)0x1f83d9ab), unchecked((int)0x5be0cd19) };
            var schedule = new int[64];
            // Keep lengths/positions wide: padding can exceed Int32.MaxValue
            // even when the original managed vector length is a valid Int32.
            int length = input.Length;
            long padded = ((long)length + 72) / 64 * 64;
            ulong bitLength = (ulong)length * 8;
            for (long offset = 0; offset < padded; offset += 64)
            {
                for (int i = 0; i < 16; i++)
                {
                    uint word = 0;
                    for (int j = 0; j < 4; j++)
                    {
                        long position = offset + i * 4 + j;
                        uint next = position < length ? input[(int)position]
                            : position == length ? 128u
                            : position >= padded - 8 ? (uint)((bitLength >> (int)((padded - 1 - position) * 8)) & 255) : 0u;
                        word = (word << 8) | next;
                    }
                    schedule[i] = unchecked((int)word);
                }
                Compress(hash, schedule, constants);
            }
            var result = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                uint word = unchecked((uint)hash[i]);
                for (int j = 0; j < 4; j++) result[i * 4 + j] = unchecked((byte)(word >> (24 - j * 8)));
            }
            return result;
        }
        private static uint Rotate(uint value, int count) => (value >> count) | (value << (32 - count));
        private static void Compress(int[] hash, int[] schedule, int[] constants)
        {
            unchecked
            {
                for (int i = 16; i < 64; i++)
                {
                    uint x = (uint)schedule[i - 15], y = (uint)schedule[i - 2];
                    uint low = Rotate(x, 7) ^ Rotate(x, 18) ^ (x >> 3);
                    uint high = Rotate(y, 17) ^ Rotate(y, 19) ^ (y >> 10);
                    schedule[i] = (int)((uint)schedule[i - 16] + low + (uint)schedule[i - 7] + high);
                }
                uint a = (uint)hash[0], b = (uint)hash[1], c = (uint)hash[2], d = (uint)hash[3];
                uint e = (uint)hash[4], f = (uint)hash[5], g = (uint)hash[6], h = (uint)hash[7];
                for (int i = 0; i < 64; i++)
                {
                    uint high = Rotate(e, 6) ^ Rotate(e, 11) ^ Rotate(e, 25);
                    uint choice = (e & f) ^ (~e & g);
                    uint first = h + high + choice + (uint)constants[i] + (uint)schedule[i];
                    uint low = Rotate(a, 2) ^ Rotate(a, 13) ^ Rotate(a, 22);
                    uint majority = (a & b) ^ (a & c) ^ (b & c);
                    h = g; g = f; f = e; e = d + first;
                    d = c; c = b; b = a; a = first + low + majority;
                }
                hash[0] = (int)((uint)hash[0] + a); hash[1] = (int)((uint)hash[1] + b); hash[2] = (int)((uint)hash[2] + c); hash[3] = (int)((uint)hash[3] + d);
                hash[4] = (int)((uint)hash[4] + e); hash[5] = (int)((uint)hash[5] + f); hash[6] = (int)((uint)hash[6] + g); hash[7] = (int)((uint)hash[7] + h);
            }
        }
        private static int[] Constants() => new int[] {
            unchecked((int)0x428a2f98), unchecked((int)0x71374491), unchecked((int)0xb5c0fbcf), unchecked((int)0xe9b5dba5), unchecked((int)0x3956c25b), unchecked((int)0x59f111f1), unchecked((int)0x923f82a4), unchecked((int)0xab1c5ed5),
            unchecked((int)0xd807aa98), unchecked((int)0x12835b01), unchecked((int)0x243185be), unchecked((int)0x550c7dc3), unchecked((int)0x72be5d74), unchecked((int)0x80deb1fe), unchecked((int)0x9bdc06a7), unchecked((int)0xc19bf174),
            unchecked((int)0xe49b69c1), unchecked((int)0xefbe4786), unchecked((int)0x0fc19dc6), unchecked((int)0x240ca1cc), unchecked((int)0x2de92c6f), unchecked((int)0x4a7484aa), unchecked((int)0x5cb0a9dc), unchecked((int)0x76f988da),
            unchecked((int)0x983e5152), unchecked((int)0xa831c66d), unchecked((int)0xb00327c8), unchecked((int)0xbf597fc7), unchecked((int)0xc6e00bf3), unchecked((int)0xd5a79147), unchecked((int)0x06ca6351), unchecked((int)0x14292967),
            unchecked((int)0x27b70a85), unchecked((int)0x2e1b2138), unchecked((int)0x4d2c6dfc), unchecked((int)0x53380d13), unchecked((int)0x650a7354), unchecked((int)0x766a0abb), unchecked((int)0x81c2c92e), unchecked((int)0x92722c85),
            unchecked((int)0xa2bfe8a1), unchecked((int)0xa81a664b), unchecked((int)0xc24b8b70), unchecked((int)0xc76c51a3), unchecked((int)0xd192e819), unchecked((int)0xd6990624), unchecked((int)0xf40e3585), unchecked((int)0x106aa070),
            unchecked((int)0x19a4c116), unchecked((int)0x1e376c08), unchecked((int)0x2748774c), unchecked((int)0x34b0bcb5), unchecked((int)0x391c0cb3), unchecked((int)0x4ed8aa4a), unchecked((int)0x5b9cca4f), unchecked((int)0x682e6ff3),
            unchecked((int)0x748f82ee), unchecked((int)0x78a5636f), unchecked((int)0x84c87814), unchecked((int)0x8cc70208), unchecked((int)0x90befffa), unchecked((int)0xa4506ceb), unchecked((int)0xbef9a3f7), unchecked((int)0xc67178f2) };
    }
}
