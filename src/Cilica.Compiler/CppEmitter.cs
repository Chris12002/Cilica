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

/// <summary>Emits validated Int32 programs or the explicitly selected mixed-width integer profile; never starts a native process.</summary>
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
        => EmitProgramCore(program, integers64: false);

    public static CppArtifact EmitIntegers(IntegerIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return EmitProgramCore(program.Flow, integers64: true);
    }

    private static CppArtifact EmitProgramCore(IrProgram program, bool integers64)
    {
        ArgumentNullException.ThrowIfNull(program);
        IrMethod entryMethod = IrValidator.Validate(program, integers64: integers64);
        var throwing = ExceptionEffects.Analyze(program);
        string entry = Symbol(program.Entry), scope = entry + "_closure", faultEntry = entry + "_try";
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(integers64 ? IntegerIrProgram.Profile : IrProgram.Profile).Append("; sha256=")
            .Append(entryMethod.Source.ContentHash.ToLowerInvariant()).Append("; MethodDef=0x").Append(Hex(entryMethod.Source.Token, "x8")).Append('\n');
        text.Append("#include <bit>\n#include <cstdint>\n\nnamespace ").Append(scope).Append(" {\n");
        text.Append("struct Context { std::uint32_t pending = 0u; const char* image = nullptr; std::uint32_t method = 0u; std::uint32_t il = 0u; };\n");
        if (integers64) text.Append(Int64CppLowering.Runtime);
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
            text.Append("[[maybe_unused]] static ").Append(Signed(method.ReturnType)).Append(' ').Append(Symbol(IrMethodId.From(method.Source))).Append('(');
            text.Append("Context&");
            foreach (var parameter in method.Parameters) text.Append(", ").Append(Signed(parameter));
            text.Append(") noexcept;\n");
        }
        foreach (IrMethod method in methods) AppendMethod(text, method, throwing);
        text.Append("}\n");
        string parameters = string.Join("", entryMethod.Parameters.Select((type, i) => ", " + Signed(type) + " p" + Number(i)));
        string arguments = string.Join("", entryMethod.Parameters.Select((_, i) => ", p" + Number(i)));
        // A distinct name prevents a throwing method from being accidentally
        // called through the old value-only ABI. Context is fresh per invocation.
        text.Append("\n// Status: 0 success, 1 DivideByZero, 2 Overflow, 3 invalid output pointers.\n")
            .Append("// Outputs require valid, disjoint storage. Null outputs return 3 without writes.\n")
            .Append("// On fault result is unchanged; origin strings borrow the generated library lifetime.\n")
            .Append("extern \"C\" std::uint32_t ").Append(faultEntry)
            .Append('(').Append(Signed(entryMethod.ReturnType)).Append("* result, std::uint32_t* method, std::uint32_t* il, const char** image")
            .Append(parameters).Append(") noexcept\n{\n")
            .Append("    if (result == nullptr || method == nullptr || il == nullptr || image == nullptr) return 3u;\n")
            .Append("    ").Append(scope).Append("::Context context;\n")
            .Append("    const ").Append(Signed(entryMethod.ReturnType)).Append(" value = ").Append(scope).Append("::").Append(entry).Append("(context").Append(arguments).Append(");\n")
            .Append("    if (context.pending == 0u) *result = value;\n")
            .Append("    *method = context.method; *il = context.il; *image = context.image;\n")
            .Append("    return context.pending;\n}\n");
        bool mayThrow = throwing.Contains(program.Entry);
        if (!mayThrow)
        {
            text.Append("\nextern \"C\" ").Append(Signed(entryMethod.ReturnType)).Append(' ').Append(entry).Append('(');
            text.Append(string.Join(", ", entryMethod.Parameters.Select((type, i) => Signed(type) + " p" + Number(i))));
            text.Append(") noexcept\n{\n    ").Append(scope).Append("::Context context;\n    return ")
                .Append(scope).Append("::").Append(entry).Append("(context").Append(arguments).Append(");\n}\n");
        }
        return new(entryMethod.Source, mayThrow ? faultEntry : entry, text.ToString()) { FaultEntryPoint = faultEntry, MayThrow = mayThrow };
    }

    private static void AppendMethod(StringBuilder text, IrMethod method, HashSet<IrMethodId> throwing)
    {
        text.Append("[[maybe_unused]] static ").Append(Signed(method.ReturnType)).Append(' ').Append(Symbol(IrMethodId.From(method.Source))).Append("([[maybe_unused]] Context& context");
        for (int index = 0; index < method.Parameters.Length; index++)
        {
            text.Append(", [[maybe_unused]] ").Append(Signed(method.Parameters[index])).Append(" p").Append(Number(index));
        }
        text.Append(") noexcept\n{\n");
        for (int index = 0; index < method.Locals.Length; index++)
            text.Append("    [[maybe_unused]] ").Append(Unsigned(method.Locals[index])).Append(" l").Append(Number(index)).Append(" = 0u;\n");
        foreach (IrBlock block in method.Blocks)
            for (int index = 0; index < block.Inputs.Length; index++)
                text.Append("    [[maybe_unused]] ").Append(Unsigned(block.Inputs[index])).Append(' ').Append(Input(block.Id, index)).Append(" = 0u;\n");
        text.Append("    goto b0;\n");
        foreach (IrBlock block in method.Blocks)
        {
            var valueTypes = block.Values.Select(v => v.Type).ToArray();
            text.Append('b').Append(Number(block.Id)).Append(": { // IL_").Append(Hex(block.ILOffset, "x4")).Append('\n');
            foreach (IrStatement statement in block.Statements)
            {
                switch (statement)
                {
                    case IrStatement.Define define:
                        IrValue value = define.Value; Location(value.ILOffset);
                        string expression = AppendValuePreparation(text, value, block.Id, Fault, operandType: id => valueTypes[id]);
                        text.Append("    [[maybe_unused]] const ").Append(Unsigned(value.Type)).Append(" v").Append(Number(value.Id)).Append(" = ")
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
                case IrReturn returned: text.Append("    return std::bit_cast<").Append(Signed(method.ReturnType)).Append(">(v").Append(Number(returned.Value)).Append(");\n"); break;
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    text.Append("    if (").Append(Comparison(branch.Comparison, branch.Left, branch.Right, wide: valueTypes[branch.Left] == IrType.Int64)).Append(") {\n");
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

    // Shared integer lowering keeps object and primitive faults/bit semantics identical.
    internal static string AppendValuePreparation(StringBuilder text, IrValue value, int block, Action<Int32Fault, int> fault,
        Func<int, string>? operand = null, Func<bool, string>? scratch = null, Func<int, IrType>? operandType = null)
    {
        operand ??= id => "v" + Number(id);
        if (value.Expression is IrExpression.Binary64 wideOperation)
        {
            string result = "i8_" + Number(value.Id);
            text.Append("    const I8Result ").Append(result).Append(" = cilica_binary_i8(").Append(Number((int)wideOperation.Operation))
                .Append("u, ").Append(operand(wideOperation.Left)).Append(", ").Append(operand(wideOperation.Right)).Append(");\n");
            if (Int64Operations.MayFault(wideOperation.Operation))
            {
                text.Append("    if (").Append(result).Append(".fault == 1u) {\n"); fault(Int32Fault.DivideByZero, value.ILOffset); text.Append("    }\n");
                text.Append("    if (").Append(result).Append(".fault == 2u) {\n"); fault(Int32Fault.Overflow, value.ILOffset); text.Append("    }\n");
            }
            return result + ".value";
        }
        if (value.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned or Int32Operation.DivideUnsigned
            or Int32Operation.RemainderSigned or Int32Operation.RemainderUnsigned } division)
        {
            // Guard before native division/remainder. Signed MinValue/-1
            // faults in the selected .NET profile for both operations,
            // even though evaluating the widened operation would be safe.
            text.Append("    if (").Append(operand(division.Right)).Append(" == 0u) {\n");
            fault(Int32Fault.DivideByZero, value.ILOffset); text.Append("    }\n");
            if (division.Operation is Int32Operation.DivideSigned or Int32Operation.RemainderSigned)
            {
                text.Append("    if (").Append(operand(division.Left)).Append(" == 0x80000000u && ")
                    .Append(operand(division.Right)).Append(" == 0xffffffffu) {\n");
                fault(Int32Fault.Overflow, value.ILOffset); text.Append("    }\n");
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
            string wide = scratch is null ? "w" + Number(value.Id) : scratch(signedWide);
            text.Append("    ");
            if (scratch is null) text.Append("const std::").Append(signedWide ? "int64_t " : "uint64_t ");
            text.Append(wide).Append(" = ").Append(CheckedExpression(arithmetic, signed, signedWide, operand)).Append(";\n");
            string range = signed ? wide + " < -2147483648LL || " + wide + " > 2147483647LL"
                : signedWide ? wide + " < 0LL || " + wide + " > 4294967295LL" : wide + " > 0xffffffffu";
            text.Append("    if (").Append(range).Append(") {\n");
            fault(Int32Fault.Overflow, value.ILOffset); text.Append("    }\n");
            expression = "static_cast<std::uint32_t>(" + wide + ")";
        }
        else expression = Expression(value.Expression, block, operand, value.Type, operandType);
        return expression;
    }

    internal static string Expression(IrExpression expression, int block, Func<int, string>? operand = null,
        IrType? resultType = null, Func<int, IrType>? operandType = null)
    {
        operand ??= id => "v" + Number(id);
        return expression switch
        {
            IrExpression.Constant c => "std::uint32_t{0x" + unchecked((uint)c.Value).ToString("x8", CultureInfo.InvariantCulture) + "u}",
            IrExpression.Constant64 c => "std::uint64_t{0x" + unchecked((ulong)c.Value).ToString("x16", CultureInfo.InvariantCulture) + "ull}",
            IrExpression.Not64 n => "~" + operand(n.Operand),
            IrExpression.ConvertInteger c => c.Conversion switch
            {
                IntegerConversion.SignExtend32 => "static_cast<std::uint64_t>(static_cast<std::int64_t>(std::bit_cast<std::int32_t>(" + operand(c.Operand) + ")))",
                IntegerConversion.ZeroExtend32 => "static_cast<std::uint64_t>(" + operand(c.Operand) + ")",
                IntegerConversion.Truncate64 => "static_cast<std::uint32_t>(" + operand(c.Operand) + ")",
                _ => throw new InvalidOperationException("Validated conversion changed."),
            },
            IrExpression.Parameter p => "std::bit_cast<" + Unsigned(resultType) + ">(p" + Number(p.Index) + ")",
            IrExpression.Local l => "l" + Number(l.Index),
            IrExpression.StackInput s => Input(block, s.Index),
            IrExpression.Binary b => Binary(b, operand),
            IrExpression.Unary u when u.Operation == Int32UnaryOperation.BitwiseNot =>
                "static_cast<std::uint32_t>(~static_cast<std::uint64_t>(" + operand(u.Operand) + "))",
            IrExpression.Compare c => "(" + Comparison(c.Comparison, c.Left, c.Right, operand: operand, wide: operandType?.Invoke(c.Left) == IrType.Int64) + " ? 1u : 0u)",
            IrExpression.Call c => "std::bit_cast<" + Unsigned(resultType) + ">(" + Symbol(c.Target) + "(context" + (c.Arguments.IsEmpty ? "" : ", ") +
                string.Join(", ", c.Arguments.Select(a => "std::bit_cast<" + Signed(operandType?.Invoke(a)) + ">(" + operand(a) + ")")) + "))",
            _ => throw new InvalidOperationException("Validated expression changed."),
        };
    }

    private static string CheckedExpression(IrExpression.Binary binary, bool signed, bool signedWide, Func<int, string> operand)
    {
        string Operand(int id) => "static_cast<std::" + (signedWide ? "int64_t>(" : "uint64_t>(") +
            (signed ? "std::bit_cast<std::int32_t>(" + operand(id) + ")" : operand(id)) + ")";
        string operation = binary.Operation switch
        {
            Int32Operation.AddCheckedSigned or Int32Operation.AddCheckedUnsigned => "+",
            Int32Operation.SubtractCheckedSigned or Int32Operation.SubtractCheckedUnsigned => "-",
            Int32Operation.MultiplyCheckedSigned or Int32Operation.MultiplyCheckedUnsigned => "*",
            _ => throw new InvalidOperationException("Validated checked operation changed."),
        };
        return Operand(binary.Left) + " " + operation + " " + Operand(binary.Right);
    }

    private static string Binary(IrExpression.Binary binary, Func<int, string> operand)
    {
        string left = "static_cast<std::uint64_t>(" + operand(binary.Left) + ")";
        string right = "static_cast<std::uint64_t>(" + operand(binary.Right) + ")";
        if (binary.Operation is Int32Operation.DivideSigned or Int32Operation.RemainderSigned)
            return "static_cast<std::uint32_t>(static_cast<std::int64_t>(std::bit_cast<std::int32_t>(" + operand(binary.Left) +
                ")) " + (binary.Operation == Int32Operation.DivideSigned ? "/" : "%") +
                " static_cast<std::int64_t>(std::bit_cast<std::int32_t>(" + operand(binary.Right) + ")))";
        if (binary.Operation == Int32Operation.ShiftRightSigned)
            return "cilica_shr_i4(" + operand(binary.Left) + ", " + operand(binary.Right) + ")";
        if (binary.Operation is Int32Operation.ShiftLeft or Int32Operation.ShiftRightUnsigned) right = "(" + right + " & 31u)";
        string operation = binary.Operation switch
        {
            Int32Operation.AddWrapping => "+", Int32Operation.SubtractWrapping => "-", Int32Operation.MultiplyWrapping => "*",
            Int32Operation.BitwiseAnd => "&", Int32Operation.BitwiseOr => "|", Int32Operation.BitwiseXor => "^",
            Int32Operation.ShiftLeft => "<<", Int32Operation.ShiftRightUnsigned => ">>", Int32Operation.DivideUnsigned => "/",
            Int32Operation.RemainderUnsigned => "%",
            _ => throw new InvalidOperationException("Validated binary operation changed."),
        };
        return "static_cast<std::uint32_t>(" + left + " " + operation + " " + right + ")";
    }

    internal static string Comparison(Int32Comparison comparison, int left, int? right, bool reference = false, Func<int, string>? operand = null, bool wide = false)
    {
        operand ??= id => "v" + Number(id);
        string first = operand(left);
        if (comparison is Int32Comparison.Zero or Int32Comparison.Nonzero)
            return first + (comparison == Int32Comparison.Zero ? " == " : " != ") + (reference ? "nullptr" : "0u");
        // dup can make both operands the same already-evaluated temporary.
        // Fold that identity to avoid tautological-compare errors under -Werror.
        if (left == right)
            return comparison is Int32Comparison.Equal or Int32Comparison.LessOrEqualSigned or Int32Comparison.LessOrEqualUnsigned
                or Int32Comparison.GreaterOrEqualSigned or Int32Comparison.GreaterOrEqualUnsigned ? "true" : "false";
        string other = operand(right!.Value);
        if (comparison is Int32Comparison.LessSigned or Int32Comparison.GreaterSigned or Int32Comparison.LessOrEqualSigned or Int32Comparison.GreaterOrEqualSigned)
        {
            first = "std::bit_cast<" + (wide ? "std::int64_t" : "std::int32_t") + ">(" + first + ")";
            other = "std::bit_cast<" + (wide ? "std::int64_t" : "std::int32_t") + ">(" + other + ")";
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
        return first + " " + operation + " " + other;
    }
    internal static string Symbol(IrMethodId id) => "cilica_m_" + id.ContentHash.ToLowerInvariant() + "_" + Hex(id.Token, "x8")
        + (id.Specialization.Length == 0 ? "" : "_g_" + id.Specialization);
    private static string Input(int block, int index) => "s" + Number(block) + "_" + Number(index);
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Hex(int value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
    private static string Signed(IrType? type) => type == IrType.Int64 ? "std::int64_t" : "std::int32_t";
    private static string Unsigned(IrType? type) => type == IrType.Int64 ? "std::uint64_t" : "std::uint32_t";
}
