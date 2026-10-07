// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Cilica adaptation of the pinned Guid.cs. Four managed Int32 words replace
// narrow fields, unsafe aliases and vector intrinsics. See ../provenance.json.
namespace System
{
    public interface IEquatable<T> { bool Equals(T other); }
    public interface IComparable { int CompareTo(object other); }
    public interface IComparable<in T> { int CompareTo(T other); }
    public interface IFormattable { string ToString(string format, IFormatProvider provider); }

    public class FormatException : SystemException
    {
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)] public extern FormatException();
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)] public extern FormatException(string message);
    }

    public readonly struct Guid : IEquatable<Guid>, IComparable, IComparable<Guid>, IFormattable
    {
        public static readonly Guid Empty;
        private readonly int a;
        private readonly int bc;
        private readonly int defg;
        private readonly int hijk;

        public Guid(string g)
        {
            var words = Cilica.Runtime.GuidParsing.Parse(g, true);
            a = words[0]; bc = words[1]; defg = words[2]; hijk = words[3];
        }

        public static Guid Parse(string input) => new Guid(Cilica.Runtime.GuidParsing.Parse(input, false));
        private Guid(int[] words)
        { a = words[0]; bc = words[1]; defg = words[2]; hijk = words[3]; }

        public Guid(int a, short b, short c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
        {
            this.a = a;
            bc = (ushort)b | ((int)(ushort)c << 16);
            defg = d | (e << 8) | (f << 16) | (g << 24);
            hijk = h | (i << 8) | (j << 16) | (k << 24);
        }
        public Guid(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
            : this(unchecked((int)a), unchecked((short)b), unchecked((short)c), d, e, f, g, h, i, j, k) { }

        public bool Equals(Guid other) => a == other.a && bc == other.bc && defg == other.defg && hijk == other.hijk;
        public override bool Equals(object other) => other is Guid value && Equals(value);
        public static bool operator ==(Guid left, Guid right) => left.Equals(right);
        public static bool operator !=(Guid left, Guid right) => !left.Equals(right);
        public override int GetHashCode() => a ^ bc ^ defg ^ hijk;

        private static int Compare(uint left, uint right) => left == right ? 0 : left < right ? -1 : 1;
        public int CompareTo(Guid other)
        {
            int result = Compare((uint)a, (uint)other.a);
            if (result != 0) return result;
            result = Compare((uint)bc & 65535, (uint)other.bc & 65535);
            if (result != 0) return result;
            result = Compare((uint)bc >> 16, (uint)other.bc >> 16);
            if (result != 0) return result;
            for (int shift = 0; shift < 32; shift += 8)
            {
                result = Compare(((uint)defg >> shift) & 255, ((uint)other.defg >> shift) & 255);
                if (result != 0) return result;
            }
            for (int shift = 0; shift < 32; shift += 8)
            {
                result = Compare(((uint)hijk >> shift) & 255, ((uint)other.hijk >> shift) & 255);
                if (result != 0) return result;
            }
            return 0;
        }
        public int CompareTo(object other)
        {
            if (other == null) return 1;
            if (other is Guid value) return CompareTo(value);
            throw new ArgumentException("Object must be of type GUID.", "value");
        }

        public override string ToString() => ToString("D", null);
        public string ToString(string format) => ToString(format, null);
        public string ToString(string format, IFormatProvider provider)
        {
            int kind = 'd';
            if (format != null && format.Length != 0)
            {
                if (format.Length != 1) throw BadFormat();
                kind = format[0] | 0x20;
            }
            if (kind != 'd' && kind != 'n' && kind != 'b' && kind != 'p' && kind != 'x') throw BadFormat();
            if (kind == 'x') return HexFormat();
            string result = kind == 'b' ? "{" : kind == 'p' ? "(" : "";
            result = string.Concat(result, Hex((uint)a, 8));
            if (kind != 'n') result = string.Concat(result, "-");
            result = string.Concat(result, Hex((uint)bc & 65535, 4));
            if (kind != 'n') result = string.Concat(result, "-");
            result = string.Concat(result, Hex((uint)bc >> 16, 4));
            if (kind != 'n') result = string.Concat(result, "-");
            for (int index = 0; index < 8; ++index)
            {
                if (index == 2 && kind != 'n') result = string.Concat(result, "-");
                result = string.Concat(result, Hex(Byte(index), 2));
            }
            if (kind == 'b') result = string.Concat(result, "}");
            if (kind == 'p') result = string.Concat(result, ")");
            return result;
        }
        private static FormatException BadFormat() => new FormatException("Format string can be only \"D\", \"d\", \"N\", \"n\", \"P\", \"p\", \"B\", \"b\", \"X\" or \"x\".");
        private uint Byte(int index) => ((uint)(index < 4 ? defg : hijk) >> ((index & 3) * 8)) & 255;
        private string HexFormat()
        {
            string result = string.Concat("{0x", Hex((uint)a, 8));
            result = string.Concat(result, ",0x");
            result = string.Concat(result, Hex((uint)bc & 65535, 4));
            result = string.Concat(result, ",0x");
            result = string.Concat(result, Hex((uint)bc >> 16, 4));
            result = string.Concat(result, ",{");
            for (int index = 0; index < 8; ++index)
            {
                if (index != 0) result = string.Concat(result, ",");
                result = string.Concat(result, "0x");
                result = string.Concat(result, Hex(Byte(index), 2));
            }
            return string.Concat(result, "}}");
        }
        private static string Hex(uint value, int digits)
        {
            string result = "";
            for (int shift = (digits - 1) * 4; shift >= 0; shift -= 4)
                result = string.Concat(result, Digit((value >> shift) & 15));
            return result;
        }
        private static string Digit(uint digit)
        {
            if (digit == 0) return "0";
            if (digit == 1) return "1";
            if (digit == 2) return "2";
            if (digit == 3) return "3";
            if (digit == 4) return "4";
            if (digit == 5) return "5";
            if (digit == 6) return "6";
            if (digit == 7) return "7";
            if (digit == 8) return "8";
            if (digit == 9) return "9";
            if (digit == 10) return "a";
            if (digit == 11) return "b";
            if (digit == 12) return "c";
            if (digit == 13) return "d";
            if (digit == 14) return "e";
            return "f";
        }
    }
}
