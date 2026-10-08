using System.Reflection.Metadata;
namespace Cilica.Compiler;

internal static class IntegerParserRules
{
    internal static bool AllowsStyle(PrimitiveTypeCode code, int style) => code switch
    {
        PrimitiveTypeCode.Int32 => style == 7,
        PrimitiveTypeCode.Int64 => style is 0 or 7,
        _ => false,
    };
}
