using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>Detached C++20 source for a complete program and its explicit C entry point.</summary>
public sealed record CppArtifact(MethodSource Source, string EntryPoint, string Text);

/// <summary>Emits validated Int32 programs; never starts a native process.</summary>
public static class CppEmitter
{
    public const int MaximumValues = IrLimits.MaximumStatements;
    public const int MaximumParameters = IrLimits.MaximumParameters;

    /// <summary>Invalid caller-constructed IR is an API error, not an assembly diagnostic.</summary>
    public static CppArtifact Emit(IrMethod method)
    {
        ArgumentNullException.ThrowIfNull(method); IrValidator.Validate(method);
        return EmitProgram(new(IrMethodId.From(method.Source), [method]));
    }

    public static CppArtifact EmitProgram(IrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        IrMethod entryMethod = IrValidator.Validate(program);
        string entry = Symbol(program.Entry), scope = entry + "_closure";
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(IrProgram.Profile).Append("; sha256=")
            .Append(entryMethod.Source.ContentHash.ToLowerInvariant()).Append("; MethodDef=0x").Append(Hex(entryMethod.Source.Token, "x8")).Append('\n');
        text.Append("#include <bit>\n#include <cstdint>\n\nnamespace ").Append(scope).Append(" {\n");
        IrMethod[] methods = [.. program.Methods.OrderBy(m => m.Source.ContentHash.ToLowerInvariant(), StringComparer.Ordinal).ThenBy(m => m.Source.Token)];
        if (methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements).OfType<IrStatement.Define>()
            .Any(d => d.Value.Expression is IrExpression.Binary { Operation: Int32Operation.ShiftRightSigned }))
        {
            // Shift only unsigned 64-bit values. The zero-count guard keeps the
            // fill empty; every evaluated native count is strictly below 64.
            text.Append("static std::uint32_t cilica_shr_i4(std::uint32_t value, std::uint32_t count) noexcept\n{\n")
                .Append("    count &= 31u;\n    const std::uint64_t bits = value;\n")
                .Append("    const std::uint64_t sign = std::uint64_t{0} - (bits >> 31u);\n")
                .Append("    const std::uint64_t fill = count == 0u ? std::uint64_t{0} : sign << (32u - count);\n")
                .Append("    return static_cast<std::uint32_t>((bits >> count) | fill);\n}\n");
        }
        foreach (IrMethod method in methods)
        {
            text.Append("[[maybe_unused]] static std::int32_t ").Append(Symbol(IrMethodId.From(method.Source))).Append('(');
            text.Append(string.Join(", ", method.Parameters.Select(_ => "std::int32_t"))).Append(") noexcept;\n");
        }
        foreach (IrMethod method in methods) AppendMethod(text, method);
        text.Append("}\n\nextern \"C\" std::int32_t ").Append(entry).Append('(');
        text.Append(string.Join(", ", entryMethod.Parameters.Select((_, i) => "std::int32_t p" + Number(i))));
        text.Append(") noexcept\n{\n    return ").Append(scope).Append("::").Append(entry).Append('(');
        text.Append(string.Join(", ", entryMethod.Parameters.Select((_, i) => "p" + Number(i)))).Append(");\n}\n");
        return new(entryMethod.Source, entry, text.ToString());
    }

    private static void AppendMethod(StringBuilder text, IrMethod method)
    {
        text.Append("[[maybe_unused]] static std::int32_t ").Append(Symbol(IrMethodId.From(method.Source))).Append('(');
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
        IrExpression.Binary b => Binary(b),
        IrExpression.Unary u when u.Operation == Int32UnaryOperation.BitwiseNot =>
            "static_cast<std::uint32_t>(~static_cast<std::uint64_t>(v" + Number(u.Operand) + "))",
        IrExpression.Compare c => "(" + Comparison(c.Comparison, c.Left, c.Right) + " ? 1u : 0u)",
        IrExpression.Call c => "std::bit_cast<std::uint32_t>(" + Symbol(c.Target) + "(" +
            string.Join(", ", c.Arguments.Select(a => "std::bit_cast<std::int32_t>(v" + Number(a) + ")")) + "))",
        _ => throw new InvalidOperationException("Validated expression changed."),
    };

    private static string Binary(IrExpression.Binary binary)
    {
        string left = "static_cast<std::uint64_t>(v" + Number(binary.Left) + ")";
        string right = "static_cast<std::uint64_t>(v" + Number(binary.Right) + ")";
        if (binary.Operation == Int32Operation.ShiftRightSigned)
            return "cilica_shr_i4(v" + Number(binary.Left) + ", v" + Number(binary.Right) + ")";
        if (binary.Operation is Int32Operation.ShiftLeft or Int32Operation.ShiftRightUnsigned) right = "(" + right + " & 31u)";
        string operation = binary.Operation switch
        {
            Int32Operation.AddWrapping => "+", Int32Operation.SubtractWrapping => "-", Int32Operation.MultiplyWrapping => "*",
            Int32Operation.BitwiseAnd => "&", Int32Operation.BitwiseOr => "|", Int32Operation.BitwiseXor => "^",
            Int32Operation.ShiftLeft => "<<", Int32Operation.ShiftRightUnsigned => ">>",
            _ => throw new InvalidOperationException("Validated binary operation changed."),
        };
        return "static_cast<std::uint32_t>(" + left + " " + operation + " " + right + ")";
    }

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
    private static string Symbol(IrMethodId id) => "cilica_m_" + id.ContentHash.ToLowerInvariant() + "_" + Hex(id.Token, "x8");
    private static string Input(int block, int index) => "s" + Number(block) + "_" + Number(index);
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Hex(int value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}
