using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Closed proofs for side-effect-free native kernels. Failure retries the original framed method.</summary>
internal static class ObjectCppTypedMethods
{
    internal const string Runtime = """
#ifndef CILICA_TYPED_INTERNALS
#if defined(__OPTIMIZE__)
#define CILICA_TYPED_INTERNALS 1
#else
#define CILICA_TYPED_INTERNALS 0
#endif
#endif
#if CILICA_TYPED_INTERNALS != 0 && CILICA_TYPED_INTERNALS != 1
#error CILICA_TYPED_INTERNALS must be 0 or 1
#endif
struct TypedCallDepth {
    std::size_t& depth;
    explicit TypedCallDepth(Context& context) noexcept : depth(context.depth) { ++depth; }
    ~TypedCallDepth() noexcept { --depth; }
    TypedCallDepth(const TypedCallDepth&) = delete;
    TypedCallDepth& operator=(const TypedCallDepth&) = delete;
};
""";

    internal static Dictionary<IrMethodId, IrMethod> Analyze(IEnumerable<IrMethod> methods)
        => methods.Where(Eligible).ToDictionary(m => IrMethodId.From(m.Source));

    private static bool Scalar(IrType type) => type.Kind is IrTypeKind.Int32 or IrTypeKind.Int64;
    private static bool Eligible(IrMethod method)
    {
        if (!Scalar(method.ReturnType) || method.Parameters.Length > 4 || method.Locals.Length > 8
            || !method.Parameters.All(Scalar) || !method.Locals.All(Scalar) || !method.ExceptionRegions.IsEmpty
            || method.Blocks.Length > 16 || method.Blocks.Sum(b => b.Statements.Length + b.Inputs.Length) > 96)
            return false;
        return method.Blocks.All(b => b.Inputs.All(Scalar) && b.Terminator is IrReturn or IrJump or IrBranch
            && b.Statements.All(s => s is IrStatement.StoreLocal || s is IrStatement.Define d && Scalar(d.Value.Type)
                && d.Value.Expression is IrExpression.Constant or IrExpression.Constant64 or IrExpression.Parameter
                    or IrExpression.Local or IrExpression.StackInput or IrExpression.Binary or IrExpression.Binary64
                    or IrExpression.Unary or IrExpression.Not64 or IrExpression.ConvertInteger or IrExpression.IntegerCast
                    or IrExpression.Compare));
    }

    internal static string Type(IrType type) => type.Kind == IrTypeKind.Int64 ? "std::uint64_t" : "std::uint32_t";
    internal static string Symbol(IrMethod method) => CppEmitter.Symbol(IrMethodId.From(method.Source));
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Signed(IrType type) => type.Kind == IrTypeKind.Int64 ? "std::int64_t" : "std::int32_t";
    private static string Member(IrType type) => type.Kind == IrTypeKind.Int64 ? "wide_bits" : "bits";
    private static void Header(StringBuilder text, IrMethod method, bool kernel, bool module)
    {
        text.Append(module ? "[[maybe_unused]] " : "[[maybe_unused]] static ")
            .Append(kernel ? "bool" : Signed(method.ReturnType)).Append(' ').Append(Symbol(method)).Append(kernel ? "_kernel" : "_typed")
            .Append("([[maybe_unused]] Context& context");
        for (int i = 0; i < method.Parameters.Length; i++)
            text.Append(", [[maybe_unused]] ").Append(Type(method.Parameters[i])).Append(" p").Append(N(i));
        if (kernel) text.Append(", ").Append(Type(method.ReturnType)).Append("& result");
        text.Append(") noexcept");
    }
    internal static void Declare(StringBuilder text, IrMethod method, bool module)
    {
        Header(text, method, true, module); text.Append(";\n");
        Header(text, method, false, module); text.Append(";\n");
    }

    internal static void Append(StringBuilder text, IrMethod method, string argumentType, bool module)
    {
        Header(text, method, true, module);
        text.Append(" {\n    // Proven typed kernel: no allocation, callback, write or escaping address.\n")
            .Append("    if (context.depth >= 64u) return false;\n    TypedCallDepth depth(context);\n");
        for (int i = 0; i < method.Locals.Length; i++)
            text.Append("    [[maybe_unused]] ").Append(Type(method.Locals[i])).Append(" l").Append(N(i)).Append(" = 0;\n");
        foreach (var block in method.Blocks)
            for (int i = 0; i < block.Inputs.Length; i++)
                text.Append("    [[maybe_unused]] ").Append(Type(block.Inputs[i])).Append(" s").Append(N(block.Id)).Append('_').Append(N(i)).Append(" = 0;\n");
        text.Append("    goto b0;\n");
        foreach (var block in method.Blocks)
        {
            var types = block.Values.ToDictionary(v => v.Id, v => v.Type);
            text.Append("    b").Append(N(block.Id)).Append(": {\n");
            foreach (var statement in block.Statements)
            {
                if (statement is IrStatement.StoreLocal store)
                {
                    text.Append("    l").Append(N(store.Local)).Append(" = v").Append(N(store.Value)).Append(";\n"); continue;
                }
                var value = ((IrStatement.Define)statement).Value;
                text.Append("    // IL_").Append(value.ILOffset.ToString("x4", CultureInfo.InvariantCulture)).Append('\n');
                string expression = CppEmitter.AppendValuePreparation(text, value, block.Id,
                    (_, _) => text.Append("        return false;\n"), operandType: id => types[id]);
                text.Append("    [[maybe_unused]] const ").Append(Type(value.Type)).Append(" v").Append(N(value.Id)).Append(" = ").Append(expression).Append(";\n");
            }
            switch (block.Terminator)
            {
                case IrReturn returned: text.Append("    result = v").Append(N(returned.Value)).Append("; return true;\n"); break;
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    text.Append("    if (").Append(CppEmitter.Comparison(branch.Comparison, branch.Left, branch.Right, wide: types[branch.Left] == IrType.Int64)).Append(") {\n");
                    Edge(branch.Taken); text.Append("    } else {\n"); Edge(branch.NotTaken); text.Append("    }\n"); break;
            }
            text.Append("    }\n");
        }
        text.Append("}\n");
        Header(text, method, false, module); text.Append(" {\n#if CILICA_TYPED_INTERNALS\n")
            .Append("    if (context.depth != 0u && context.depth < 64u && context.pending == 0u && context.exception == nullptr) {\n")
            .Append("        ").Append(Type(method.ReturnType)).Append(" result = 0;\n        if (").Append(Symbol(method)).Append("_kernel(context");
        for (int i = 0; i < method.Parameters.Length; i++) text.Append(", p").Append(N(i));
        text.Append(", result)) return std::bit_cast<").Append(Signed(method.ReturnType)).Append(">(result);\n    }\n#endif\n")
            .Append("    ").Append(argumentType).Append(" arguments[").Append(N(Math.Max(1, method.Parameters.Length))).Append("]{};\n");
        for (int i = 0; i < method.Parameters.Length; i++)
            text.Append("    arguments[").Append(N(i)).Append("].").Append(Member(method.Parameters[i])).Append(" = p").Append(N(i)).Append(";\n");
        text.Append("    return ").Append(Symbol(method)).Append("_framed(context, arguments);\n}\n");
        text.Append(module ? "[[maybe_unused]] " : "[[maybe_unused]] static ").Append(Signed(method.ReturnType)).Append(' ').Append(Symbol(method))
            .Append("(Context& context, [[maybe_unused]] const ").Append(argumentType).Append("* arguments) noexcept {\n    return ").Append(Symbol(method)).Append("_typed(context");
        for (int i = 0; i < method.Parameters.Length; i++) text.Append(", arguments[").Append(N(i)).Append("].").Append(Member(method.Parameters[i]));
        text.Append(");\n}\n");
        void Edge(IrEdge edge)
        {
            for (int i = 0; i < edge.Arguments.Length; i++)
                text.Append("        s").Append(N(edge.Target)).Append('_').Append(N(i)).Append(" = v").Append(N(edge.Arguments[i])).Append(";\n");
            text.Append("        goto b").Append(N(edge.Target)).Append(";\n");
        }
    }
}
