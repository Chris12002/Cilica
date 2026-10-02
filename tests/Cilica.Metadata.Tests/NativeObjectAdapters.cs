using System.Globalization;
using System.Linq;
using System.Text;
using Cilica.Compiler;

namespace Cilica.Metadata.Tests;

/// <summary>Small test-only adapters for private diagnostics; production calls use arena argument packets.</summary>
internal static class NativeObjectAdapters
{
    internal static string Emit(ObjectIrProgram program, CppArtifact artifact)
    {
        var text = new StringBuilder("namespace " + artifact.EntryPoint[..^4] + "_closure {\n");
        foreach (var method in program.Flow.Methods.Where(m => m.Parameters.Length <= 4))
        {
            string symbol = "cilica_m_" + method.Source.ContentHash.ToLowerInvariant() + "_" + method.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
            text.Append("[[maybe_unused]] static ").Append(Type(method.ReturnType)).Append(' ').Append(symbol).Append("(Context& context");
            for (int i = 0; i < method.Parameters.Length; i++) text.Append(", ").Append(Type(method.Parameters[i])).Append(" p").Append(i);
            text.Append(") noexcept { std::array<Argument, ").Append(method.Parameters.Length).Append("> arguments{};\n");
            for (int i = 0; i < method.Parameters.Length; i++)
                text.Append("arguments[").Append(i).Append(method.Parameters[i].Kind == IrTypeKind.Int32 ? "].bits = std::bit_cast<std::uint32_t>(p" : "].reference = p")
                    .Append(i).Append(method.Parameters[i].Kind == IrTypeKind.Int32 ? ");\n" : ";\n");
            text.Append("return ").Append(symbol).Append("(context, arguments.data()); }\n");
        }
        return text.Append("}\n").ToString();
    }

    private static string Type(IrType type) => type.Kind switch { IrTypeKind.Int32 => "std::int32_t", IrTypeKind.Void => "void", _ => "Object*" };
}
