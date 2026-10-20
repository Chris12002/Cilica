using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>
/// Closed proofs for side-effect-free native kernels over validated, type-safe IR.
/// Entry wrappers check incoming references and borrows. Kernels preserve declared
/// types through native locals, typed storage reads and the closed call graph.
/// No allocation, external write or unknown call can invalidate those facts.
/// Failure retries the original framed method.
/// </summary>
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
// The existing acyclic depth/expansion proof bounds forced native inlining.
#if defined(__OPTIMIZE__)
#define CILICA_TYPED_KERNEL [[maybe_unused, gnu::always_inline]]
#else
#define CILICA_TYPED_KERNEL [[maybe_unused]]
#endif
struct TypedCallDepth {
    std::size_t& depth;
    explicit TypedCallDepth(Context& context) noexcept : depth(context.depth) { ++depth; }
    ~TypedCallDepth() noexcept { --depth; }
    TypedCallDepth(const TypedCallDepth&) = delete;
    TypedCallDepth& operator=(const TypedCallDepth&) = delete;
};
""";

    internal sealed class Plan : Dictionary<IrMethodId, IrMethod>
    {
        internal readonly Dictionary<(IrMethodId Method, IrMethodId Declaration), ObjectVirtualTarget[]> Dispatch = [];
        internal readonly HashSet<IrMethodId> Writes = [];
    }
    private sealed record CallSite(IrMethodId Target, ImmutableArray<int> Arguments, bool Virtual, bool NullCheck);
    private static IEnumerable<CallSite> Calls(IrMethod method) => method.Blocks.SelectMany(b => b.Statements).Select(s => s switch
    {
        IrStatement.Invoke i => new CallSite(i.Target, i.Arguments, i.VirtualDispatch, i.NullCheckReceiver),
        IrStatement.Define { Value.Expression: IrExpression.Call c } => new CallSite(c.Target, c.Arguments, c.VirtualDispatch, c.NullCheckReceiver),
        _ => null,
    }).OfType<CallSite>();

    internal static Plan Analyze(ObjectIrProgram program)
    {
        var candidates = program.Flow.Methods.Where(Eligible).ToArray();
        var result = new Plan();
        var costs = new Dictionary<IrMethodId, (int Depth, int Size)>();
        var boxed = program.InterfaceDispatch?.Methods.Where(m => m.BoxedDeclaration is not null).Select(m => m.Binding.Method).ToHashSet() ?? [];
        var dispatch = program.Dispatch.ToDictionary(d => d.Declaration, d => d.Targets.ToArray());
        if (program.InterfaceDispatch is { } interfaces)
            foreach (var d in interfaces.Dispatch)
                dispatch.Add(d.Declaration, d.Targets.Where(t => !boxed.Contains(t.Implementation))
                    .Select(t => new ObjectVirtualTarget(t.Receiver, t.Implementation)).ToArray());
        bool changed;
        do
        {
            changed = false;
            foreach (var method in candidates)
            {
                var id = IrMethodId.From(method.Source);
                if (result.ContainsKey(id)) continue;
                var callees = new List<IrMethodId>();
                var selected = new Dictionary<IrMethodId, ObjectVirtualTarget[]>();
                bool ready = true;
                foreach (var call in Calls(method))
                {
                    if (!call.Virtual)
                    {
                        if (!result.ContainsKey(call.Target)) { ready = false; break; }
                        callees.Add(call.Target);
                    }
                    else
                    {
                        var targets = dispatch.GetValueOrDefault(call.Target, []).Where(t => result.ContainsKey(t.Implementation)).ToArray();
                        if (targets.Length is 0 or > 16) { ready = false; break; }
                        selected[call.Target] = targets;
                        callees.AddRange(targets.Select(t => t.Implementation));
                    }
                }
                if (!ready) continue;
                int depth = 1 + callees.Select(c => costs[c].Depth).DefaultIfEmpty().Max();
                int size = method.Blocks.Sum(b => b.Statements.Length + b.Inputs.Length) + callees.Sum(c => costs[c].Size);
                if (depth > 8 || size > 512) continue;
                result.Add(id, method); costs.Add(id, (depth, size)); changed = true;
                foreach (var pair in selected) result.Dispatch.Add((id, pair.Key), pair.Value);
                if (method.Blocks.SelectMany(b => b.Statements).Any(s => s is IrStatement.StoreLocation or IrStatement.InitializeLocation)
                    || callees.Any(result.Writes.Contains)) result.Writes.Add(id);
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
        if (!(Plain(method.ReturnType) || method.ReturnType.Kind == IrTypeKind.Void) || ObjectCppLeafMethods.FieldGetter(method) is not null
            || method.Parameters.Length > 4 || method.Locals.Length > 8
            || !method.Parameters.All(Parameter) || !method.Locals.All(Plain) || !method.ExceptionRegions.IsEmpty
            || method.Blocks.Length > 16 || method.Blocks.Sum(b => b.Statements.Length + b.Inputs.Length) > 96)
            return false;
        foreach (var block in method.Blocks)
        {
            if (!block.Inputs.All(Plain) || block.Terminator is not (IrReturn or IrVoidReturn or IrThrow or IrJump or IrBranch)) return false;
            var values = block.Values.ToDictionary(v => v.Id);
            foreach (var statement in block.Statements)
            {
                if (statement is IrStatement.StoreLocal or IrStatement.Invoke) continue;
                if (statement is IrStatement.StoreLocation or IrStatement.InitializeLocation)
                {
                    int location = statement is IrStatement.StoreLocation store ? store.Location : ((IrStatement.InitializeLocation)statement).Location;
                    // Reference writes are limited to this kernel's own by-value parameters/locals.
                    // Incoming reference borrows are snapshots and must never be mutated.
                    bool ownReference = values[location].Type.Element!.Kind == IrTypeKind.Reference
                        && values[location].Expression is IrExpression.AddressLocal or IrExpression.AddressParameter;
                    if ((!Scalar(values[location].Type.Element!) && !ownReference) || values[location].Type.ReadOnly) return false;
                    continue;
                }
                if (statement is not IrStatement.Define { Value: var value }) return false;
                if (!Parameter(value.Type)) return false;
                if (value.Type.Kind == IrTypeKind.ManagedLocation && value.Expression is not
                    (IrExpression.Parameter or IrExpression.AddressLocal or IrExpression.AddressParameter or IrExpression.ReadOnlyLocation)) return false;
                if (value.Expression is IrExpression.Call call)
                {
                    if (call.ValueKeyContract is not null) return false;
                }
                else if (value.Expression is not (IrExpression.Constant or IrExpression.Constant64 or IrExpression.Parameter
                    or IrExpression.Local or IrExpression.StackInput or IrExpression.Binary or IrExpression.Binary64
                    or IrExpression.Unary or IrExpression.Not64 or IrExpression.ConvertInteger or IrExpression.IntegerCast
                    or IrExpression.Compare or IrExpression.Null or IrExpression.StringLength or IrExpression.StringCodeUnit
                    or IrExpression.StringEquals or IrExpression.LoadLocation or IrExpression.AddressLocal or IrExpression.AddressParameter
                    or IrExpression.ReadOnlyLocation or IrExpression.LoadField or IrExpression.ArrayLength or IrExpression.LoadElement
                    or IrExpression.StringLiteral or IrExpression.NewException)) return false;
            }
        }
        return true;
    }

    private static IrType Resolved(IrType type) => type.Kind == IrTypeKind.ManagedLocation ? type.Element! : type;
    internal static string Type(IrType type) => type.Kind == IrTypeKind.ManagedLocation ? Type(type.Element!) + "*" : type.Kind switch
    {
        IrTypeKind.Int64 => "std::uint64_t", IrTypeKind.Reference or IrTypeKind.NullReference => "Object*", _ => "std::uint32_t",
    };
    internal static string Symbol(IrMethod method) => CppEmitter.Symbol(IrMethodId.From(method.Source));
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Signed(IrType type) => type.Kind switch { IrTypeKind.Int64 => "std::int64_t", IrTypeKind.Reference => "Object*", IrTypeKind.Void => "void", _ => "std::int32_t" };
    private static string Member(IrType type) => type.Kind switch
    {
        IrTypeKind.Int64 => "wide_bits", IrTypeKind.ManagedLocation => "location", IrTypeKind.Reference => "reference", _ => "bits",
    };
    private static void Header(StringBuilder text, IrMethod method, bool kernel, bool module)
    {
        // Kernel prototypes and bodies belong to shared storage. The module builder
        // promotes static to inline, so every unit sees a body with one identity.
        text.Append(kernel ? "CILICA_TYPED_KERNEL static " : module ? "[[maybe_unused]] " : "[[maybe_unused]] static ")
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

    internal static void AppendKernel(StringBuilder text, IrMethod method, ObjectTypeId objectBase, Func<ObjectTypeId, string> predicate, Plan plan, ObjectTypeCatalog catalog, ObjectNativeLayout native, Func<ObjectTypeId, string> typeSymbol, Dictionary<ObjectFieldId, ObjectTypeId[]> fieldAccess, Func<ObjectFieldId, string> fieldSymbol)
    {
        var id = IrMethodId.From(method.Source);
        var fields = catalog.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
        Header(text, method, true, module: false);
        text.Append(" {\n    // Proven typed kernel: no safepoint or external write; scalar frame writes are staged.\n")
            .Append("    if (context.depth >= 64u) return false;\n    TypedCallDepth depth(context);\n");
        for (int i = 0; i < method.Locals.Length; i++)
            text.Append("    [[maybe_unused]] ").Append(Type(method.Locals[i])).Append(" l").Append(N(i)).Append(" = 0;\n");
        foreach (var block in method.Blocks)
            for (int i = 0; i < block.Inputs.Length; i++)
                text.Append("    [[maybe_unused]] ").Append(Type(block.Inputs[i])).Append(" s").Append(N(block.Id)).Append('_').Append(N(i)).Append(" = 0;\n");
        text.Append("    goto b0;\n");
        int invocation = 0;
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
                if (statement is IrStatement.Invoke invoke)
                {
                    int callIndex = invocation++;
                    text.Append("    [[maybe_unused]] std::uint32_t ignored").Append(N(callIndex)).Append(" = 0;\n");
                    Call(new(invoke.Target, invoke.Arguments, invoke.VirtualDispatch, invoke.NullCheckReceiver), "ignored" + N(callIndex)); continue;
                }
                if (statement is IrStatement.StoreLocation write)
                { text.Append("    *v").Append(N(write.Location)).Append(" = v").Append(N(write.Value)).Append(";\n"); continue; }
                if (statement is IrStatement.InitializeLocation zero)
                { text.Append("    *v").Append(N(zero.Location)).Append(" = 0;\n"); continue; }
                var value = ((IrStatement.Define)statement).Value;
                text.Append("    // IL_").Append(value.ILOffset.ToString("x4", CultureInfo.InvariantCulture)).Append('\n');
                string expression;
                switch (value.Expression)
                {
                    case IrExpression.Parameter parameter: expression = "p" + N(parameter.Index); break;
                    case IrExpression.Null: expression = "nullptr"; break;
                    case IrExpression.LoadLocation load: expression = "*v" + N(load.Location); break;
                    case IrExpression.AddressLocal address: expression = "&l" + N(address.Index); break;
                    case IrExpression.AddressParameter address: expression = "&p" + N(address.Index); break;
                    case IrExpression.ReadOnlyLocation read: expression = "v" + N(read.Location); break;
                    case IrExpression.StringLiteral or IrExpression.NewException:
                        // Cold allocation/throw work is replayed by the framed method.
                        text.Append("    return false;\n"); expression = "nullptr"; break;
                    case IrExpression.LoadField field:
                        Check("v" + N(field.Receiver), field.Field.Owner, true);
                        string storage = fields[field.Field].Storage switch { ObjectStorage.Int32 => "std::uint32_t", ObjectStorage.Int64 => "std::uint64_t", _ => "Object*" };
                        expression = "*static_cast<" + storage + "*>(" + ObjectCppFieldAccess.Address(field.Field, "v" + N(field.Receiver), native, fieldAccess, typeSymbol, fieldSymbol) + ")"; break;
                    case IrExpression.ArrayLength length:
                        CheckArray("v" + N(length.Array), null, null);
                        expression = "array_length(v" + N(length.Array) + ")"; break;
                    case IrExpression.LoadElement element:
                        CheckArray("v" + N(element.Array), element.ArrayType, "v" + N(element.Index));
                        expression = (value.Type.Kind == IrTypeKind.Int32 ? "load_integer_element" : value.Type.Kind == IrTypeKind.Int64 ? "load_wide_element" : "load_reference_element")
                            + "(v" + N(element.Array) + ", v" + N(element.Index) + ")"; break;
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
                        text.Append("    ").Append(Type(value.Type)).Append(" v").Append(N(value.Id)).Append(" = 0;\n");
                        Call(new(call.Target, call.Arguments, call.VirtualDispatch, call.NullCheckReceiver), "v" + N(value.Id));
                        continue;
                    default:
                        expression = CppEmitter.AppendValuePreparation(text, value, block.Id,
                            (_, _) => text.Append("        return false;\n"), operandType: id => types[id]); break;
                }
                text.Append("    [[maybe_unused]] ").Append(Type(value.Type)).Append(" const v").Append(N(value.Id)).Append(" = ").Append(expression).Append(";\n");
                if (value.Type.Kind == IrTypeKind.Reference && value.Expression is IrExpression.LoadField or IrExpression.LoadElement or IrExpression.LoadLocation)
                    Check("v" + N(value.Id), value.Type.Class!, false);
            }
            switch (block.Terminator)
            {
                case IrReturn returned: text.Append("    result = v").Append(N(returned.Value)).Append("; return true;\n"); break;
                case IrVoidReturn: text.Append("    result = 0; return true;\n"); break;
                case IrThrow: text.Append("    return false;\n"); break;
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    text.Append("    if (").Append(CppEmitter.Comparison(branch.Comparison, branch.Left, branch.Right, reference: types[branch.Left].Kind is IrTypeKind.Reference or IrTypeKind.NullReference, wide: types[branch.Left] == IrType.Int64)).Append(") {\n");
                    Edge(branch.Taken); text.Append("    } else {\n"); Edge(branch.NotTaken); text.Append("    }\n"); break;
            }
            text.Append("    }\n");
        }
        text.Append("}\n");

        void Check(string value, ObjectTypeId type, bool nonnull)
        {
            if (nonnull) text.Append("    if (").Append(value).Append(" == nullptr) return false;\n");
            // The IR validator proves field/array/call compatibility. Inputs are
            // checked at the wrapper and managed storage preserves declared types.
            // Retain corruption diagnostics without re-proving every native read.
            text.Append("#if CILICA_VERIFY_INTERNAL_REFERENCES\n")
                .Append("    if (!internal_reference_owned(context.instance, ").Append(value).Append(") || (").Append(value).Append(" != nullptr && !")
                .Append(predicate(type)).Append('(').Append(value).Append("->type))) return false;\n#endif\n");
        }
        void CheckArray(string value, ObjectTypeId? type, string? index)
        {
            Check(value, type ?? objectBase, true);
            text.Append("#if CILICA_VERIFY_INTERNAL_REFERENCES\n")
                .Append("    if (!valid_array_storage(").Append(value).Append(")) { context.pending = 6u; return false; }\n#endif\n");
            if (index is not null)
                text.Append("    if (").Append(index).Append(" >= array_length(").Append(value).Append(")) return false;\n");
        }
        void Call(CallSite call, string result)
        {
            if (call.NullCheck || call.Virtual) text.Append("    if (v").Append(N(call.Arguments[0])).Append(" == nullptr) return false;\n");
            if (!call.Virtual) Invoke(call.Target);
            else
            {
                var targets = plan.Dispatch[(id, call.Target)];
                for (int i = 0; i < targets.Length; i++)
                {
                    text.Append(i == 0 ? "    if (" : "    else if (").Append("v").Append(N(call.Arguments[0])).Append("->type == &")
                        .Append(typeSymbol(targets[i].Receiver)).Append("_descriptor) {\n");
                    Invoke(targets[i].Implementation); text.Append("    }\n");
                }
                text.Append("    else return false;\n");
            }
            void Invoke(IrMethodId target)
            {
                text.Append("    if (!").Append(CppEmitter.Symbol(target)).Append("_kernel(context");
                foreach (int arg in call.Arguments) text.Append(", v").Append(N(arg));
                text.Append(", ").Append(result).Append(")) return false;\n");
            }
        }
        void Edge(IrEdge edge)
        {
            for (int i = 0; i < edge.Arguments.Length; i++)
                text.Append("        s").Append(N(edge.Target)).Append('_').Append(N(i)).Append(" = v").Append(N(edge.Arguments[i])).Append(";\n");
            text.Append("        goto b").Append(N(edge.Target)).Append(";\n");
        }
    }
    internal static void Append(StringBuilder text, IrMethod method, string argumentType, bool module,
        ObjectTypeId objectBase, Func<ObjectTypeId, string> predicate, Plan plan)
    {
        var id = IrMethodId.From(method.Source);
        Header(text, method, false, module); text.Append(" {\n#if CILICA_TYPED_INTERNALS\n")
            .Append("    if (context.depth != 0u && context.depth < 64u && context.pending == 0u && context.exception == nullptr) {\n");
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            var type = method.Parameters[i]; string argument = "p" + N(i);
            if (type.Kind == IrTypeKind.ManagedLocation)
            {
                string kind = type.Element!.Kind switch { IrTypeKind.Int64 => "wide_integer", IrTypeKind.Reference => "reference", _ => "integer" };
                string accepts = type.Element.Kind == IrTypeKind.Reference ? predicate(type.Element.Class!) : "nullptr";
                text.Append("        const auto storage").Append(N(i)).Append(" = location_storage(context, ").Append(argument).Append(", ")
                    .Append(type.ReadOnly ? "false" : "true").Append(");\n")
                    .Append("        if (storage").Append(N(i)).Append(".status != LocationStatus::complete || storage").Append(N(i)).Append(".kind != StorageKind::").Append(kind)
                    .Append(" || storage").Append(N(i)).Append(".accepts != ").Append(accepts).Append(" || ").Append(argument).Append(".narrow != LocationNarrow::none) goto typed_fallback;\n");
                if (plan.Writes.Contains(id) && Scalar(type.Element))
                    text.Append("        if (").Append(argument).Append(".origin != LocationOrigin::frame_").Append(kind).Append(") goto typed_fallback;\n");
                text.Append("        ").Append(Type(type.Element)).Append(" resolved").Append(N(i)).Append(" = *static_cast<").Append(Type(type)).Append(">(storage").Append(N(i)).Append(".address);\n");
                if (type.Element.Kind == IrTypeKind.Reference)
                {
                    text.Append("        if (!location_reference(context.instance, ").Append(accepts).Append(", resolved").Append(N(i)).Append(")) goto typed_fallback;\n");
                    CheckInputArray("resolved" + N(i), type.Element);
                }
                text.Append("        ").Append(Type(type)).Append(" borrow").Append(N(i)).Append(" = &resolved").Append(N(i)).Append(";\n");
                for (int j = 0; j < i; j++)
                    if (method.Parameters[j].Kind == IrTypeKind.ManagedLocation && method.Parameters[j].Element == type.Element)
                        text.Append("        if (storage").Append(N(i)).Append(".address == storage").Append(N(j)).Append(".address) borrow").Append(N(i)).Append(" = borrow").Append(N(j)).Append(";\n");
            }
            else if (type.Kind == IrTypeKind.Reference)
            {
                text.Append("        if (!internal_reference_owned(context.instance, ").Append(argument).Append(")");
                if (type.Class != objectBase) text.Append(" || (").Append(argument).Append(" != nullptr && !").Append(predicate(type.Class!)).Append('(').Append(argument).Append("->type))");
                text.Append(") goto typed_fallback;\n");
                CheckInputArray(argument, type);
            }
        }
        text.Append("        ").Append(Type(method.ReturnType)).Append(" result = 0;\n        if (").Append(Symbol(method)).Append("_kernel(context");
        for (int i = 0; i < method.Parameters.Length; i++) text.Append(method.Parameters[i].Kind == IrTypeKind.ManagedLocation ? ", borrow" : ", p").Append(N(i));
        text.Append(", result)) {\n");
        if (plan.Writes.Contains(id))
            for (int i = 0; i < method.Parameters.Length; i++)
                if (method.Parameters[i].Kind == IrTypeKind.ManagedLocation && Scalar(method.Parameters[i].Element!) && !method.Parameters[i].ReadOnly)
                    text.Append("            *static_cast<").Append(Type(method.Parameters[i])).Append(">(storage").Append(N(i)).Append(".address) = *borrow").Append(N(i)).Append(";\n");
        text.Append("            return");
        if (method.ReturnType.Kind == IrTypeKind.Reference) text.Append(" result");
        else if (Scalar(method.ReturnType)) text.Append(" std::bit_cast<").Append(Signed(method.ReturnType)).Append(">(result)");
        text.Append(";\n        }\n    }\n    [[maybe_unused]] typed_fallback:;\n#endif\n")
            .Append("    ").Append(argumentType).Append(" arguments[").Append(N(Math.Max(1, method.Parameters.Length))).Append("]{};\n");
        for (int i = 0; i < method.Parameters.Length; i++)
            text.Append("    arguments[").Append(N(i)).Append("].").Append(Member(method.Parameters[i])).Append(" = p").Append(N(i)).Append(";\n");
        text.Append("    return ").Append(Symbol(method)).Append("_framed(context, arguments);\n}\n");
        text.Append(module ? "[[maybe_unused]] " : "[[maybe_unused]] static ").Append(Signed(method.ReturnType)).Append(' ').Append(Symbol(method))
            .Append("(Context& context, [[maybe_unused]] const ").Append(argumentType).Append("* arguments) noexcept {\n    return ").Append(Symbol(method)).Append("_typed(context");
        for (int i = 0; i < method.Parameters.Length; i++) text.Append(", arguments[").Append(N(i)).Append("].").Append(Member(method.Parameters[i]));
        text.Append(");\n}\n");
        void CheckInputArray(string value, IrType type)
        {
            if (type.Class?.ArrayElement is not null)
                // Malformed private storage is not a managed fault to replay:
                // ordinary framed array reads also trust the backing allocation.
                text.Append("        if (").Append(value).Append(" != nullptr && !valid_array_storage(").Append(value)
                    .Append(")) { context.pending = 6u; goto typed_fallback; }\n");
        }
    }
}
