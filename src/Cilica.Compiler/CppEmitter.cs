using System;
using System.Globalization;
using System.Text;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>Detached C++20 source and the explicit C entry point for one method.</summary>
public sealed record CppArtifact(MethodSource Source, string EntryPoint, string Text);

/// <summary>Emits validated Int32 control flow; never starts a native process.</summary>
public static class CppEmitter
{
    public const int MaximumValues = IrLimits.MaximumStatements;
    public const int MaximumParameters = IrLimits.MaximumParameters;

    /// <summary>Invalid caller-constructed IR is an API error, not an assembly diagnostic.</summary>
    public static CppArtifact Emit(IrMethod method)
    {
        ArgumentNullException.ThrowIfNull(method); IrValidator.Validate(method);
        string entry = "cilica_m_" + method.Source.ContentHash.ToLowerInvariant() + "_" + Hex(method.Source.Token, "x8");
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(IrMethod.Profile).Append("; sha256=")
            .Append(method.Source.ContentHash.ToLowerInvariant()).Append("; MethodDef=0x").Append(Hex(method.Source.Token, "x8")).Append('\n');
        text.Append("#include <bit>\n#include <cstdint>\n\nextern \"C\" std::int32_t ").Append(entry).Append('(');
        for (int index = 0; index < method.Parameters.Length; index++)
        {
            if (index != 0) text.Append(", ");
            text.Append("[[maybe_unused]] std::int32_t p").Append(Number(index));
        }
        text.Append(") noexcept\n{\n");
        for (int index = 0; index < method.Locals.Length; index++)
            text.Append("    [[maybe_unused]] std::uint32_t l").Append(Number(index)).Append(" = 0u;\n");
        foreach (IrBlock block in method.Blocks)
            for (int index = 0; index < block.Inputs.Length; index++)
                text.Append("    [[maybe_unused]] std::uint32_t ").Append(Input(block.Id, index)).Append(" = 0u;\n");
        text.Append("    goto b0;\n");
        foreach (IrBlock block in method.Blocks)
        {
            text.Append('b').Append(Number(block.Id)).Append(": { // IL_").Append(Hex(block.ILOffset, "x4")).Append('\n');
            foreach (IrStatement statement in block.Statements)
            {
                switch (statement)
                {
                    case IrStatement.Define define:
                        IrValue value = define.Value; Location(value.ILOffset);
                        text.Append("    [[maybe_unused]] const std::uint32_t v").Append(Number(value.Id)).Append(" = ")
                            .Append(Expression(value.Expression, block.Id)).Append(";\n"); break;
                    case IrStatement.StoreLocal store:
                        Location(store.ILOffset); text.Append("    l").Append(Number(store.Local)).Append(" = v").Append(Number(store.Value)).Append(";\n"); break;
                }
            }
            Location(block.Terminator.ILOffset);
            switch (block.Terminator)
            {
                case IrReturn returned: text.Append("    return std::bit_cast<std::int32_t>(v").Append(Number(returned.Value)).Append(");\n"); break;
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    text.Append("    if (").Append(Comparison(branch.Comparison, branch.Left, branch.Right)).Append(") {\n");
                    Edge(branch.Taken); text.Append("    } else {\n"); Edge(branch.NotTaken); text.Append("    }\n"); break;
            }
            text.Append("}\n");
        }
        text.Append("}\n");
        return new(method.Source, entry, text.ToString());

        void Location(int offset) => text.Append("    // IL_").Append(Hex(offset, "x4")).Append('\n');
        void Edge(IrEdge edge)
        {
            // Sources are immutable block-local temporaries. Sequential copies
            // therefore preserve parallel edge transfer, including self loops.
            for (int index = 0; index < edge.Arguments.Length; index++)
                text.Append("        ").Append(Input(edge.Target, index)).Append(" = v").Append(Number(edge.Arguments[index])).Append(";\n");
            text.Append("        goto b").Append(Number(edge.Target)).Append(";\n");
        }
    }

    private static string Expression(IrExpression expression, int block) => expression switch
    {
        IrExpression.Constant c => "std::uint32_t{0x" + unchecked((uint)c.Value).ToString("x8", CultureInfo.InvariantCulture) + "u}",
        IrExpression.Parameter p => "std::bit_cast<std::uint32_t>(p" + Number(p.Index) + ")",
        IrExpression.Local l => "l" + Number(l.Index),
        IrExpression.StackInput s => Input(block, s.Index),
        IrExpression.Binary b => "static_cast<std::uint32_t>(static_cast<std::uint64_t>(v" + Number(b.Left) + ") " +
            (b.Operation switch { Int32Operation.AddWrapping => "+", Int32Operation.SubtractWrapping => "-", Int32Operation.MultiplyWrapping => "*", _ => throw new InvalidOperationException() }) +
            " static_cast<std::uint64_t>(v" + Number(b.Right) + "))",
        IrExpression.Compare c => "(" + Comparison(c.Comparison, c.Left, c.Right) + " ? 1u : 0u)",
        _ => throw new InvalidOperationException("Validated expression changed."),
    };

    private static string Comparison(Int32Comparison comparison, int left, int? right)
    {
        string operand = "v" + Number(left);
        if (comparison is Int32Comparison.Zero or Int32Comparison.Nonzero)
            return operand + (comparison == Int32Comparison.Zero ? " == 0u" : " != 0u");
        string other = "v" + Number(right!.Value);
        if (comparison is Int32Comparison.LessSigned or Int32Comparison.GreaterSigned or Int32Comparison.LessOrEqualSigned or Int32Comparison.GreaterOrEqualSigned)
        {
            operand = "std::bit_cast<std::int32_t>(" + operand + ")";
            other = "std::bit_cast<std::int32_t>(" + other + ")";
        }
        string operation = comparison switch
        {
            Int32Comparison.Equal => "==", Int32Comparison.NotEqual => "!=",
            Int32Comparison.LessSigned or Int32Comparison.LessUnsigned => "<",
            Int32Comparison.GreaterSigned or Int32Comparison.GreaterUnsigned => ">",
            Int32Comparison.LessOrEqualSigned or Int32Comparison.LessOrEqualUnsigned => "<=",
            Int32Comparison.GreaterOrEqualSigned or Int32Comparison.GreaterOrEqualUnsigned => ">=",
            _ => throw new InvalidOperationException("Validated comparison changed."),
        };
        return operand + " " + operation + " " + other;
    }
    private static string Input(int block, int index) => "s" + Number(block) + "_" + Number(index);
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Hex(int value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}
