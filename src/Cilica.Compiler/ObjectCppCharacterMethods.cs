using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Small allocating paths, separate from the no-safepoint typed kernels.</summary>
internal static class ObjectCppCharacterMethods
{
    internal const string LocalRuntime = """
// Only adjacent, nonescaping compiler-proven local reads may use this entry.
// Diagnostic builds retain the full carrier/lifetime path.
[[maybe_unused]] static bool direct_character_local(const Context& context) noexcept {
    return CILICA_TYPED_INTERNALS && !CILICA_VERIFY_BORROW_LIFETIMES
        && context.depth != 0u && context.depth < 63u
        && context.pending == 0u && context.exception == nullptr && !context.instance.marking;
}
""";

    internal sealed record Plan(int AllocationOffset, IrMethodId? Target = null);

    internal static Dictionary<IrMethodId, Plan> Analyze(ObjectIrProgram program)
    {
        var result = new Dictionary<IrMethodId, Plan>();
        if (program.Flow.Types.String is not { } text) return result;
        var methods = program.Flow.Methods.Where(m => m.Parameters.Length == 1
            && m.ReturnType == IrType.Reference(text) && m.Locals.IsEmpty
            && m.ExceptionRegions.IsEmpty && m.Blocks.Length == 1
            && m.Blocks[0].Inputs.IsEmpty && m.Blocks[0].Terminator is IrReturn).ToArray();
        foreach (var method in methods)
        {
            var statements = method.Blocks[0].Statements;
            if (method.Parameters[0] != IrType.Int32 || statements.Length != 3
                || statements[0] is not IrStatement.Define { Value: var parameter }
                || parameter.Expression is not IrExpression.Parameter { Index: 0 } || parameter.Type != IrType.Int32
                || statements[1] is not IrStatement.Define { Value: var narrowed } || !Narrow(narrowed, parameter.Id)
                || statements[2] is not IrStatement.Define { Value: var allocation, Exceptional: IrExceptionTarget.MethodExit }
                || allocation.Expression is not IrExpression.StringFromCharacter character || character.Character != narrowed.Id
                || allocation.Type != method.ReturnType
                || ((IrReturn)method.Blocks[0].Terminator).Value != allocation.Id) continue;
            result.Add(IrMethodId.From(method.Source), new(allocation.ILOffset));
        }
        // Exactly one borrowed scalar read and a call to an already-proven factory.
        // No recursive forwarding, arbitrary managed calls, handlers or reference arguments.
        foreach (var method in methods)
        {
            var statements = method.Blocks[0].Statements;
            if (method.Parameters[0] != IrType.Location(IrType.Int32, readOnly: true, narrow: IntegerCastTarget.UInt16)
                || statements.Length != 4
                || statements[0] is not IrStatement.Define { Value: var parameter }
                || parameter.Expression is not IrExpression.Parameter { Index: 0 } || parameter.Type != method.Parameters[0]
                || statements[1] is not IrStatement.Define { Value: var loaded, Exceptional: IrExceptionTarget.MethodExit }
                || loaded.Expression is not IrExpression.LoadLocation load || load.Location != parameter.Id || loaded.Type != IrType.Int32
                || statements[2] is not IrStatement.Define { Value: var narrowed } || !Narrow(narrowed, loaded.Id)
                || statements[3] is not IrStatement.Define { Value: var returned, Exceptional: IrExceptionTarget.MethodExit }
                || returned.Expression is not IrExpression.Call { VirtualDispatch: false, NullCheckReceiver: false, ValueKeyContract: null } call
                || call.Arguments.Length != 1 || call.Arguments[0] != narrowed.Id
                || !result.TryGetValue(call.Target, out var factory) || factory.Target is not null
                || returned.Type != method.ReturnType
                || ((IrReturn)method.Blocks[0].Terminator).Value != returned.Id) continue;
            result.Add(IrMethodId.From(method.Source), new(returned.ILOffset, call.Target));
        }
        return result;
    }

    private static bool Narrow(IrValue value, int operand) => value.Type == IrType.Int32
        && value.Expression is IrExpression.IntegerCast
        { Target: IntegerCastTarget.UInt16, Checked: false, UnsignedSource: false } cast && cast.Operand == operand;

    internal static void LocalHeader(StringBuilder code, IrMethod method, bool module) =>
        code.Append(module ? "[[maybe_unused]] " : "[[maybe_unused]] static ")
            .Append("Object* ").Append(CppEmitter.Symbol(IrMethodId.From(method.Source)))
            .Append("_local_character(Context& context, std::uint32_t character, const Location& fallback) noexcept");

    internal static void AppendLocal(StringBuilder code, IrMethod method, Plan plan, bool module)
    {
        LocalHeader(code, method, module);
        code.Append(" {\n    if (direct_character_local(context)) {\n")
            .Append("        // The caller owns the local; no owner root or location can escape.\n")
            .Append("        Frame frame; RootScope roots(context, frame, nullptr, 0);\n")
            .Append("        LocationArgument next{}; next.bits = character & 0xffffu;\n")
            .Append("        return ").Append(CppEmitter.Symbol(plan.Target!)).Append("(context, &next);\n")
            .Append("    }\n    LocationArgument argument{}; argument.location = fallback;\n")
            .Append("    return ").Append(CppEmitter.Symbol(IrMethodId.From(method.Source))).Append("(context, &argument);\n}\n");
    }

    internal static void Append(StringBuilder code, IrMethod method, Plan plan, string descriptor, bool handlers)
    {
        code.Append("#if CILICA_TYPED_INTERNALS\n")
            .Append("    // Proven character construction: real GC frames, one allocation, no replay after allocation.\n")
            .Append("    if (context.depth != 0u && context.depth < ").Append(plan.Target is null ? "64" : "63")
            .Append("u && context.pending == 0u && context.exception == nullptr) {\n");
        if (plan.Target is { } target)
        {
            code.Append("""
                        auto location = arguments[0].location; location.read_only = true;
                        std::uint32_t character = 0;
                        if (valid_location_argument(context, location, StorageKind::integer, nullptr, true, LocationNarrow::UInt16)
                            && load_location_integer(context, location, character) == LocationStatus::complete) {
                            Object* owner = location.owner; Frame frame;
                            RootScope roots(context, frame, &owner, 1);
                            LocationArgument next{}; next.bits = character & 0xffffu;
                """).Append("\n            return ").Append(CppEmitter.Symbol(target)).Append("(context, &next);\n        }\n");
        }
        else
        {
            code.Append("        Frame frame; RootScope roots(context, frame, nullptr, 0);\n")
                .Append("        auto* result = string_from_character(context, ").Append(descriptor).Append(", arguments[0].bits & 0xffffu);\n")
                .Append("        if (result == nullptr) {\n")
                .Append("            context.pending = context.pending == 0u ? 5u : context.pending; context.image = \"")
                .Append(method.Source.ContentHash.ToLowerInvariant()).Append("\"; context.method = 0x")
                .Append(method.Source.Token.ToString("x8", CultureInfo.InvariantCulture)).Append("u; context.il = ")
                .Append(plan.AllocationOffset.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
            if (handlers) code.Append("            (void)materialize_exception(context);\n");
            code.Append("        }\n        return result;\n");
        }
        code.Append("    }\n#endif\n");
    }
}
