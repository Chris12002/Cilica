using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>Detached C++20 source and the explicit C entry point for one method.</summary>
public sealed record CppArtifact(MethodSource Source, string EntryPoint, string Text);

/// <summary>Emits the validated straight-line Int32 profile; never starts a native process.</summary>
public static class CppEmitter
{
    public const int MaximumValues = 65_536;
    public const int MaximumParameters = 4_096;

    /// <summary>Invalid caller-constructed IR is an API error, not an assembly diagnostic.</summary>
    public static CppArtifact Emit(IrMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        Validate(method);
        string entry = "cilica_m_" + method.Source.ContentHash.ToLowerInvariant() + "_" + Hex(method.Source.Token, "x8");
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(IrMethod.Profile).Append("; sha256=")
            .Append(method.Source.ContentHash.ToLowerInvariant()).Append("; MethodDef=0x")
            .Append(Hex(method.Source.Token, "x8")).Append('\n');
        text.Append("#include <bit>\n#include <cstdint>\n\nextern \"C\" std::int32_t ").Append(entry).Append('(');
        for (int index = 0; index < method.Parameters.Length; index++)
        {
            if (index != 0) text.Append(", ");
            text.Append("[[maybe_unused]] std::int32_t p").Append(Number(index));
        }
        text.Append(") noexcept\n{\n");
        IrBlock block = method.Blocks[0];
        foreach (IrValue value in block.Values)
        {
            text.Append("    // IL_").Append(Hex(value.ILOffset, "x4")).Append('\n');
            text.Append("    [[maybe_unused]] const std::uint32_t v").Append(Number(value.Id)).Append(" = ");
            switch (value.Expression)
            {
                case IrExpression.Constant constant:
                    text.Append("std::uint32_t{0x").Append(unchecked((uint)constant.Value).ToString("x8", CultureInfo.InvariantCulture)).Append("u}");
                    break;
                case IrExpression.Parameter parameter:
                    text.Append("std::bit_cast<std::uint32_t>(p").Append(Number(parameter.Index)).Append(')');
                    break;
                case IrExpression.Binary binary:
                    string operation = binary.Operation switch
                    {
                        Int32Operation.AddWrapping => "+",
                        Int32Operation.SubtractWrapping => "-",
                        Int32Operation.MultiplyWrapping => "*",
                        _ => throw new InvalidOperationException("Validated operation changed."),
                    };
                    // Widen before arithmetic: even unusual native integer promotions
                    // cannot turn uint32 multiplication into signed overflow.
                    text.Append("static_cast<std::uint32_t>(static_cast<std::uint64_t>(v")
                        .Append(Number(binary.Left)).Append(") ").Append(operation)
                        .Append(" static_cast<std::uint64_t>(v").Append(Number(binary.Right)).Append("))");
                    break;
            }
            text.Append(";\n");
        }
        text.Append("    // IL_").Append(Hex(block.Return.ILOffset, "x4")).Append('\n');
        text.Append("    return std::bit_cast<std::int32_t>(v").Append(Number(block.Return.Value)).Append(");\n}\n");
        return new(method.Source, entry, text.ToString());
    }

    private static void Validate(IrMethod method)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException("Invalid Int32 IR: " + message, nameof(method));
        }
        Require(method.Source is not null, "source is missing.");
        Require(method.Source!.ContentHash is { Length: 64 } hash && hash.All(char.IsAsciiHexDigit), "source hash must be SHA-256 hexadecimal.");
        Require((uint)method.Source.Token >> 24 == 0x06 && (method.Source.Token & 0xffffff) != 0, "source needs a non-nil MethodDef token.");
        Require(!method.Parameters.IsDefault && method.Parameters.Length <= MaximumParameters && method.Parameters.All(p => p == IrType.Int32)
            && method.ReturnType == IrType.Int32, "signature is outside the bounded Int32 profile.");
        Require(!method.Blocks.IsDefault && method.Blocks.Length == 1 && method.Blocks[0] is { Id: 0 }, "exactly block zero is required.");
        IrBlock block = method.Blocks[0];
        Require(!block.Values.IsDefault && block.Values.Length is > 0 and <= MaximumValues, "value count is outside the emission bound.");
        int previousOffset = -1;
        for (int index = 0; index < block.Values.Length; index++)
        {
            IrValue? value = block.Values[index];
            Require(value is not null && value.Id == index && value.Type == IrType.Int32 && value.ILOffset > previousOffset,
                "values need consecutive IDs, Int32 types and increasing nonnegative IL offsets.");
            previousOffset = value!.ILOffset;
            Require(value.Expression switch
            {
                IrExpression.Constant => true,
                IrExpression.Parameter parameter => parameter.Index >= 0 && parameter.Index < method.Parameters.Length,
                IrExpression.Binary binary => binary.Operation is Int32Operation.AddWrapping or Int32Operation.SubtractWrapping or Int32Operation.MultiplyWrapping
                    && binary.Left >= 0 && binary.Left < index && binary.Right >= 0 && binary.Right < index,
                _ => false,
            }, "expression, parameter or operand is invalid.");
        }
        Require(block.Return is not null && block.Return.ILOffset > previousOffset
            && block.Return.Value >= 0 && block.Return.Value < block.Values.Length, "return needs an earlier value and a later IL offset.");
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Hex(int value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}
