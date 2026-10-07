using System.Collections.Immutable;
using System.Reflection.Metadata;
namespace Cilica.Compiler;
internal static class DefaultIntegerFormat
{
    internal static bool Supports(PrimitiveTypeCode code) => code is PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64;
    // Only guards admitted suffixes. The owned parser remains responsible for
    // syntax errors and exact callback/error order, including malformed literals.
    internal static bool Literal(ImmutableArray<ushort> text)
    {
        if (text.IsDefault) return false;
        bool item = false;
        for (int i = 0; i < text.Length; ++i)
        {
            int c = text[i];
            if (!item && (c == '{' || c == '}') && i + 1 < text.Length && text[i + 1] == c) { ++i; continue; }
            if (c == '{') item = true;
            else if (c == '}') item = false;
            else if (c == ':' && item) return false;
        }
        return true;
    }
}
