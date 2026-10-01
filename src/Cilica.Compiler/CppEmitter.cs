using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>Detached C++20 source for a complete program and its explicit C entry point.</summary>
public sealed record CppArtifact(MethodSource Source, string EntryPoint, string Text)
{
    public string FaultEntryPoint { get; init; } = "";
    public bool MayThrow { get; init; }
}

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
        var throwing = ExceptionEffects.Analyze(program);
        string entry = Symbol(program.Entry), scope = entry + "_closure", faultEntry = entry + "_try";
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(IrProgram.Profile).Append("; sha256=")
            .Append(entryMethod.Source.ContentHash.ToLowerInvariant()).Append("; MethodDef=0x").Append(Hex(entryMethod.Source.Token, "x8")).Append('\n');
        text.Append("#include <bit>\n#include <cstdint>\n\nnamespace ").Append(scope).Append(" {\n");
        text.Append("struct Context { std::uint32_t pending = 0u; const char* image = nullptr; std::uint32_t method = 0u; std::uint32_t il = 0u; };\n");
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
            text.Append("Context&");
            foreach (var parameter in method.Parameters) text.Append(", std::int32_t");
            text.Append(") noexcept;\n");
        }
        foreach (IrMethod method in methods) AppendMethod(text, method, throwing);
        text.Append("}\n");
        string parameters = string.Join("", entryMethod.Parameters.Select((_, i) => ", std::int32_t p" + Number(i)));
        string arguments = string.Join("", entryMethod.Parameters.Select((_, i) => ", p" + Number(i)));
        // A distinct name prevents a throwing method from being accidentally
        // called through the old value-only ABI. Context is fresh per invocation.
        text.Append("\n// Status: 0 success, 1 DivideByZero, 2 Overflow, 3 invalid output pointers.\n")
            .Append("// Outputs require valid, disjoint storage. Null outputs return 3 without writes.\n")
            .Append("// On fault result is unchanged; origin strings borrow the generated library lifetime.\n")
            .Append("extern \"C\" std::uint32_t ").Append(faultEntry)
            .Append("(std::int32_t* result, std::uint32_t* method, std::uint32_t* il, const char** image")
            .Append(parameters).Append(") noexcept\n{\n")
            .Append("    if (result == nullptr || method == nullptr || il == nullptr || image == nullptr) return 3u;\n")
            .Append("    ").Append(scope).Append("::Context context;\n")
            .Append("    const std::int32_t value = ").Append(scope).Append("::").Append(entry).Append("(context").Append(arguments).Append(");\n")
            .Append("    if (context.pending == 0u) *result = value;\n")
            .Append("    *method = context.method; *il = context.il; *image = context.image;\n")
            .Append("    return context.pending;\n}\n");
        bool mayThrow = throwing.Contains(program.Entry);
        if (!mayThrow)
        {
            text.Append("\nextern \"C\" std::int32_t ").Append(entry).Append('(');
            text.Append(string.Join(", ", entryMethod.Parameters.Select((_, i) => "std::int32_t p" + Number(i))));
            text.Append(") noexcept\n{\n    ").Append(scope).Append("::Context context;\n    return ")
                .Append(scope).Append("::").Append(entry).Append("(context").Append(arguments).Append(");\n}\n");
        }
        return new(entryMethod.Source, mayThrow ? faultEntry : entry, text.ToString()) { FaultEntryPoint = faultEntry, MayThrow = mayThrow };
    }

    private static void AppendMethod(StringBuilder text, IrMethod method, HashSet<IrMethodId> throwing)
    {
        text.Append("[[maybe_unused]] static std::int32_t ").Append(Symbol(IrMethodId.From(method.Source))).Append("([[maybe_unused]] Context& context");
        for (int index = 0; index < method.Parameters.Length; index++)
        {
            text.Append(", [[maybe_unused]] std::int32_t p").Append(Number(index));
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
                        if (value.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned or Int32Operation.DivideUnsigned } division)
                        {
                            // Guard before any native division. The signed overflow
                            // case is a managed fault even though the widened quotient fits.
                            text.Append("    if (v").Append(Number(division.Right)).Append(" == 0u) {\n");
                            Fault(Int32Fault.DivideByZero, value.ILOffset); text.Append("    }\n");
                            if (division.Operation == Int32Operation.DivideSigned)
                            {
                                text.Append("    if (v").Append(Number(division.Left)).Append(" == 0x80000000u && v")
                                    .Append(Number(division.Right)).Append(" == 0xffffffffu) {\n");
                                Fault(Int32Fault.Overflow, value.ILOffset); text.Append("    }\n");
                            }
                        }
                        string expression;
                        if (value.Expression is IrExpression.Binary arithmetic && Int32Operations.IsChecked(arithmetic.Operation))
                        {
                            // Int32 signed products fit in Int64. UInt32 products
                            // require UInt64; unsigned subtraction uses Int64 so
                            // negative results are checked without native wraparound.
                            bool signed = Int32Operations.IsSignedChecked(arithmetic.Operation);
                            bool signedWide = signed || arithmetic.Operation == Int32Operation.SubtractCheckedUnsigned;
                            string wide = "w" + Number(value.Id);
                            text.Append("    const std::").Append(signedWide ? "int64_t " : "uint64_t ").Append(wide).Append(" = ")
                                .Append(CheckedExpression(arithmetic, signed, signedWide)).Append(";\n");
                            string range = signed ? wide + " < -2147483648LL || " + wide + " > 2147483647LL"
                                : signedWide ? wide + " < 0LL || " + wide + " > 4294967295LL" : wide + " > 0xffffffffu";
                            text.Append("    if (").Append(range).Append(") {\n");
                            Fault(Int32Fault.Overflow, value.ILOffset); text.Append("    }\n");
                            expression = "static_cast<std::uint32_t>(" + wide + ")";
                        }
                        else expression = Expression(value.Expression, block.Id);
                        text.Append("    [[maybe_unused]] const std::uint32_t v").Append(Number(value.Id)).Append(" = ")
                            .Append(expression).Append(";\n");
                        if (value.Expression is IrExpression.Call call && throwing.Contains(call.Target))
                            // Do not execute a store, edge transfer or later call with
                            // the placeholder return value of a failed invocation.
                            text.Append("    if (context.pending != 0u) return 0;\n");
                        break;
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

        void Fault(Int32Fault fault, int offset)
        {
            text.Append("        context.pending = ").Append(Number((int)fault)).Append("u; context.image = \"")
                .Append(method.Source.ContentHash.ToLowerInvariant()).Append("\"; context.method = 0x")
                .Append(Hex(method.Source.Token, "x8")).Append("u; context.il = ").Append(Number(offset)).Append("u;\n        return 0;\n");
        }
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
        IrExpression.Call c => "std::bit_cast<std::uint32_t>(" + Symbol(c.Target) + "(context" + (c.Arguments.IsEmpty ? "" : ", ") +
            string.Join(", ", c.Arguments.Select(a => "std::bit_cast<std::int32_t>(v" + Number(a) + ")")) + "))",
        _ => throw new InvalidOperationException("Validated expression changed."),
    };

    private static string CheckedExpression(IrExpression.Binary binary, bool signed, bool signedWide)
    {
        string Operand(int id) => "static_cast<std::" + (signedWide ? "int64_t>(" : "uint64_t>(") +
            (signed ? "std::bit_cast<std::int32_t>(v" + Number(id) + ")" : "v" + Number(id)) + ")";
        string operation = binary.Operation switch
        {
            Int32Operation.AddCheckedSigned or Int32Operation.AddCheckedUnsigned => "+",
            Int32Operation.SubtractCheckedSigned or Int32Operation.SubtractCheckedUnsigned => "-",
            Int32Operation.MultiplyCheckedSigned or Int32Operation.MultiplyCheckedUnsigned => "*",
            _ => throw new InvalidOperationException("Validated checked operation changed."),
        };
        return Operand(binary.Left) + " " + operation + " " + Operand(binary.Right);
    }

    private static string Binary(IrExpression.Binary binary)
    {
        string left = "static_cast<std::uint64_t>(v" + Number(binary.Left) + ")";
        string right = "static_cast<std::uint64_t>(v" + Number(binary.Right) + ")";
        if (binary.Operation == Int32Operation.DivideSigned)
            return "static_cast<std::uint32_t>(static_cast<std::int64_t>(std::bit_cast<std::int32_t>(v" + Number(binary.Left) +
                ")) / static_cast<std::int64_t>(std::bit_cast<std::int32_t>(v" + Number(binary.Right) + ")))";
        if (binary.Operation == Int32Operation.ShiftRightSigned)
            return "cilica_shr_i4(v" + Number(binary.Left) + ", v" + Number(binary.Right) + ")";
        if (binary.Operation is Int32Operation.ShiftLeft or Int32Operation.ShiftRightUnsigned) right = "(" + right + " & 31u)";
        string operation = binary.Operation switch
        {
            Int32Operation.AddWrapping => "+", Int32Operation.SubtractWrapping => "-", Int32Operation.MultiplyWrapping => "*",
            Int32Operation.BitwiseAnd => "&", Int32Operation.BitwiseOr => "|", Int32Operation.BitwiseXor => "^",
            Int32Operation.ShiftLeft => "<<", Int32Operation.ShiftRightUnsigned => ">>", Int32Operation.DivideUnsigned => "/",
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
