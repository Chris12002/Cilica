using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

internal sealed class ObjectCppDelegates
{
    private readonly (ObjectTypeId Type, IrMethodId Target)[] _targets;
    private readonly Dictionary<(ObjectTypeId Type, IrMethodId Target), int> _indices;
    private readonly Dictionary<IrMethodId, ObjectMethodBinding> _bindings;
    private readonly Dictionary<IrMethodId, IrMethod> _methods;
    internal ObjectCppDelegates(ObjectIrProgram program)
    {
        _bindings = program.Bindings.ToDictionary(b => b.Method);
        _methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        _targets = program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values)
            .Select(v => v.Expression).OfType<IrExpression.NewDelegate>().Select(d => (d.Type, d.Target)).Distinct()
            .OrderBy(d => d.Type.ContentHash, StringComparer.Ordinal).ThenBy(d => d.Type.Token).ThenBy(d => d.Type.Specialization, StringComparer.Ordinal)
            .ThenBy(d => d.Target.ContentHash, StringComparer.Ordinal).ThenBy(d => d.Target.Token).ThenBy(d => d.Target.Specialization, StringComparer.Ordinal).ToArray();
        if (_targets.Length > 4096) throw new ArgumentException("Native delegate binding table exceeds 4096 targets.", nameof(program));
        _indices = _targets.Select((target, index) => (target, index)).ToDictionary(p => p.target, p => p.index);
    }
    internal int Index(IrExpression.NewDelegate creation) => _indices[(creation.Type, creation.Target)];
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static void AppendStorage(StringBuilder text, ImmutableArray<ObjectDelegateType> declarations,
        Func<ObjectTypeId, string> symbol, ObjectTypeId objectBase)
    {
        text.Append("""
struct DelegateObject { Object header; Object* receiver = nullptr; std::size_t target = 0; };
static_assert(std::is_standard_layout_v<DelegateObject> && std::is_trivially_destructible_v<DelegateObject> && offsetof(DelegateObject, header) == 0);
static constexpr std::size_t delegate_references[] = {offsetof(DelegateObject, receiver)};
[[gnu::noinline]] static void* delegate_field(Object* value, [[maybe_unused]] std::size_t offset) noexcept {
    return &reinterpret_cast<DelegateObject*>(value)->receiver;
}
struct DelegateTarget;
struct DelegateResult { std::uint32_t status = 0; Object* value = nullptr; const DelegateTarget* target = nullptr; };

""");
        foreach (var declaration in declarations)
            text.Append("static constexpr Descriptor ").Append(symbol(ObjectTypeId.From(declaration.Source)))
                .Append("_descriptor = {sizeof(DelegateObject), alignof(DelegateObject), delegate_references, 1, ")
                .Append("[](Object* p) noexcept { delete reinterpret_cast<DelegateObject*>(p); }, &").Append(symbol(objectBase))
                .Append("_descriptor, nullptr, 0, nullptr, delegate_field};\n");
        text.Append("[[gnu::noinline]] static bool known_delegate(const Descriptor* type) noexcept {\n");
        foreach (var declaration in declarations)
            text.Append("    if (type == &").Append(symbol(ObjectTypeId.From(declaration.Source))).Append("_descriptor) return true;\n");
        text.Append("    return false;\n}\n");
    }

    internal void AppendTargets(StringBuilder text, Func<ObjectTypeId, string> symbol, Func<ObjectTypeId, string> predicate)
    {
        text.Append("""
struct DelegateTarget {
    const Descriptor* type;
    ReferencePredicate receiver;
    std::int32_t (*integer)(Context&, const LocationArgument*) noexcept;
    std::int64_t (*wide_integer)(Context&, const LocationArgument*) noexcept;
    void (*action)(Context&, const LocationArgument*) noexcept;
    Object* (*reference)(Context&, const LocationArgument*) noexcept;
};
""");
        text.Append("static constexpr std::array<DelegateTarget, ").Append(N(_targets.Length)).Append("> delegate_targets = {{\n");
        foreach (var (type, target) in _targets)
        {
            var binding = _bindings[target]; var result = _methods[target].ReturnType.Kind;
            string body = CppEmitter.Symbol(target);
            text.Append("    {&").Append(symbol(type)).Append("_descriptor, ")
                .Append(binding.Kind == ObjectMethodKind.Static ? "nullptr" : predicate(binding.Owner)).Append(", ")
                .Append(result == IrTypeKind.Int32 ? body : "nullptr").Append(", ")
                .Append(result == IrTypeKind.Int64 ? body : "nullptr").Append(", ")
                .Append(result == IrTypeKind.Void ? body : "nullptr").Append(", ")
                .Append(result == IrTypeKind.Reference ? body : "nullptr").Append("},\n");
        }
        text.Append("}};\n").Append(Helpers).Append('\n');
    }

    private const string Helpers = """
// Status 12 is invalid closed-instance binding (null target); 4 is null invocation.
// All managed identities are authenticated before payload access. Target entries
// are compiler-owned, immutable and exact for their delegate signature.
[[maybe_unused, gnu::noinline]] static DelegateResult make_delegate(Context& c, std::size_t index, Object* receiver) noexcept {
    if (c.instance.marking || index >= delegate_targets.size()) return {6};
    const auto& target = delegate_targets[index];
    if (target.receiver != nullptr && receiver == nullptr) return {12};
    if (target.receiver == nullptr ? receiver != nullptr : !location_reference(c.instance, target.receiver, receiver)) return {6};
    Object* value = allocate<DelegateObject>(c, *target.type);
    if (value == nullptr) return {c.pending == 0 ? 5u : c.pending};
    auto* payload = reinterpret_cast<DelegateObject*>(value);
    payload->receiver = receiver; payload->target = index;
    return {0, value, &target};
}
[[maybe_unused, gnu::noinline]] static DelegateResult prepare_delegate(Context& c, Object* value, const Descriptor* expected) noexcept {
    if (c.instance.marking) return {6};
    if (value == nullptr) return {4};
    if (!known_delegate(expected) || !traceable_reference(c.instance, value) || value->type != expected) return {6};
    const auto* payload = reinterpret_cast<DelegateObject*>(value);
    if (payload->target >= delegate_targets.size()) return {6};
    const auto& target = delegate_targets[payload->target];
    if (target.type != expected) return {6};
    if (target.receiver == nullptr ? payload->receiver != nullptr
        : payload->receiver == nullptr || !location_reference(c.instance, target.receiver, payload->receiver)) return {6};
    return {0, value, &target};
}
""";
}
