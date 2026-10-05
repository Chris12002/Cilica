// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of ValueStringBuilder.AppendFormatHelper. See provenance.
namespace Cilica.Runtime
{
    using static CompositeFormatting;
    internal static class BoxedText<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern string Render(object value);
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
    }
    // Public metadata permits the compiler-selected bridge across assembly boundaries.
    // This is an implementation helper, not a general framework-formatting API.
    public static class BoxedFormatting<T>
    {
        public static string Format(string format, object argument)
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
                int index = current - '0', width = 0;
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
                        while (true)
                        {
                            current = Next(format, ref position);
                            if (current == '}') break;
                            if (current == '{') Invalid(position, "Format item ends prematurely.");
                        }
                    }
                }
                position++;
                if (index != 0) throw new System.FormatException("Index (zero based) must be greater than or equal to zero and less than the size of the argument list.");
                // Admission excludes IFormattable. Such ordinary objects ignore format text.
                string text = BoxedText<T>.Render(argument);
                if (text == null) text = "";
                int padding = width - text.Length;
                if (!left) for (int i = 0; i < padding; i++) output.Append(' ');
                output.Append(text);
                if (left) for (int i = 0; i < padding; i++) output.Append(' ');
            }
            return output.ToString();
        }
    }
}
