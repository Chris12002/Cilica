// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of ValueStringBuilder.AppendFormatHelper. See provenance.
namespace Cilica.Runtime
{
    using static CompositeFormatting;
    internal static class BoxedText<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern string Render(object value, string format, int start, int length);
    }
    internal static class CompositeFormatting
    {
        internal static void Invalid(int position, string reason)
        {
            throw new System.FormatException(string.Concat(string.Concat("Input string was not in a correct format. Failure to parse near offset ",
                Cilica.Library.IntegerFormatting.Int32Invariant(position)), string.Concat(". ", reason)));
        }
        internal static char Next(string format, ref int position)
        {
            position++;
            if ((uint)position >= (uint)format.Length) Invalid(position, "Format item ends prematurely.");
            return format[position];
        }
        internal static bool Digit(char value) => (uint)(value - '0') < 10;
        internal static string Format<TFirst, TSecond, TThird>(string format, object first, object second, object third, int count)
        {
            if (format == null) throw new System.ArgumentNullException("format");
            var output = new System.Text.StringBuilder();
            int position = 0;
            while (position < format.Length)
            {
                char current = format[position];
                if (current != '{' && current != '}') { output.Append(current); position++; continue; }
                char brace = current;
                current = Next(format, ref position);
                if (current == brace) { output.Append(current); position++; continue; }
                if (brace != '{') Invalid(position, "Unexpected closing brace without a corresponding opening brace.");
                int index = current - '0', width = 0, formatStart = 0, formatLength = 0;
                bool left = false;
                if ((uint)index >= 10u) Invalid(position, "Expected an ASCII digit.");
                current = Next(format, ref position);
                if (current != '}')
                {
                    while (Digit(current) && index < 1000000) { index = index * 10 + current - '0'; current = Next(format, ref position); }
                    while (current == ' ') current = Next(format, ref position);
                    if (current == ',')
                    {
                        do { current = Next(format, ref position); } while (current == ' ');
                        if (current == '-') { left = true; current = Next(format, ref position); }
                        width = current - '0';
                        if ((uint)width >= 10u) Invalid(position, "Expected an ASCII digit.");
                        current = Next(format, ref position);
                        while (Digit(current) && width < 1000000) { width = width * 10 + current - '0'; current = Next(format, ref position); }
                        while (current == ' ') current = Next(format, ref position);
                    }
                    if (current != '}')
                    {
                        if (current != ':') Invalid(position, "Format item ends prematurely.");
                        formatStart = position + 1;
                        while (true)
                        {
                            current = Next(format, ref position);
                            if (current == '}') break;
                            if (current == '{') Invalid(position, "Format item ends prematurely.");
                        }
                        formatLength = position - formatStart;
                    }
                }
                position++;
                if ((uint)index >= (uint)count) throw new System.FormatException("Index (zero based) must be greater than or equal to zero and less than the size of the argument list.");
                // Pass the original UTF-16 range without allocating a substring.
                // Ordinary non-IFormattable renderers ignore this range.
                // A boxed nullable may be null. Like the pinned formatter, skip
                // payload dispatch for null before applying alignment to empty text.
                string text = index == 0 ? (first == null ? null : BoxedText<TFirst>.Render(first, format, formatStart, formatLength))
                    : index == 1 ? (second == null ? null : BoxedText<TSecond>.Render(second, format, formatStart, formatLength))
                    : (third == null ? null : BoxedText<TThird>.Render(third, format, formatStart, formatLength));
                if (text == null) text = "";
                int padding = width - text.Length;
                if (!left) for (int i = 0; i < padding; i++) output.Append(' ');
                output.Append(text);
                if (left) for (int i = 0; i < padding; i++) output.Append(' ');
            }
            return output.ToString();
        }
    }
    // Public metadata permits compiler-selected bridges across image boundaries.
    // All three entry shapes share the parser; this is not general object formatting.
    internal static class StringFormatText
    {
        internal static string Render(object value, string format, int start, int length) => (string)value;
    }
    public static class BoxedFormatting<TFirst, TSecond, TThird>
    {
        public static string Format(string format, object first, object second, object third) => CompositeFormatting.Format<TFirst,TSecond,TThird>(format, first, second, third, 3);
    }
    public static class BoxedFormatting<T>
    {
        public static string Format(string format, object argument) => CompositeFormatting.Format<T, T, T>(format, argument, null, null, 1);
    }
    public static class BoxedFormatting<TFirst, TSecond>
    {
        public static string Format(string format, object first, object second) => CompositeFormatting.Format<TFirst, TSecond, TSecond>(format, first, second, null, 2);
    }
}

namespace Cilica.Runtime
{
    // Bounded Int32 enum formatting, adapted from the pinned System.Enum source.
    // Discovery admits unique values with no custom attributes (including Flags).
    public static class EnumDefaultText<T>
    {
        private static string[] names;
        // Checked managed bridge. Publish only a complete per-enum name cache.
        public static string Default(int bits)
        {
            var cached = names;
            if (cached == null)
            {
                int count = EnumText<T>.Count();
                cached = new string[count];
                for (int i = 0; i < count; i++) cached[i] = EnumText<T>.NameAt(i);
                names = cached;
            }
            for (int i = 0; i < cached.Length; i++)
                if (EnumText<T>.ValueAt(i) == bits) return cached[i];
            return IntegerText.Int32(bits);
        }

    }

    internal static class EnumText<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern int Bits(object value);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern int Count();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern int ValueAt(int index);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern string NameAt(int index);
        internal static string Render(object box, string format, int start, int length)
        {
            int bits = Bits(box);
            int specifier = length == 0 ? 'g' : format[start] | 0x20;
            if (length > 1 || (specifier != 'g' && specifier != 'd' && specifier != 'x' && specifier != 'f'))
                throw new System.FormatException("Format string can be only \"G\", \"g\", \"X\", \"x\", \"F\", \"f\", \"D\" or \"d\".");
            if (specifier == 'x')
            {
                var chars = new char[8];
                uint value = (uint)bits;
                for (int i = 7; i >= 0; i--) { int digit = (int)(value & 15); chars[i] = (char)(digit < 10 ? '0' + digit : 'A' + digit - 10); value >>= 4; }
                return new string(chars);
            }
            if (specifier != 'd')
            {
                int count = Count();
                for (int i = 0; i < count; i++) if (ValueAt(i) == bits) return NameAt(i);
                if (specifier == 'f' && bits != 0)
                {
                    uint remaining = (uint)bits;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        uint candidate = (uint)ValueAt(i);
                        if (candidate != 0 && (remaining & candidate) == candidate) remaining &= ~candidate;
                    }
                    // Do not allocate names for a failed decomposition.
                    if (remaining == 0)
                    {
                        remaining = (uint)bits; string result = null;
                        for (int i = count - 1; i >= 0; i--)
                        {
                            uint candidate = (uint)ValueAt(i);
                            if (candidate != 0 && (remaining & candidate) == candidate)
                            {
                                remaining &= ~candidate;
                                string name = NameAt(i);
                                result = result == null ? name : string.Concat(name, string.Concat(", ", result));
                            }
                        }
                        return result;
                    }
                }
            }
            return Cilica.Library.IntegerFormatting.Int32Invariant(bits);
        }
    }
}

namespace Cilica.Runtime
{
    internal static class DictionaryKeyText<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern string Render(T value);
    }
    internal static class BoxedKeyText<T>
    {
        internal static string Render(T value) => BoxedText<T>.Render((object)value, "", 0, 0);
    }
    internal static class StringKeyText
    {
        internal static string Render(string value) => value;
    }
}

namespace Cilica.Runtime
{
    // Compiler admission requires a literal with no format suffixes. Keep a
    // defensive guard for direct implementation-library calls outside that API.
    internal static class IntegerFormatText
    {
        private static void Default(int length)
        { if (length != 0) throw new System.NotSupportedException("Only default integer composite formatting is supported."); }
        internal static string RenderInt32(object value, string format, int start, int length)
        { Default(length); return Cilica.Library.IntegerFormatting.Int32Invariant((int)value); }
        internal static string RenderUInt32(object value, string format, int start, int length)
        { Default(length); return Cilica.Library.IntegerFormatting.UInt32Invariant((uint)value); }
        internal static string RenderInt64(object value, string format, int start, int length)
        { Default(length); return Cilica.Library.IntegerFormatting.Int64Invariant((long)value); }
        internal static string RenderUInt64(object value, string format, int start, int length)
        { Default(length); return Cilica.Library.IntegerFormatting.UInt64Invariant((ulong)value); }
    }
}

namespace Cilica.Runtime
{
    internal static class DefaultReferenceKeyText<T> where T : class
    {
        internal static string Render(T value) => Name();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        private static extern string Name();
    }
}
