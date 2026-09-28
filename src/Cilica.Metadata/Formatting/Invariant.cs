using System;
using System.Globalization;
using System.Text;

namespace Cilica.Metadata.Formatting;

/// <summary>Culture-independent formatting of literal values.</summary>
internal static class Invariant
{
    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats a boxed constant or attribute value as a C#-like literal.</summary>
    public static string Literal(object? value) => value switch
    {
        null => "null",
        string text => Quote(text, '"'),
        char character => Quote(character.ToString(), '\''),
        bool boolean => boolean ? "true" : "false",
        float single => single.ToString("R", CultureInfo.InvariantCulture) + "f",
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    public static string Quote(string text, char quote)
    {
        var builder = new StringBuilder(text.Length + 2);
        builder.Append(quote);
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            switch (character)
            {
                case '\\': builder.Append(@"\\"); break;
                case '\n': builder.Append(@"\n"); break;
                case '\r': builder.Append(@"\r"); break;
                case '\t': builder.Append(@"\t"); break;
                case '\0': builder.Append(@"\0"); break;
                default:
                    if (character == quote)
                    {
                        builder.Append('\\').Append(character);
                    }
                    else if (char.IsControl(character) || IsLoneSurrogate(text, index))
                    {
                        builder.Append(@"\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        return builder.Append(quote).ToString();
    }

    private static bool IsLoneSurrogate(string text, int index)
    {
        char character = text[index];
        if (char.IsHighSurrogate(character))
        {
            return index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]);
        }
        if (char.IsLowSurrogate(character))
        {
            return index == 0 || !char.IsHighSurrogate(text[index - 1]);
        }
        return false;
    }

    public static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(bytes);
}
