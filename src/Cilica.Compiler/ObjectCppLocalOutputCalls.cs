using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Adjacent, single-use scalar local outputs into the closed no-safepoint kernel graph.</summary>
internal static class ObjectCppLocalOutputCalls
{
    internal const string Runtime = """
[[maybe_unused]] static bool direct_local_output(const Context& context) noexcept {
    return CILICA_TYPED_INTERNALS && !CILICA_VERIFY_BORROW_LIFETIMES && !CILICA_VERIFY_INTERNAL_REFERENCES
        && context.depth != 0u && context.depth < 64u && context.pending == 0u
        && context.exception == nullptr && !context.instance.marking;
}
""";
    internal sealed record Site(int Local, int Argument, IrMethodId Declaration, ImmutableArray<int> Arguments, ObjectVirtualTarget[] Targets);
    internal static int BorrowIndex(IrMethod method)
    {
        if (method.ReturnType.Kind is not (IrTypeKind.Int32 or IrTypeKind.Int64)) return -1;
        var borrows = method.Parameters.Select((type, index) => (type, index)).Where(p => p.type.Kind == IrTypeKind.ManagedLocation).ToArray();
        return borrows.Length == 1 && !borrows[0].type.ReadOnly && borrows[0].type.Narrow is null
            && borrows[0].type.Element!.Kind is IrTypeKind.Int32 or IrTypeKind.Int64 ? borrows[0].index : -1;
    }
    internal static Dictionary<(int Block, int Address), Site> Analyze(IrMethod method,
        ObjectCppTypedMethods.Plan typed, IEnumerable<ObjectVirtualDispatch> dispatch, HashSet<IrMethodId> boxed)
    {
        var maps = dispatch.ToDictionary(d => d.Declaration, d => d.Targets.ToArray());
        var result = new Dictionary<(int, int), Site>();
        foreach (var block in method.Blocks)
        {
            if (block.Statements.Length > 128) continue;
            int candidates = 0;
            for (int i = 0; i + 1 < block.Statements.Length; i++)
            {
                if (block.Statements[i] is not IrStatement.Define { Value: var address }
                    || address.Expression is not IrExpression.AddressLocal local || address.Type.ReadOnly || address.Type.Narrow is not null
                    || address.Type.Element != method.Locals[local.Index]
                    || block.Statements[i + 1] is not IrStatement.Define { Value.Expression: IrExpression.Call call }
                    || call.ValueKeyContract is not null || call.Arguments.Count(a => a == address.Id) != 1) continue;
                if (++candidates > 16) break;
                int argument = call.Arguments.IndexOf(address.Id);
                var targets = call.VirtualDispatch ? maps.GetValueOrDefault(call.Target, []) : [];
                if (call.VirtualDispatch && (targets.Length is 0 or > 4 || targets.Any(t => boxed.Contains(t.Implementation)))) continue;
                var ids = call.VirtualDispatch ? targets.Select(t => t.Implementation) : [call.Target];
                if (!ids.All(id => typed.TryGetValue(id, out var target) && typed.Writes.Contains(id)
                    && BorrowIndex(target) == argument && target.Parameters[argument] == address.Type)) continue;
                if (ObjectCppLocalCharacterCalls.SingleUse(block, address.Id, i + 1))
                    result.Add((block.Id, address.Id), new(local.Index, argument, call.Target, call.Arguments, targets));
            }
        }
        return result;
    }
    private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
    private static string Signed(IrMethod method) => method.ReturnType.Kind == IrTypeKind.Int64 ? "std::int64_t" : "std::int32_t";
    internal static void Header(StringBuilder text, IrMethod method, bool module)
    {
        text.Append(module ? "[[maybe_unused]] " : "[[maybe_unused]] static ").Append(Signed(method)).Append(' ')
            .Append(ObjectCppTypedMethods.Symbol(method)).Append("_local_output(Context& context");
        for (int i = 0; i < method.Parameters.Length; i++)
            text.Append(", ").Append(ObjectCppTypedMethods.Type(method.Parameters[i])).Append(" p").Append(N(i));
        text.Append(", std::uint64_t token, std::size_t slot) noexcept");
    }
    internal static void Append(StringBuilder text, IrMethod method, bool module, ObjectTypeId objectBase, Func<ObjectTypeId, string> predicate)
    {
        int borrow = BorrowIndex(method);
        if (borrow < 0) return;
        string symbol = ObjectCppTypedMethods.Symbol(method), b = N(borrow);
        Header(text, method, module);
        text.Append(" {\n    if (direct_local_output(context)) {\n");
        // Retain the same reference and array-storage entry checks as the general
        // typed wrapper. Only the compiler-proven scalar local bypasses a carrier.
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            var type = method.Parameters[i];
            if (type.Kind != IrTypeKind.Reference) continue;
            string p = "p" + N(i);
            text.Append("        if (!internal_reference_owned(context.instance, ").Append(p).Append(')');
            if (type.Class != objectBase) text.Append(" || (").Append(p).Append(" != nullptr && !").Append(predicate(type.Class!)).Append('(').Append(p).Append("->type))");
            text.Append(") goto local_fallback;\n");
            if (type.Class?.ArrayElement is not null)
                text.Append("        if (").Append(p).Append(" != nullptr && !valid_array_storage(").Append(p).Append(")) { context.pending = 6u; goto local_fallback; }\n");
        }
        text.Append("        auto staged = *p").Append(b).Append(";\n        ").Append(ObjectCppTypedMethods.Type(method.ReturnType))
            .Append(" result = 0;\n        if (").Append(symbol).Append("_kernel(context");
        for (int i = 0; i < method.Parameters.Length; i++) text.Append(i == borrow ? ", &staged" : ", p" + N(i));
        text.Append(", result)) {\n            *p").Append(b).Append(" = staged;\n            return std::bit_cast<").Append(Signed(method))
            .Append(">(result);\n        }\n    }\n    [[maybe_unused]] local_fallback:;\n")
            .Append("    const auto location = frame_location(context, token, LocationOrigin::frame_")
            .Append(method.Parameters[borrow].Element!.Kind == IrTypeKind.Int64 ? "wide_integer" : "integer")
            .Append(", slot);\n    if (location.status != LocationStatus::complete) { context.pending = 6u; return 0; }\n")
            .Append("    return ").Append(symbol).Append("_typed(context");
        for (int i = 0; i < method.Parameters.Length; i++) text.Append(i == borrow ? ", location.value" : ", p" + N(i));
        text.Append(");\n}\n");
    }
}
