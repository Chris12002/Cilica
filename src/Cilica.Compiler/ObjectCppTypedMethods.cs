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
    {
        var candidates = methods.Where(Eligible).ToArray();
        var result = new Dictionary<IrMethodId, IrMethod>();
        var costs = new Dictionary<IrMethodId, (int Depth, int Size)>();
        bool changed;
        do
        {
            changed = false;
            foreach (var method in candidates)
            {
                var id = IrMethodId.From(method.Source);
                if (result.ContainsKey(id)) continue;
                var calls = method.Blocks.SelectMany(b => b.Values).Select(v => v.Expression).OfType<IrExpression.Call>().ToArray();
                // Cycles and indirect calls never acquire a proof. Borrow arguments
                // remain local to their entry kernel; no byref forwarding in this slice.
                if (calls.Any(c => !result.TryGetValue(c.Target, out var target)
                    || target.Parameters.Any(t => t.Kind == IrTypeKind.ManagedLocation))) continue;
                int depth = 1 + calls.Select(c => costs[c.Target].Depth).DefaultIfEmpty().Max();
                int size = method.Blocks.Sum(b => b.Statements.Length + b.Inputs.Length) + calls.Sum(c => costs[c.Target].Size);
                if (depth > 8 || size > 256) continue;
                result.Add(id, method); costs.Add(id, (depth, size)); changed = true;
            }
        } while (changed);
        return result;
    }

    private static bool Scalar(IrType type) => type.Kind is IrTypeKind.Int32 or IrTypeKind.Int64;
    private static bool Plain(IrType type) => Scalar(type) || type.Kind is IrTypeKind.Reference or IrTypeKind.NullReference;
    private static bool Parameter(IrType type) => Plain(type) || type.Kind == IrTypeKind.ManagedLocation
        && type.Narrow is null && type.Element is { } element && Plain(element);
    private static bool Eligible(IrMethod method)
    {
        if (!Scalar(method.ReturnType) || method.Parameters.Length > 4 || method.Locals.Length > 8
            || !method.Parameters.All(Parameter) || !method.Locals.All(Plain) || !method.ExceptionRegions.IsEmpty
            || method.Blocks.Length > 16 || method.Blocks.Sum(b => b.Statements.Length + b.Inputs.Length) > 96)
            return false;
        foreach (var block in method.Blocks)
        {
            if (!block.Inputs.All(Plain) || block.Terminator is not (IrReturn or IrJump or IrBranch)) return false;
            var values = block.Values.ToDictionary(v => v.Id);
            foreach (var statement in block.Statements)
            {
                if (statement is IrStatement.StoreLocal) continue;
                if (statement is not IrStatement.Define { Value: var value }) return false;
                if (value.Type.Kind == IrTypeKind.ManagedLocation && value.Expression is not IrExpression.Parameter) return false;
                if (!Plain(value.Type) && value.Type.Kind != IrTypeKind.ManagedLocation) return false;
                if (value.Expression is IrExpression.LoadLocation load)
                {
                    if (values[load.Location].Expression is not IrExpression.Parameter) return false;
                }
                else if (value.Expression is IrExpression.Call call)
                {
                    if (call.VirtualDispatch || call.ValueKeyContract is not null) return false;
                }
                else if (value.Expression is not (IrExpression.Constant or IrExpression.Constant64 or IrExpression.Parameter
                    or IrExpression.Local or IrExpression.StackInput or IrExpression.Binary or IrExpression.Binary64
                    or IrExpression.Unary or IrExpression.Not64 or IrExpression.ConvertInteger or IrExpression.IntegerCast
                    or IrExpression.Compare or IrExpression.Null or IrExpression.StringLength or IrExpression.StringCodeUnit
                    or IrExpression.StringEquals)) return false;
            }
        }
        return true;
    }

    private static IrType Resolved(IrType type) => type.Kind == IrTypeKind.ManagedLocation ? type.Element! : type;
    internal static string Type(IrType type) => Resolved(type).Kind switch
    {
        IrTypeKind.Int64 => "std::uint64_t", IrTypeKind.Reference or IrTypeKind.NullReference => "Object*", _ => "std::uint32_t",
    };
    internal static string Symbol(IrMethod method) => CppEmitter.Symbol(IrMethodId.From(method.Source));
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Signed(IrType type) => type.Kind == IrTypeKind.Int64 ? "std::int64_t" : "std::int32_t";
    private static string Member(IrType type) => type.Kind switch
    {
        IrTypeKind.Int64 => "wide_bits", IrTypeKind.ManagedLocation => "location", IrTypeKind.Reference => "reference", _ => "bits",
    };
    private static void Header(StringBuilder text, IrMethod method, bool kernel, bool module)
    {
        text.Append(module ? "[[maybe_unused]] " : "[[maybe_unused]] static ")
            .Append(kernel ? "bool" : Signed(method.ReturnType)).Append(' ').Append(Symbol(method)).Append(kernel ? "_kernel" : "_typed")
            .Append("([[maybe_unused]] Context& context");
        for (int i = 0; i < method.Parameters.Length; i++)
            text.Append(", [[maybe_unused]] ").Append(!kernel && method.Parameters[i].Kind == IrTypeKind.ManagedLocation ? "const Location&" : Type(method.Parameters[i])).Append(" p").Append(N(i));
        if (kernel) text.Append(", [[maybe_unused]] ").Append(Type(method.ReturnType)).Append("& result");
        text.Append(") noexcept");
    }
    internal static void Declare(StringBuilder text, IrMethod method, bool module)
    {
        Header(text, method, true, module); text.Append(";\n");
        Header(text, method, false, module); text.Append(";\n");
    }

    internal static void Append(StringBuilder text, IrMethod method, string argumentType, bool module, ObjectTypeId objectBase, Func<ObjectTypeId, string> predicate)
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
                string expression;
                switch (value.Expression)
                {
                    case IrExpression.Parameter parameter: expression = "p" + N(parameter.Index); break;
                    case IrExpression.Null: expression = "nullptr"; break;
                    case IrExpression.LoadLocation load: expression = "v" + N(load.Location); break;
                    case IrExpression.StringLength length:
                        text.Append("    if (v").Append(N(length.Operand)).Append(" == nullptr) return false;\n");
                        expression = "string_length(v" + N(length.Operand) + ")"; break;
                    case IrExpression.StringCodeUnit unit:
                        text.Append("    if (v").Append(N(unit.Operand)).Append(" == nullptr || v").Append(N(unit.Index))
                            .Append(" >= string_length(v").Append(N(unit.Operand)).Append(")) return false;\n");
                        expression = "string_code_unit(v" + N(unit.Operand) + ", v" + N(unit.Index) + ")"; break;
                    case IrExpression.StringEquals equal: expression = "string_equals(v" + N(equal.Left) + ", v" + N(equal.Right) + ")"; break;
                    case IrExpression.Compare compare:
                        expression = "(" + CppEmitter.Comparison(compare.Comparison, compare.Left, compare.Right,
                            reference: types[compare.Left].Kind is IrTypeKind.Reference or IrTypeKind.NullReference,
                            wide: types[compare.Left] == IrType.Int64) + " ? 1u : 0u)"; break;
                    case IrExpression.Call call:
                        if (call.NullCheckReceiver) text.Append("    if (v").Append(N(call.Arguments[0])).Append(" == nullptr) return false;\n");
                        text.Append("    ").Append(Type(value.Type)).Append(" v").Append(N(value.Id)).Append(" = 0;\n    if (!")
                            .Append(CppEmitter.Symbol(call.Target)).Append("_kernel(context");
                        foreach (int arg in call.Arguments) text.Append(", v").Append(N(arg));
                        text.Append(", v").Append(N(value.Id)).Append(")) return false;\n");
                        continue;
                    default:
                        expression = CppEmitter.AppendValuePreparation(text, value, block.Id,
                            (_, _) => text.Append("        return false;\n"), operandType: id => types[id]); break;
                }
                text.Append("    [[maybe_unused]] ").Append(Type(value.Type)).Append(" const v").Append(N(value.Id)).Append(" = ").Append(expression).Append(";\n");
            }
            switch (block.Terminator)
            {
                case IrReturn returned: text.Append("    result = v").Append(N(returned.Value)).Append("; return true;\n"); break;
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    text.Append("    if (").Append(CppEmitter.Comparison(branch.Comparison, branch.Left, branch.Right, reference: types[branch.Left].Kind is IrTypeKind.Reference or IrTypeKind.NullReference, wide: types[branch.Left] == IrType.Int64)).Append(") {\n");
                    Edge(branch.Taken); text.Append("    } else {\n"); Edge(branch.NotTaken); text.Append("    }\n"); break;
            }
            text.Append("    }\n");
        }
        text.Append("}\n");
        Header(text, method, false, module); text.Append(" {\n#if CILICA_TYPED_INTERNALS\n")
            .Append("    if (context.depth != 0u && context.depth < 64u && context.pending == 0u && context.exception == nullptr) {\n");
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            var type = method.Parameters[i]; string argument = "p" + N(i);
            if (type.Kind == IrTypeKind.ManagedLocation)
            {
                string kind = type.Element!.Kind switch { IrTypeKind.Int64 => "wide_integer", IrTypeKind.Reference => "reference", _ => "integer" };
                string accepts = type.Element.Kind == IrTypeKind.Reference ? predicate(type.Element.Class!) : "nullptr";
                text.Append("        if (!valid_location_argument(context, ").Append(argument).Append(", StorageKind::").Append(kind)
                    .Append(", ").Append(accepts).Append(", ").Append(type.ReadOnly ? "true" : "false").Append(")) goto typed_fallback;\n")
                    .Append("        ").Append(Type(type)).Append(" resolved").Append(N(i)).Append(" = 0;\n")
                    .Append("        if (load_location_").Append(kind).Append("(context, ").Append(argument).Append(", resolved").Append(N(i))
                    .Append(") != LocationStatus::complete) goto typed_fallback;\n");
            }
            else if (type.Kind == IrTypeKind.Reference)
            {
                text.Append("        if (!internal_reference_owned(context.instance, ").Append(argument).Append(")");
                if (type.Class != objectBase) text.Append(" || (").Append(argument).Append(" != nullptr && !").Append(predicate(type.Class!)).Append('(').Append(argument).Append("->type))");
                text.Append(") goto typed_fallback;\n");
            }
        }
        text.Append("        ").Append(Type(method.ReturnType)).Append(" result = 0;\n        if (").Append(Symbol(method)).Append("_kernel(context");
        for (int i = 0; i < method.Parameters.Length; i++) text.Append(method.Parameters[i].Kind == IrTypeKind.ManagedLocation ? ", resolved" : ", p").Append(N(i));
        text.Append(", result)) return std::bit_cast<").Append(Signed(method.ReturnType)).Append(">(result);\n    }\n    [[maybe_unused]] typed_fallback:;\n#endif\n")
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
