using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Emits the bounded typed object profile; references never cross its Int32 C boundary.</summary>
public static class ObjectCppEmitter
{
    // Small native helpers also bound unoptimized register spills and sanitizer
    // instrumentation. Inlining them would undo that bound; measure per target.
    private const int StatementsPerStep = 2;
    private const int CopiesPerStep = 4;
    public const string Profile = "object-frames-native-v63";

    public static CppArtifact EmitProgram(ObjectIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ArgumentNullException.ThrowIfNull(program.Flow.Types);
        var plans = ObjectRootAssignment.Assign(program).ToDictionary(p => p.Method);
        return EmitValidated(program, plans, locations: false);
    }

    public static CppArtifact EmitLocations(ScalarLocationIrProgram program)
    {
        var plans = ObjectRootAssignment.AssignLocations(program).ToDictionary(p => p.Method);
        return EmitValidated(program.Flow, plans, locations: true);
    }

    /// <summary>
    /// Emits a scalar program with private boxed-payload runtime helpers for native
    /// conformance probes. This does not lower or execute boxed-value IR operations.
    /// The value catalog must share the program's exact detached reference catalog.
    /// </summary>
    public static CppArtifact EmitBoxedRuntime(ScalarLocationIrProgram program, ValueTypeCatalog values)
    {
        var payloads = ValueLayoutPlanner.Build(values);
        var plans = ObjectRootAssignment.AssignLocations(program).ToDictionary(p => p.Method);
        if (values.References != program.Flow.Flow.Types)
            throw new ArgumentException("Boxed payloads must share the scalar program's reference catalog.", nameof(values));
        return EmitValidated(program.Flow, plans, locations: true, payloads, valueLayouts: values.Types);
    }

    public static CppArtifact EmitBoxes(ScalarBoxedIrProgram program)
    {
        var plans = ObjectRootAssignment.AssignBoxes(program).ToDictionary(p => p.Method);
        return EmitValidated(program.Flow.Flow, plans, locations: true, ValueLayoutPlanner.Build(program.Values), valueLayouts: program.Values.Types);
    }

    /// <summary>Private storage helpers only; this scalar program cannot contain managed handler IR.</summary>
    public static CppArtifact EmitExceptionRuntime(ScalarLocationIrProgram program, ManagedExceptionTypes exceptions)
    {
        var plans = ObjectRootAssignment.AssignLocations(program).ToDictionary(p => p.Method);
        _ = new ManagedExceptionRules(exceptions, program.Flow.Flow.Types);
        if (program.Flow.Bindings.Any(b => exceptions.Types.Contains(b.Owner) && b.Kind != ObjectMethodKind.Static))
            throw new ArgumentException("Intrinsic exception shells cannot have ordinary instance bodies.", nameof(program));
        return EmitValidated(program.Flow, plans, locations: true, exceptions: exceptions);
    }

    public static CppArtifact EmitExceptions(ScalarManagedExceptionIrProgram program)
    {
        var plans = ObjectRootAssignment.AssignExceptions(program).ToDictionary(p => p.Method);
        var boxes = program.Flow.Flow;
        return EmitValidated(boxes.Flow.Flow, plans, locations: true, ValueLayoutPlanner.Build(boxes.Values), program.Exceptions, handlers: true, valueLayouts: boxes.Values.Types);
    }

    public static CppArtifact EmitDelegates(ScalarDelegateIrProgram program)
    {
        var plans = ObjectRootAssignment.AssignDelegates(program).ToDictionary(p => p.Method);
        return EmitValidated(program.Flow.Flow.Flow, plans, locations: true, ValueLayoutPlanner.Build(program.Flow.Values), valueLayouts: program.Flow.Values.Types);
    }

    private static CppArtifact EmitValidated(ObjectIrProgram program, Dictionary<IrMethodId, ObjectMethodRoots> plans, bool locations,
        ImmutableArray<ValueTypePlan> boxedPayloads = default, ManagedExceptionTypes? exceptions = null, bool handlers = false, ImmutableArray<ValueTypeLayout> valueLayouts = default)
    {
        string argumentType = locations ? "LocationArgument" : "Argument";
        var methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        var signatures = methods.ToDictionary(p => p.Key, p => (p.Value.Parameters, p.Value.ReturnType));
        if (program.InterfaceDispatch is { } interfacePlan)
            foreach (var declaration in interfacePlan.Declarations)
                signatures.Add(IrMethodId.From(declaration.Source), (declaration.Parameters, declaration.ReturnType));
        foreach (var declaration in program.AbstractMethods)
            signatures.Add(IrMethodId.From(declaration.Source), (declaration.Parameters, declaration.ReturnType));
        var bindings = program.Bindings.ToDictionary(b => b.Method);
        var native = new ObjectNativeLayout(program.Flow.Types);
        var delegateDeclarations = program.Flow.Types.Delegates;
        var delegates = delegateDeclarations.IsEmpty ? null : new ObjectCppDelegates(program);
        if (boxedPayloads.IsDefault) boxedPayloads = [];
        int fieldSlots = native.Fields.Values.Sum(f => f.Length);
        foreach (var payload in boxedPayloads)
        {
            if (payload.Slots.Length > ObjectNativeLimits.MaximumFieldSlots - fieldSlots)
                throw new ArgumentException("Combined class/boxed native field-slot limit exceeded.", nameof(program));
            fieldSlots += payload.Slots.Length;
        }
        var types = program.Flow.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var fields = program.Flow.Types.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
        var initializers = program.Bindings.Where(b => b.TypeInitializerName is not null).Select((binding, index) => (binding, index)).ToDictionary(p => p.binding.Method);
        var statics = program.Flow.Types.Statics.Select((f, index) => (f, index)).ToDictionary(p => p.f.Id);
        var typeRules = new IrTypeRules(program.Flow.Types);
        var contracts = program.Flow.Types.Interfaces.Select(i => ObjectTypeId.From(i.Source)).ToHashSet();
        var arrays = program.Flow.Types.Arrays;
        var concrete = types.Keys.Concat(delegateDeclarations.Select(d => ObjectTypeId.From(d.Source))).Concat(arrays).Concat(program.Flow.Types.String is { } selectedString ? [selectedString] : Array.Empty<ObjectTypeId>()).ToArray();
        // Empty has a reserved identity even when produced only by Concat(null, null).
        var literals = new Dictionary<string, ImmutableArray<ushort>>(StringComparer.Ordinal) { [""] = [] };
        string LiteralKey(ImmutableArray<ushort> units) => new(units.Select(u => (char)u).ToArray());
        foreach (var literal in methods.Values.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Select(v => v.Expression).OfType<IrExpression.StringLiteral>())
            literals.TryAdd(LiteralKey(literal.CodeUnits), literal.CodeUnits);
        var literalIds = literals.Keys.Select((key, index) => (key, index)).ToDictionary(p => p.key, p => p.index, StringComparer.Ordinal);
        var referenceTypes = concrete.Concat(contracts).ToArray();
        var runtimeOperations = methods.Values.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Select(v => v.Expression).ToArray();
        var runtimeImplementation = runtimeOperations.Select(e => e switch {
            IrExpression.RuntimeTypeLiteral t => t.Implementation, IrExpression.ObjectGetType t => t.Implementation,
            IrExpression.RuntimeTypeHash t => t.Implementation, IrExpression.EnumQuery q => q.Implementation, _ => null }).FirstOrDefault(t => t is not null);
        var runtimeIdentities = (runtimeImplementation is null ? Array.Empty<ObjectTypeId>()
            : referenceTypes.Concat(boxedPayloads.Select(p => p.Type)).Distinct().ToArray())
            .Select((type, index) => (type, index)).ToDictionary(p => p.type, p => p.index);
        string Predicate(ObjectTypeId type) => contracts.Contains(type) ? ContractSymbol(type) : TypeSymbol(type) + "_accepts";
        var boxedTargets = program.InterfaceDispatch?.Methods.Where(m => m.BoxedDeclaration is not null)
            .ToDictionary(m => m.Binding.Method, m => m.Binding.Owner!) ?? [];
        var payloadWidths = boxedPayloads.ToDictionary(p => p.Type, p => p.Slots.Length);
        var boxedDispatchArguments = program.InterfaceDispatch?.Dispatch
            .Where(m => m.Targets.Any(t => boxedTargets.ContainsKey(t.Implementation)))
            .ToDictionary(m => m.Declaration, m => m.Targets.Max(t => methods[t.Implementation].Parameters.Length)) ?? [];
        var layouts = methods.ToDictionary(p => p.Key, p => new ObjectFrameLayout(p.Value, plans[p.Key], exceptions?.Base, boxedDispatchArguments));
        var entry = methods[program.Flow.Entry];
        string symbol = CppEmitter.Symbol(program.Flow.Entry) + "_objects_packed", scope = symbol + "_closure";
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(Profile).Append("\n#include <array>\n#include <atomic>\n#include <bit>\n#include <cstddef>\n#include <cstdint>\n#include <new>\n#include <limits>\n#include <type_traits>\n")
            .Append(ObjectCppRuntime.DiagnosticIncludes).Append("namespace ")
            .Append(scope).Append(" {\nstatic constexpr std::size_t exception_resource_capacity = ")
            .Append((exceptions is null ? 0 : ManagedExceptionMessages.Build(exceptions, program.Flow.Types).Values
                .Where(message => !message.StartsWith("Exception of type '", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Count()).ToString(CultureInfo.InvariantCulture))
            .Append(";\nstatic constexpr std::size_t runtime_type_count = ").Append(N(runtimeIdentities.Count)).Append(";\nstatic constexpr std::size_t static_field_count = ").Append(N(statics.Count)).Append(";\nstatic constexpr std::size_t type_initializer_count = ").Append(N(initializers.Count)).Append(";\n").Append(ObjectCppRuntime.Text).Append('\n').Append(ObjectCppTracing.Text).Append('\n').Append(ObjectCppMarking.Text).Append('\n').Append(ObjectCppHandles.Text).Append('\n').Append(ObjectCppLocations.Text).Append('\n');
        if (exceptions is not null) ObjectCppExceptions.AppendStorage(text);
        if (handlers) text.Append(ObjectCppExceptionFlow.Storage);
        if (program.Flow.Types.Integer64) text.Append(Int64CppLowering.Runtime);
        if (locations) text.Append("struct LocationArgument { std::uint32_t bits = 0; Object* reference = nullptr; Location location; std::uint64_t wide_bits = 0; };\n");
        foreach (var type in referenceTypes)
            text.Append("[[maybe_unused, gnu::noinline]] static bool ").Append(Predicate(type)).Append("(const Descriptor*) noexcept;\n");
        foreach (var composed in native.Fields)
        {
            var layout = types[composed.Key];
            string type = TypeSymbol(composed.Key);
            if (exceptions?.Types.Contains(composed.Key) == true)
            {
                ObjectCppExceptions.AppendType(text, type, TypeSymbol(layout.BaseType!)); continue;
            }
            string Member(ObjectField field) => field.Id.Owner == composed.Key ? FieldSymbol(field.Id)
                : TypeSymbol(field.Id.Owner) + "_" + FieldSymbol(field.Id);
            string Offset(ObjectField field) => "offsetof(" + type + ", " + Member(field) + ")";
            var references = composed.Value.Where(f => f.Storage is ObjectStorage.Reference).ToArray();
            text.Append("struct ").Append(type).Append(" { Object header;\n");
            foreach (var field in composed.Value)
                text.Append("    ").Append(StorageType(field.Storage)).Append(' ').Append(Member(field)).Append(";\n");
            text.Append("};\nstatic_assert(std::is_standard_layout_v<").Append(type).Append("> && std::is_trivially_destructible_v<")
                .Append(type).Append("> && offsetof(").Append(type).Append(", header) == 0);\n")
                .Append("static constexpr std::array<std::size_t, ").Append(N(references.Length)).Append("> ").Append(type).Append("_references = {")
                .Append(string.Join(", ", references.Select(Offset))).Append("};\n")
                .Append("static constexpr std::array<std::size_t, ").Append(N(composed.Value.Length)).Append("> ").Append(type).Append("_fields = {")
                .Append(string.Join(", ", composed.Value.Select(Offset))).Append("};\n");
            // Typed member expressions preserve subobject provenance under C++20;
            // offsets select members, rather than manufacture pointers with byte arithmetic.
            for (int index = 0; index < composed.Value.Length; index++)
                text.Append("[[maybe_unused, gnu::noinline]] static void* ").Append(type).Append("_field_").Append(N(index))
                    .Append("(Object* p) noexcept { return &reinterpret_cast<").Append(type).Append("*>(p)->")
                    .Append(Member(composed.Value[index])).Append("; }\n");
            text.Append("static constexpr std::array<FieldAccessor, ").Append(N(composed.Value.Length)).Append("> ").Append(type).Append("_accessors = {")
                .Append(string.Join(", ", Enumerable.Range(0, composed.Value.Length).Select(i => type + "_field_" + N(i)))).Append("};\n");
            text.Append("static constexpr std::array<FieldStorage, ").Append(N(composed.Value.Length)).Append("> ").Append(type).Append("_storage = {{")
                .Append(string.Join(", ", composed.Value.Select(field => "{StorageKind::" + StorageKind(field.Storage)
                    + ", " + ((field.Attributes & System.Reflection.FieldAttributes.InitOnly) != 0 ? "true" : "false")
                    + ", " + (field.Storage is ObjectStorage.Reference reference ? Predicate(reference.Type) : "nullptr") + "}"))).Append("}};\n")
                .Append("[[maybe_unused]] static constexpr Descriptor ").Append(type).Append("_descriptor = { sizeof(").Append(type).Append("), ")
                .Append("alignof(").Append(type).Append("), ").Append(type).Append("_references.data(), ").Append(N(references.Length))
                .Append(", [](Object* p) noexcept { delete reinterpret_cast<").Append(type).Append("*>(p); }, ")
                .Append(layout.BaseType is null ? "nullptr" : "&" + TypeSymbol(layout.BaseType) + "_descriptor")
                .Append(", ").Append(type).Append("_fields.data(), ").Append(N(composed.Value.Length))
                .Append(", ").Append(type).Append("_accessors.data(), field_by_offset, 0, nullptr, nullptr, nullptr, ").Append(type).Append("_storage.data() };\n");
        }
        if (program.Flow.Types.String is { } stringType)
        {
            text.Append("[[maybe_unused]] static constexpr Descriptor ").Append(TypeSymbol(stringType)).Append("_descriptor = { sizeof(StringObject), alignof(StringObject), nullptr, 0, destroy_string, &")
                .Append(TypeSymbol(program.Flow.Types.ObjectBase)).Append("_descriptor, nullptr, 0, nullptr, nullptr };\n");
            foreach (var literal in literals)
                text.Append("[[maybe_unused]] static constexpr std::array<std::uint16_t, ").Append(N(literal.Value.Length)).Append("> literal_")
                    .Append(N(literalIds[literal.Key])).Append(" = {").Append(string.Join(", ", literal.Value.Select(u => N(u) + "u"))).Append("};\n");
        }
        foreach (var array in arrays)
        {
            bool reference = array.ArrayElement!.Kind == ObjectArrayElementKind.Reference;
            var slots = array.ArrayElement.Kind == ObjectArrayElementKind.Value
                ? program.Flow.Types.ValueArrays.Single(v => v.Type == array.ArrayElement.Type).Slots : [];
            if (!slots.IsEmpty)
            {
                text.Append("static constexpr std::array<ArrayComponent, ").Append(N(slots.Length)).Append("> ").Append(TypeSymbol(array)).Append("_components = {{");
                int integers = 0, wide = 0, references = 0;
                foreach (var slot in slots)
                    text.Append("{StorageKind::").Append(StorageKind(slot)).Append(", ")
                        .Append(N(slot is ObjectStorage.Int32 ? integers++ : slot is ObjectStorage.Int64 ? wide++ : references++))
                        .Append(", ").Append(slot is ObjectStorage.Reference r ? Predicate(r.Type) : "nullptr").Append("},");
                text.Append("}};\n");
            }
            string valueStorage = "3, nullptr, array_reference_count, array_references, nullptr, "
                + N(slots.Count(s => s is ObjectStorage.Int32)) + ", " + N(slots.Count(s => s is ObjectStorage.Int64))
                + ", " + N(slots.Count(s => s is ObjectStorage.Reference)) + ", " + TypeSymbol(array) + "_components.data(), " + N(slots.Length);
            text.Append("[[maybe_unused]] static constexpr Descriptor ").Append(TypeSymbol(array)).Append("_descriptor = { sizeof(ArrayObject), alignof(ArrayObject), nullptr, 0, destroy_array, &")
                .Append(TypeSymbol(program.Flow.Types.ObjectBase)).Append("_descriptor, nullptr, 0, nullptr, nullptr, ")
                .Append(reference ? "2, " + Predicate(array.ArrayElement.Type!) + ", array_reference_count, array_references" : array.ArrayElement.Kind == ObjectArrayElementKind.Value ? valueStorage : array.ArrayElement.Kind == ObjectArrayElementKind.Int64 ? "4, nullptr, nullptr, nullptr, nullptr, 0, 1" : "1, nullptr, nullptr, nullptr").Append(" };\n");
        }
        if (delegates is not null) ObjectCppDelegates.AppendStorage(text, delegateDeclarations, TypeSymbol, program.Flow.Types.ObjectBase);
        // Interface identities are only cast-message keys, never concrete heap
        // descriptors: known_type and allocation tables still exclude them.
        if (handlers && program.Flow.Types.Integer64)
            foreach (var contract in contracts.OrderBy(t => t.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Token).ThenBy(t => t.Specialization, StringComparer.Ordinal))
                text.Append("[[maybe_unused]] static constexpr Descriptor ").Append(TypeSymbol(contract)).Append("_descriptor = {};\n");
        if (!boxedPayloads.IsEmpty)
            ObjectCppBoxing.Append(text, boxedPayloads, TypeSymbol(program.Flow.Types.ObjectBase), Predicate);
        if (exceptions is not null) ObjectCppExceptions.AppendHelpers(text, exceptions, program.Flow.Types, TypeSymbol,
            ManagedExceptionCastMessages.Build(program.Flow.Methods, program.Flow.Types, valueLayouts.IsDefault ? [] : valueLayouts), valueLayouts, program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Any(v => v.Expression is IrExpression.ArrayCopyCompatible));
        var boxedRelations = valueLayouts.IsDefault ? null : new BoxedTypeRelations(program.Flow.Types, valueLayouts);
        // Exact membership compares untrusted descriptor addresses without dereferencing them.
        // This covers nominal/interface ancestry, vector covariance and widening to Object.
        foreach (var expected in referenceTypes)
        {
            string name = Predicate(expected);
            var receivers = concrete.Where(t => typeRules.Assignable(IrType.Reference(t), IrType.Reference(expected))).ToArray();
            var descriptors = receivers.Select(t => "&" + TypeSymbol(t) + "_descriptor").Concat(
                boxedPayloads.Where(p => expected == program.Flow.Types.ObjectBase || boxedRelations?.Accepts(p.Type, expected) == true)
                    .Select(p => "&" + ObjectCppBoxing.Symbol(p.Type) + "_descriptor")).ToArray();
            text.Append("static constexpr std::array<const Descriptor*, ").Append(N(descriptors.Length)).Append("> ")
                .Append(name).Append("_types = {").Append(string.Join(", ", descriptors)).Append("};\n")
                .Append("[[maybe_unused, gnu::noinline]] static bool ").Append(name).Append("(const Descriptor* type) noexcept {\n")
                .Append("    for (const auto* receiver : ").Append(name).Append("_types) if (receiver == type) return true;\n    return false;\n}\n");
        }
        text.Append("[[maybe_unused]] static bool known_type(const Descriptor* type) noexcept { return ")
            .Append(Predicate(program.Flow.Types.ObjectBase)).Append("(type); }\n")
            .Append("[[maybe_unused, gnu::noinline]] static bool array_type(const Descriptor* type) noexcept {\n");
        foreach (var array in arrays)
            text.Append("    if (type == &").Append(TypeSymbol(array)).Append("_descriptor) return true;\n");
        text.Append("    (void)type; return false;\n}\n");
        text.Append("[[maybe_unused]] static bool string_type(const Descriptor* type) noexcept { ")
            .Append(program.Flow.Types.String is { } textType ? "return type == &" + TypeSymbol(textType) + "_descriptor;" : "(void)type; return false;")
            .Append(" }\n");
        text.Append("[[maybe_unused]] static bool runtime_type_storage(const Descriptor* type) noexcept { ")
            .Append(runtimeImplementation is null ? "(void)type; return false;" : "return type == &" + TypeSymbol(runtimeImplementation) + "_descriptor;")
            .Append(" }\n");
        if (runtimeImplementation is not null)
        {
            text.Append("[[maybe_unused]] static std::size_t runtime_type_index(const Descriptor* type) noexcept {\n");
            foreach (var type in concrete)
                text.Append("    if (type == &").Append(TypeSymbol(type)).Append("_descriptor) return ").Append(N(runtimeIdentities[type])).Append("u;\n");
            foreach (var type in boxedPayloads)
                text.Append("    if (type == &").Append(ObjectCppBoxing.Symbol(type.Type)).Append("_descriptor) return ").Append(N(runtimeIdentities[type.Type])).Append("u;\n");
            text.Append("    return runtime_type_count;\n}\n");
        }
        if (runtimeOperations.Any(e => e is IrExpression.EnumEntry)) ObjectCppEnumEntries.Append(text, valueLayouts);
        if (runtimeOperations.Any(e => e is IrExpression.EnumQuery)) ObjectCppEnums.Append(text, valueLayouts, runtimeIdentities);
        foreach (var method in methods.Values.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Source.Token))
        {
            var layout = layouts[IrMethodId.From(method.Source)];
            text.Append("struct ").Append(FrameType(method)).Append(" {\n    Frame header;\n    const ").Append(argumentType).Append("* incoming = nullptr; ").Append(argumentType).Append(" result;\n")
                .Append("    std::array<Object*, ").Append(N(layout.RootCount)).Append("> roots{};\n")
                .Append("    std::array<std::uint32_t, ").Append(N(layout.IntegerCount)).Append("> integers{};\n")
                .Append("    std::array<std::uint64_t, ").Append(N(layout.WideIntegerCount)).Append("> wide_integers{};\n")
                .Append("    std::array<").Append(argumentType).Append(", ").Append(N(layout.CallArgumentCount)).Append("> arguments{};\n")
                .Append("    std::int64_t signed_scratch = 0; std::uint64_t unsigned_scratch = 0;\n");
            if (locations) text.Append("    std::array<Location, ").Append(N(layout.LocationCount)).Append("> locations{};\n")
                .Append("    std::uint64_t borrow_token = 0; LocationResult location_result{};\n");
            if (!boxedPayloads.IsEmpty) text.Append("    std::array<Argument, ").Append(N(layout.BoxArgumentCount))
                .Append("> box_arguments{}; BoxResult box_result{};\n");
            if (layout.HasBoxedDispatch) text.Append("    const Descriptor* dispatch_box_type = nullptr; std::size_t dispatch_payload = 0;\n");
            if (delegates is not null) text.Append("    DelegateResult delegate_result{};\n");
            if (handlers) text.Append("    std::uint32_t active_block = 0; bool exception_routed = false; ExceptionResult exception_result{};\n")
                .Append("    std::array<ExceptionOrigin, ").Append(N(method.ExceptionRegions.Length * 2)).Append("> exception_origins{};\n")
                .Append("    std::array<ExceptionContinuation, ").Append(N(method.ExceptionRegions.Length)).Append("> exception_continuations{};\n");
            foreach (var kind in layout.DispatchKinds.OrderBy(k => k))
                text.Append("    ").Append(CType(new(kind))).Append(" (*dispatch_").Append(kind.ToString().ToLowerInvariant())
                    .Append(")(Context&, const ").Append(argumentType).Append("*) noexcept = nullptr;\n");
            text.Append("};\n");
            if (layout.BorrowsFrame) text.Append("static constexpr std::array<ReferencePredicate, ").Append(N(layout.RootCount)).Append("> ")
                .Append(FrameType(method)).Append("_root_types = {")
                .Append(string.Join(", ", layout.RootTypes.Select(t => Predicate(t.Kind == IrTypeKind.Reference ? t.Class! : program.Flow.Types.ObjectBase)))).Append("};\n");
            Header(method); text.Append(";\n");
        }
        if (initializers.Count != 0) {
            text.Append(ObjectCppInitialization.Text.Replace("$ARGUMENT", argumentType, StringComparison.Ordinal));
            foreach (var initializer in initializers.Values)
                text.Append("static constexpr std::uint16_t initialization_name_").Append(N(initializer.index)).Append("[] = {")
                    .Append(string.Join(", ", initializer.binding.TypeInitializerName!.Select(ch => N(ch)))).Append("};\n");
        }
        delegates?.AppendTargets(text, TypeSymbol, Predicate);
        if (boxedTargets.Count != 0) text.Append(ObjectCppBoxedDispatch.Text);
        var dispatch = program.Dispatch.Concat(program.InterfaceDispatch is { } prepared
            ? prepared.Dispatch.Select(m => new ObjectVirtualDispatch(m.Declaration,
                [.. m.Targets.Select(t => new ObjectVirtualTarget(t.Receiver, t.Implementation))])) : []);
        foreach (var map in dispatch)
        {
            string name = DispatchSymbol(map.Declaration);
            bool boxedMap = boxedDispatchArguments.ContainsKey(map.Declaration);
            text.Append("using ").Append(name).Append("_body = ").Append(CType(signatures[map.Declaration].ReturnType))
                .Append(" (*)(Context&, const ").Append(argumentType).Append("*) noexcept;\nstruct ").Append(name)
                .Append("_entry { const Descriptor* type; ").Append(name).Append("_body body;")
                .Append(boxedMap ? " bool boxed; std::size_t payload;" : "").Append(" };\n")
                .Append("static constexpr std::array<").Append(name).Append("_entry, ").Append(N(map.Targets.Length)).Append("> ")
                .Append(name).Append("_table = {{\n");
            foreach (var target in map.Targets)
            {
                bool boxed = boxedTargets.ContainsKey(target.Implementation);
                text.Append("    { &").Append(boxed ? ObjectCppBoxing.Symbol(target.Receiver) : TypeSymbol(target.Receiver)).Append("_descriptor, ")
                    .Append(CppEmitter.Symbol(target.Implementation));
                if (boxedMap) text.Append(boxed ? ", true, " + N(payloadWidths[target.Receiver]) : ", false, 0");
                text.Append(" },\n");
            }
            text.Append("}};\n[[maybe_unused, gnu::noinline]] static ").Append(name).Append("_body ").Append(name)
                .Append("(const Descriptor* type").Append(boxedMap ? ", const Descriptor*& boxed, std::size_t& payload" : "")
                .Append(") noexcept {\n").Append(boxedMap ? "    boxed = nullptr; payload = 0;\n" : "")
                .Append("    for (const auto& entry : ").Append(name).Append("_table) if (entry.type == type) { ")
                .Append(boxedMap ? "boxed = entry.boxed ? entry.type : nullptr; payload = entry.payload; " : "")
                .Append("return entry.body; }\n    return nullptr;\n}\n");
        }
        foreach (var method in methods.Values.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Source.Token)) AppendMethod(method);
        text.Append("}\n// Private experimental boundary; valid disjoint outputs, references remain internal.\n")
            .Append("// Parameters must point to count readable Int32 values; exact arity required, null allowed only for zero arity.\n")
            .Append("// 0 success, 1 divide-by-zero, 2 overflow, 3 invalid boundary arguments, 4 null, 5 allocation, 6 ownership/type, 7 invalid cast, 8 call-depth, 9 execution storage, 10 index bounds, 11 array type mismatch.\n")
            .Append("// Result unchanged on failure; origin text borrows the generated library lifetime.\n")
            .Append("extern \"C\" std::uint32_t ").Append(symbol).Append("_try(std::int32_t* result, std::uint32_t* method, std::uint32_t* il, const char** image, const std::int32_t* parameters, std::size_t count) noexcept {\n")
            .Append("    if (result == nullptr || method == nullptr || il == nullptr || image == nullptr || count != ").Append(N(entry.Parameters.Length))
            .Append("u || (count != 0 && parameters == nullptr)) return 3u;\n    using namespace ")
            .Append(scope).Append(";\n    Instance heap;\n    Context context(heap);\n    std::int32_t value = 0;\n    {\n")
            .Append("    Storage<std::array<").Append(argumentType).Append(", ").Append(N(entry.Parameters.Length)).Append(">> arguments(context);\n")
            .Append("    if (arguments.value == nullptr) { context.pending = 9u; context.image = \"").Append(entry.Source.ContentHash.ToLowerInvariant())
            .Append("\"; context.method = ").Append(N(entry.Source.Token)).Append("u; context.il = ").Append(N(entry.Blocks[0].ILOffset)).Append("u; } else {\n");
        text.Append("    for (std::size_t i = 0; i < count; ++i) (*arguments.value)[i].bits = std::bit_cast<std::uint32_t>(parameters[i]);\n");
        text.Append("    value = ").Append(CppEmitter.Symbol(program.Flow.Entry)).Append("(context, arguments.value->data());\n    }\n    }\n")
            .Append("    if (context.pending == 0u) *result = value;\n    *method = context.method; *il = context.il; *image = context.image;\n    return context.pending;\n}\n");
        return new(entry.Source, symbol + "_try", text.ToString()) { FaultEntryPoint = symbol + "_try", MayThrow = true };

        void Header(IrMethod method)
        {
            text.Append("[[maybe_unused]] static ").Append(CType(method.ReturnType)).Append(' ').Append(CppEmitter.Symbol(IrMethodId.From(method.Source)))
                .Append("([[maybe_unused]] Context& context, [[maybe_unused]] const ").Append(argumentType).Append("* arguments) noexcept");
        }

        string FrameType(IrMethod method) => CppEmitter.Symbol(IrMethodId.From(method.Source)) + "_storage";

        void AppendMethod(IrMethod method)
        {
            var layout = layouts[IrMethodId.From(method.Source)];
            int currentBlock = 0;
            var exceptionRules = handlers ? new ManagedExceptionRules(exceptions!, program.Flow.Types) : null;
            var output = text;
            var code = new StringBuilder();
            var steps = new List<string>();
            var labels = new Dictionary<string, int>();
            void Flush(bool terminal = false)
            {
                if (code.Length == 0) return;
                if (!terminal) code.Append("    return ").Append(N(steps.Count + 1)).Append("u;\n");
                steps.Add(code.ToString()); code.Clear();
            }
            void Label(string label) { Flush(); labels.Add(label, steps.Count); }
            bool inStep = true;
            string Value(int id) => layout.Storage(ObjectRootKind.Value, id, currentBlock);
            static string PacketMember(IrType type) => type.Kind switch
            {
                IrTypeKind.Int32 => "bits", IrTypeKind.Int64 => "wide_bits", IrTypeKind.ManagedLocation => "location", _ => "reference",
            };
            string Exit() => inStep ? "return frame_done;" : method.ReturnType.Kind switch { IrTypeKind.Void => "return;", IrTypeKind.Reference => "return nullptr;", _ => "return 0;" };
            void Fault(string status, int offset)
            {
                code.Append("        context.pending = ").Append(status).Append("; context.image = \"").Append(method.Source.ContentHash.ToLowerInvariant())
                    .Append("\"; context.method = 0x").Append(method.Source.Token.ToString("x8", CultureInfo.InvariantCulture)).Append("u; context.il = ")
                    .Append(N(offset)).Append("u; ").Append(Exit()).Append('\n');
            }
            void Guard(string condition, string status, int offset)
            {
                code.Append("    if (").Append(condition).Append(") {\n"); Fault(status, offset); code.Append("    }\n");
            }
            void CheckLocationResult(int offset) => Guard("f->location_result.status != LocationStatus::complete",
                "f->location_result.status == LocationStatus::null_reference ? 4u : f->location_result.status == LocationStatus::index_bounds ? 10u : f->location_result.status == LocationStatus::array_type_mismatch ? 11u : 6u", offset);
            void CastFault(string condition, string receiver, string target, int offset, bool nullableTarget = false, bool dynamicTarget = false)
            {
                code.Append("    if (").Append(condition).Append(") {\n        raise_cast_exception(context, ").Append(receiver).Append(", ")
                    .Append(dynamicTarget ? target : "&" + target + "_descriptor").Append(", {7u, \"").Append(method.Source.ContentHash).Append("\", ")
                    .Append(N(method.Source.Token)).Append("u, ").Append(N(offset)).Append("u}, ").Append(nullableTarget ? "true" : "false").Append(");\n        ").Append(Exit()).Append("\n    }\n");
            }
            void CheckBoxResult(int offset) => Guard("f->box_result.status != BoxStatus::complete",
                "context.pending != 0u ? context.pending : f->box_result.status == BoxStatus::null_reference ? 4u : "
                    + "f->box_result.status == BoxStatus::wrong_type ? 7u : f->box_result.status == BoxStatus::allocation ? 5u : 6u", offset);
            void PublishLocation(ObjectRootKind kind, int index, IrType type, int block = -1)
            {
                string storage = layout.Storage(kind, index, block);
                if (type.ReadOnly) code.Append("    ").Append(storage).Append(".read_only = true;\n");
                code.Append("    ").Append(layout.Owner(kind, index, block)).Append(" = ").Append(storage).Append(".owner;\n");
            }
            string Matches(string value, ObjectTypeId type) => Predicate(type) + "(" + value + "->type)";
            string TypeTestMatch(string value, ObjectTypeId type, bool boxed) => boxed
                ? "(" + value + "->type == &" + ObjectCppBoxing.Symbol(type) + "_descriptor)" : Matches(value, type);
            void Check(string value, ObjectTypeId type, bool nullCheck, int offset)
            {
                if (nullCheck) Guard(value + " == nullptr", "4u", offset);
                Guard("!context.instance.owns(" + value + ")", "6u", offset);
                if (type != program.Flow.Types.ObjectBase)
                    Guard(value + " != nullptr && !" + Matches(value, type), "6u", offset);
            }
            void ArrayCheck(int receiver, ObjectTypeId? view, int? index, int offset)
            {
                string value = Value(receiver);
                Guard(value + " == nullptr", "4u", offset);
                Guard("!context.instance.owns(" + value + ")", "6u", offset);
                Guard("!" + (view is null ? "array_type(" + value + "->type)" : Matches(value, view)), "6u", offset);
                Flush();
                if (index is { } element)
                {
                    Guard(Value(element) + " >= array_length(" + value + ")", "10u", offset);
                    Flush();
                }
            }
            string Call(IrMethodId target, ImmutableArray<int> args, string? receiver = null, bool dispatch = false, int offset = 0)
            {
                var parameters = signatures[target].Parameters;
                int start = receiver is null ? 0 : 1;
                if (receiver is not null) code.Append("    f->arguments[0].reference = ").Append(receiver).Append(";\n");
                for (int i = 0; i < args.Length; i++)
                {
                    code.Append("    f->arguments[").Append(N(i + start)).Append("].").Append(PacketMember(parameters[i + start])).Append(" = ")
                        .Append(Value(args[i])).Append(";\n");
                    if ((i + 1) % CopiesPerStep == 0) Flush();
                }
                Flush();
                if (dispatch && boxedDispatchArguments.ContainsKey(target))
                {
                    code.Append("    f->unsigned_scratch = adapt_boxed_call(context, f->arguments.data(), ").Append(N(args.Length))
                        .Append(", f->arguments.size(), f->dispatch_box_type, f->dispatch_payload);\n");
                    Guard("f->unsigned_scratch != 0u", "static_cast<std::uint32_t>(f->unsigned_scratch)", offset); Flush();
                }
                return (dispatch ? DispatchPointer(signatures[target].ReturnType.Kind) : CppEmitter.Symbol(target)) + "(context, f->arguments.data())";
            }
            string DelegateCall(ObjectTypeId type, int receiver, ImmutableArray<int> arguments, int offset)
            {
                var declaration = delegateDeclarations.Single(d => ObjectTypeId.From(d.Source) == type);
                code.Append("    f->delegate_result = prepare_delegate(context, ").Append(Value(receiver)).Append(", &")
                    .Append(TypeSymbol(type)).Append("_descriptor);\n");
                Guard("f->delegate_result.status != 0u", "f->delegate_result.status", offset); Flush();
                code.Append("    f->arguments[0].reference = reinterpret_cast<DelegateObject*>(f->delegate_result.value)->receiver;\n");
                for (int i = 0; i < arguments.Length; ++i)
                {
                    code.Append("    f->arguments[").Append(N(i + 1)).Append("].").Append(PacketMember(declaration.Parameters[i])).Append(" = ").Append(Value(arguments[i])).Append(";\n");
                    if ((i + 1) % CopiesPerStep == 0) Flush();
                }
                Flush();
                var result = declaration.ReturnType.Kind;
                return "f->delegate_result.target->" + (result == IrTypeKind.Void ? "action" : result == IrTypeKind.Reference ? "reference" : "integer")
                    + "(context, f->arguments.data() + (f->delegate_result.target->receiver == nullptr ? 1 : 0))";
            }
            string DispatchPointer(IrTypeKind kind) => "f->dispatch_" + kind.ToString().ToLowerInvariant();
            void SelectDispatch(IrMethodId target, ImmutableArray<int> arguments, int offset)
            {
                Check(Value(arguments[0]), signatures[target].Parameters[0].Class!, true, offset); Flush();
                string pointer = DispatchPointer(signatures[target].ReturnType.Kind);
                code.Append("    ").Append(pointer).Append(" = ").Append(DispatchSymbol(target)).Append('(')
                    .Append(Value(arguments[0])).Append("->type")
                    .Append(boxedDispatchArguments.ContainsKey(target) ? ", f->dispatch_box_type, f->dispatch_payload" : "").Append(");\n");
                Guard(pointer + " == nullptr", "6u", offset); Flush();
            }
            void Pending() => code.Append("    if (context.pending != 0u) ").Append(Exit()).Append('\n');
            string Field(ObjectFieldId field, int receiver)
            {
                string value = Value(receiver);
                string storage = StorageType(fields[field].Storage);
                return "*static_cast<" + storage + "*>(field_address(" + value + ", " + N(native.Indices[field]) + "))";
            }
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                code.Append("    ").Append(layout.Storage(ObjectRootKind.Parameter, i)).Append(" = f->incoming[").Append(N(i))
                    .Append("].").Append(PacketMember(method.Parameters[i])).Append(";\n");
                if (method.Parameters[i].Kind == IrTypeKind.ManagedLocation) PublishLocation(ObjectRootKind.Parameter, i, method.Parameters[i]);
                if ((i + 1) % CopiesPerStep == 0) Flush();
            }
            Flush();
            for (int i = 0; i < method.Parameters.Length; i++)
                if (method.Parameters[i].Kind == IrTypeKind.Reference)
                {
                    Check(layout.Storage(ObjectRootKind.Parameter, i), method.Parameters[i].Class!, false, method.Blocks[0].ILOffset);
                    Flush();
                }
                else if (method.Parameters[i].Kind == IrTypeKind.ManagedLocation)
                {
                    var type = method.Parameters[i]; string storage = layout.Storage(ObjectRootKind.Parameter, i);
                    Guard("!valid_location_argument(context, " + storage + ", "
                        + "StorageKind::" + LocationKind(type.Element!) + ", " + (type.Element!.Kind == IrTypeKind.Reference ? Predicate(type.Element.Class!) : "nullptr")
                        + ", " + (type.ReadOnly ? "true" : "false") + ")", "6u", method.Blocks[0].ILOffset);
                    Flush();
                }
            foreach (var block in method.Blocks)
            {
                currentBlock = block.Id;
                var values = block.Values.ToDictionary(v => v.Id);
                Label("b" + N(block.Id));
                if (handlers) code.Append("    f->active_block = ").Append(N(block.Id)).Append(";\n");
                int statementsInStep = 0;
                foreach (var statement in block.Statements)
                {
                    bool isolatedOperation = statement is IrStatement.StoreField or IrStatement.StoreElement or IrStatement.StoreLocation or IrStatement.InitializeLocation
                        or IrStatement.Define { Value.Expression: IrExpression.LoadStatic or IrExpression.EmptyArray or IrExpression.NewArray or IrExpression.ArrayLength or IrExpression.ArrayCopyCompatible or IrExpression.LoadElement or IrExpression.LoadField or IrExpression.CastClass or IrExpression.IsInstance
                            or IrExpression.AddressLocal or IrExpression.AddressParameter or IrExpression.AddressField or IrExpression.AddressArraySlot or IrExpression.LoadLocation
                            or IrExpression.NewException or IrExpression.ExceptionMessage or IrExpression.ExceptionInnerException or IrExpression.ExceptionTypeName or IrExpression.ExceptionParameterName or IrExpression.BoxScalars or IrExpression.CheckBox or IrExpression.AddressBoxSlot or IrExpression.NewDelegate or IrExpression.CallDelegate
                            or IrExpression.StringLiteral or IrExpression.InvariantCulture or IrExpression.RuntimeTypeLiteral or IrExpression.ObjectGetType or IrExpression.RuntimeTypeHash or IrExpression.ObjectIdentityHash or IrExpression.EnumEntry or IrExpression.EnumQuery or IrExpression.IntegerTextPublish or IrExpression.IntegerTextFind or IrExpression.StringLength or IrExpression.StringCodeUnit or IrExpression.StringEquals or IrExpression.StringFromCodeUnits or IrExpression.StringConcat
                            or IrExpression.Call { VirtualDispatch: true } }
                        or IrStatement.InitializeType or IrStatement.StoreStatic or IrStatement.Invoke { VirtualDispatch: true } or IrStatement.InvokeDelegate;
                    // Array/field access, type tests and virtual calls include ownership/type checks.
                    // Keep them in separate steps to retain the O0 sanitizer stack bound.
                    if (isolatedOperation) { Flush(); statementsInStep = 0; }
                    switch (statement)
                    {
                        case IrStatement.Define define:
                        {
                            var value = define.Value; string expression;
                            code.Append("    // IL_").Append(value.ILOffset.ToString("x4", CultureInfo.InvariantCulture)).Append('\n');
                            switch (value.Expression)
                            {
                                case IrExpression.NewException creation when creation.TypeName is int initializedTypeName:
                                    code.Append("    f->exception_result = make_initialization_exception(context, &").Append(TypeSymbol(creation.Type)).Append("_descriptor, ")
                                        .Append(Value(initializedTypeName)).Append(", ").Append(creation.Inner is int suppliedInner ? Value(suppliedInner) : "nullptr").Append(");\n");
                                    Guard("f->exception_result.status != 0u", "f->exception_result.status", value.ILOffset);
                                    expression = "f->exception_result.value"; break;
                                case IrExpression.NewException creation:
                                    code.Append("    f->exception_result = make_exception(context, &").Append(TypeSymbol(creation.Type)).Append("_descriptor, ")
                                        .Append(creation.Message is int messageValue ? Value(messageValue) : "nullptr").Append(creation.TypeName is not null ? ", 3u, " : creation.Message is null ? ", 1u, " : ", 0u, ")
                                        .Append(creation.ParameterName is int parameterValue ? Value(parameterValue) : "nullptr").Append(", ")
                                        .Append(creation.Inner is int innerValue ? Value(innerValue) : "nullptr").Append(", ")
                                        .Append(creation.TypeName is int nameValue ? Value(nameValue) : "nullptr").Append(", ")
                                        .Append(creation.ActualValue is int actualValue ? Value(actualValue) : "nullptr").Append(");\n");
                                    Guard("f->exception_result.status != 0u", "f->exception_result.status", value.ILOffset);
                                    expression = "f->exception_result.value"; break;
                                case IrExpression.ExceptionInnerException inner:
                                    code.Append("    f->exception_result = exception_inner(context, ").Append(Value(inner.Operand)).Append(");\n");
                                    Guard("f->exception_result.status != 0u", "f->exception_result.status", value.ILOffset);
                                    expression = "f->exception_result.value"; break;
                                case IrExpression.ExceptionTypeName name:
                                    code.Append("    f->exception_result = exception_type_name(context, ").Append(Value(name.Operand)).Append(");\n");
                                    Guard("f->exception_result.status != 0u", "f->exception_result.status", value.ILOffset);
                                    expression = "f->exception_result.value"; break;
                                case IrExpression.ExceptionParameterName parameter:
                                    code.Append("    f->exception_result = exception_parameter_name(context, ").Append(Value(parameter.Operand)).Append(");\n");
                                    Guard("f->exception_result.status != 0u", "f->exception_result.status", value.ILOffset);
                                    expression = "f->exception_result.value"; break;
                                case IrExpression.ExceptionMessage message:
                                    code.Append("    f->exception_result = exception_message(context, ").Append(Value(message.Operand)).Append(");\n");
                                    Guard("f->exception_result.status != 0u", "f->exception_result.status", value.ILOffset);
                                    expression = "f->exception_result.value"; break;
                                case IrExpression.NewDelegate creation:
                                    code.Append("    f->delegate_result = make_delegate(context, ").Append(N(delegates!.Index(creation)))
                                        .Append(", ").Append(Value(creation.Receiver)).Append(");\n");
                                    Guard("f->delegate_result.status != 0u", "f->delegate_result.status", value.ILOffset);
                                    expression = "f->delegate_result.value"; break;
                                case IrExpression.CallDelegate call:
                                    expression = DelegateCall(call.Type, call.Delegate, call.Arguments, value.ILOffset);
                                    if (value.Type == IrType.Int32) expression = "std::bit_cast<std::uint32_t>(" + expression + ")";
                                    break;
                                case IrExpression.BoxScalars boxed:
                                    for (int slot = 0; slot < boxed.Values.Length; ++slot)
                                    {
                                        code.Append("    f->box_arguments[").Append(N(slot)).Append("].")
                                            .Append(PacketMember(values[boxed.Values[slot]].Type)).Append(" = ").Append(Value(boxed.Values[slot])).Append(";\n");
                                        if ((slot + 1) % CopiesPerStep == 0) Flush();
                                    }
                                    Flush();
                                    code.Append("    f->box_result = box_value(context, &").Append(ObjectCppBoxing.Symbol(boxed.Type))
                                        .Append("_descriptor, f->box_arguments.data(), ").Append(N(boxed.Values.Length)).Append(");\n");
                                    CheckBoxResult(value.ILOffset); expression = "f->box_result.value"; break;
                                case IrExpression.CheckBox boxed:
                                    code.Append("    f->box_result = unbox(context, ").Append(Value(boxed.Operand)).Append(", &")
                                        .Append(ObjectCppBoxing.Symbol(boxed.Type)).Append("_descriptor, ").Append(boxed.NullableTarget ? "true" : "false").Append(");\n");
                                    if (handlers) CastFault("f->box_result.status == BoxStatus::wrong_type", Value(boxed.Operand), ObjectCppBoxing.Symbol(boxed.Type), value.ILOffset, boxed.NullableTarget);
                                    CheckBoxResult(value.ILOffset); expression = "f->box_result.value"; break;
                                case IrExpression.AddressBoxSlot slot:
                                    code.Append("    f->box_result = unbox(context, ").Append(Value(slot.Receiver)).Append(", &")
                                        .Append(ObjectCppBoxing.Symbol(slot.Type)).Append("_descriptor);\n");
                                    if (handlers) CastFault("f->box_result.status == BoxStatus::wrong_type", Value(slot.Receiver), ObjectCppBoxing.Symbol(slot.Type), value.ILOffset);
                                    CheckBoxResult(value.ILOffset); Flush();
                                    code.Append("    f->location_result = heap_location(context, f->box_result.value, f->box_result.value->type, ").Append(N(slot.Slot)).Append(");\n");
                                    CheckLocationResult(value.ILOffset); expression = "f->location_result.value"; break;
                                case IrExpression.AddressLocal local:
                                    code.Append("    f->location_result = frame_location(context, f->borrow_token, LocationOrigin::")
                                        .Append("frame_" + LocationKind(value.Type.Element!)).Append(", ")
                                        .Append(N(layout.Index(ObjectRootKind.Local, local.Index))).Append(");\n");
                                    CheckLocationResult(value.ILOffset); expression = "f->location_result.value"; break;
                                case IrExpression.AddressParameter parameter:
                                    code.Append("    f->location_result = frame_location(context, f->borrow_token, LocationOrigin::")
                                        .Append("frame_" + LocationKind(value.Type.Element!)).Append(", ")
                                        .Append(N(layout.Index(ObjectRootKind.Parameter, parameter.Index))).Append(");\n");
                                    CheckLocationResult(value.ILOffset); expression = "f->location_result.value"; break;
                                case IrExpression.AddressArraySlot element:
                                    code.Append("    f->location_result = array_location(context, ").Append(Value(element.Array)).Append(", &")
                                        .Append(TypeSymbol(element.ArrayType)).Append("_descriptor, ").Append(Value(element.Index)).Append(", ")
                                        .Append(N(element.Slot)).Append(");\n");
                                    CheckLocationResult(value.ILOffset); expression = "f->location_result.value"; break;
                                case IrExpression.AddressField field:
                                    code.Append("    f->location_result = heap_location(context, ").Append(Value(field.Receiver)).Append(", &")
                                        .Append(TypeSymbol(field.Field.Owner)).Append("_descriptor, ").Append(N(native.Indices[field.Field])).Append(");\n");
                                    CheckLocationResult(value.ILOffset); expression = "f->location_result.value"; break;
                                case IrExpression.LoadLocation load:
                                    Guard("load_location_" + LocationKind(value.Type) + "(context, "
                                        + Value(load.Location) + ", " + Value(value.Id) + ") != LocationStatus::complete", "6u", value.ILOffset);
                                    expression = Value(value.Id); break;
                                case IrExpression.ReadOnlyLocation view:
                                    expression = Value(view.Location); break;
                                case IrExpression.IntegerTextFind cached:
                                    expression = "integer_text_find(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " + Value(cached.Index) + ")"; break;
                                case IrExpression.IntegerTextPublish cached:
                                    Check(Value(cached.Text), program.Flow.Types.String!, true, value.ILOffset); Flush();
                                    expression = "integer_text_publish(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " + Value(cached.Index) + ", " + Value(cached.Text) + ")"; break;
                                case IrExpression.RuntimeTypeLiteral literalType:
                                    string literalImplementation = TypeSymbol(literalType.Implementation);
                                    expression = "runtime_type<" + literalImplementation + ">(context, " + literalImplementation + "_descriptor, " + N(runtimeIdentities[literalType.Target]) + "u)"; break;
                                case IrExpression.ObjectGetType objectType:
                                    Check(Value(objectType.Operand), program.Flow.Types.ObjectBase, true, value.ILOffset); Flush();
                                    string objectImplementation = TypeSymbol(objectType.Implementation);
                                    expression = "runtime_type<" + objectImplementation + ">(context, " + objectImplementation + "_descriptor, runtime_type_index(" + Value(objectType.Operand) + "->type))"; break;
                                case IrExpression.EnumEntry enumEntry:
                                    expression = (enumEntry.Name ? "enum_entry_name(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " : "enum_entry_value(context, ")
                                        + ObjectCppEnumEntries.Symbol(enumEntry.EnumType) + ".data(), " + ObjectCppEnumEntries.Symbol(enumEntry.EnumType) + ".size(), " + Value(enumEntry.Index) + ")"; break;
                                case IrExpression.EnumQuery query:
                                    Check(Value(query.Type), query.Implementation, true, value.ILOffset); Flush();
                                    Check(Value(query.Value), program.Flow.Types.ObjectBase, true, value.ILOffset); Flush();
                                    expression = query.Message ? "enum_error_message(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " + Value(query.Type) + ", " + Value(query.Value) + ")"
                                        : "enum_query(context, " + Value(query.Type) + ", " + Value(query.Value) + ")"; break;
                                case IrExpression.ObjectIdentityHash identityHash:
                                    Check(Value(identityHash.Operand), program.Flow.Types.ObjectBase, true, value.ILOffset); Flush();
                                    expression = "static_cast<std::uint32_t>(reinterpret_cast<std::uintptr_t>(" + Value(identityHash.Operand) + ") / alignof(Object))"; break;
                                case IrExpression.RuntimeTypeHash typeHash:
                                    Check(Value(typeHash.Operand), typeHash.Implementation, true, value.ILOffset); Flush();
                                    expression = "runtime_type_hash(context, " + Value(typeHash.Operand) + ")"; break;
                                case IrExpression.InvariantCulture culture:
                                    string cultureType = TypeSymbol(culture.Type);
                                    expression = "invariant_culture<" + cultureType + ">(context, " + cultureType + "_descriptor)"; break;
                                case IrExpression.StringLiteral literal:
                                    int literalId = literalIds[LiteralKey(literal.CodeUnits)];
                                    expression = "string_literal(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " + N(literalId)
                                        + ", literal_" + N(literalId) + ".data(), " + N(literal.CodeUnits.Length) + "u)"; break;
                                case IrExpression.StringLength length:
                                    Check(Value(length.Operand), program.Flow.Types.String!, true, value.ILOffset); Flush();
                                    expression = "string_length(" + Value(length.Operand) + ")"; break;
                                case IrExpression.StringCodeUnit unit:
                                    Check(Value(unit.Operand), program.Flow.Types.String!, true, value.ILOffset); Flush();
                                    Guard(Value(unit.Index) + " >= string_length(" + Value(unit.Operand) + ")", "10u", value.ILOffset); Flush();
                                    expression = "string_code_unit(" + Value(unit.Operand) + ", " + Value(unit.Index) + ")"; break;
                                case IrExpression.StringEquals equal:
                                    Check(Value(equal.Left), program.Flow.Types.String!, false, value.ILOffset); Flush();
                                    Check(Value(equal.Right), program.Flow.Types.String!, false, value.ILOffset); Flush();
                                    expression = "string_equals(" + Value(equal.Left) + ", " + Value(equal.Right) + ")"; break;
                                case IrExpression.LoadStatic loaded:
                                    Guard("!ensure_static_storage(context)", "context.pending == 0u ? 5u : context.pending", value.ILOffset); Flush();
                                    expression = "context.instance.statics[" + N(statics[loaded.Field].index) + "]." + (value.Type == IrType.Int32 ? "bits" : value.Type == IrType.Int64 ? "wide" : "reference"); break;
                                case IrExpression.EmptyArray empty:
                                    expression = "empty_array(context, " + TypeSymbol(empty.ArrayType) + "_descriptor)"; break;
                                case IrExpression.StringFromCodeUnits units:
                                    ArrayCheck(units.Array, ObjectTypeId.Vector(program.Flow.Types.ObjectBase, ObjectArrayElement.Int32), null, value.ILOffset);
                                    expression = "string_from_code_units(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " + Value(units.Array) + ")"; break;
                                case IrExpression.StringConcat concat:
                                    Check(Value(concat.Left), program.Flow.Types.String!, false, value.ILOffset); Flush();
                                    Check(Value(concat.Right), program.Flow.Types.String!, false, value.ILOffset); Flush();
                                    expression = "string_concat(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " + Value(concat.Left) + ", " + Value(concat.Right) + ")"; break;
                                case IrExpression.NewObject allocation:
                                    string type = TypeSymbol(bindings[allocation.Constructor].Owner);
                                    expression = "allocate<" + type + ">(context, " + type + "_descriptor)"; break;
                                case IrExpression.NewArray array:
                                    expression = "allocate_array(context, " + TypeSymbol(array.ArrayType) + "_descriptor, " + Value(array.Length) + ")"; break;
                                case IrExpression.ArrayCopyCompatible copy:
                                    ArrayCheck(copy.Source, copy.ArrayType, null, value.ILOffset);
                                    ArrayCheck(copy.Destination, copy.ArrayType, null, value.ILOffset);
                                    expression = "copy_arrays_compatible(" + Value(copy.Source) + "->type, " + Value(copy.Destination) + "->type) ? 1u : 0u"; break;
                                case IrExpression.ArrayLength length:
                                    ArrayCheck(length.Array, null, null, value.ILOffset);
                                    expression = "array_length(" + Value(length.Array) + ")"; break;
                                case IrExpression.LoadElement element:
                                    ArrayCheck(element.Array, element.ArrayType, element.Index, value.ILOffset);
                                    expression = (value.Type.Kind == IrTypeKind.Int32 ? "load_integer_element(" : value.Type.Kind == IrTypeKind.Int64 ? "load_wide_element(" : "load_reference_element(")
                                        + Value(element.Array) + ", " + Value(element.Index) + ")"; break;
                                case IrExpression.LoadField field:
                                    Check(Value(field.Receiver), field.Field.Owner, true, value.ILOffset);
                                    expression = Field(field.Field, field.Receiver); break;
                                case IrExpression.CastClass cast:
                                    Guard("!context.instance.owns(" + Value(cast.Operand) + ")", "6u", value.ILOffset);
                                    string castMismatch = Value(cast.Operand) + " != nullptr && !" + TypeTestMatch(Value(cast.Operand), cast.Target, cast.BoxedTarget);
                                    if (handlers) CastFault(castMismatch, Value(cast.Operand), cast.BoxedTarget ? ObjectCppBoxing.Symbol(cast.Target) : TypeSymbol(cast.Target), value.ILOffset);
                                    else Guard(castMismatch, "7u", value.ILOffset);
                                    expression = Value(cast.Operand); break;
                                case IrExpression.IsInstance test:
                                    Guard("!context.instance.owns(" + Value(test.Operand) + ")", "6u", value.ILOffset);
                                    expression = Value(test.Operand) + " != nullptr && " + TypeTestMatch(Value(test.Operand), test.Target, test.BoxedTarget)
                                        + " ? " + Value(test.Operand) + " : nullptr"; break;
                                case IrExpression.Null: expression = "nullptr"; break;
                                case IrExpression.Parameter p: expression = layout.Storage(ObjectRootKind.Parameter, p.Index); break;
                                case IrExpression.Local l: expression = layout.Storage(ObjectRootKind.Local, l.Index); break;
                                case IrExpression.StackInput input: expression = layout.Storage(ObjectRootKind.BlockInput, input.Index, block.Id); break;
                                case IrExpression.Call call:
                                    if (call.VirtualDispatch) SelectDispatch(call.Target, call.Arguments, value.ILOffset);
                                    else if (call.NullCheckReceiver) Guard(Value(call.Arguments[0]) + " == nullptr", "4u", value.ILOffset);
                                    expression = Call(call.Target, call.Arguments, dispatch: call.VirtualDispatch, offset: value.ILOffset);
                                    if (value.Type.Kind is IrTypeKind.Int32 or IrTypeKind.Int64)
                                        expression = "std::bit_cast<" + (value.Type.Kind == IrTypeKind.Int64 ? "std::uint64_t" : "std::uint32_t") + ">(" + expression + ")";
                                    break;
                                default:
                                    expression = CppEmitter.AppendValuePreparation(code, value, block.Id, (fault, offset) => Fault(N((int)fault) + "u", offset),
                                        Value, signed => signed ? "f->signed_scratch" : "f->unsigned_scratch", id => values[id].Type); break;
                            }
                            code.Append("    ").Append(Value(value.Id)).Append(" = ").Append(expression).Append(";\n");
                            if (value.Type.Kind == IrTypeKind.ManagedLocation) PublishLocation(ObjectRootKind.Value, value.Id, value.Type, block.Id);
                            if (value.Expression is IrExpression.NewObject created)
                            {
                                Guard(Value(value.Id) + " == nullptr", "context.pending == 0u ? 5u : context.pending", value.ILOffset);
                                // Publish the new reference in the caller frame before entering its constructor.
                                string constructorCall = Call(created.Constructor, created.Arguments, Value(value.Id));
                                code.Append("    ").Append(constructorCall).Append(";\n"); Pending();
                            }
                            else if (value.Expression is IrExpression.EmptyArray or IrExpression.NewArray or IrExpression.StringLiteral or IrExpression.InvariantCulture or IrExpression.RuntimeTypeLiteral or IrExpression.ObjectGetType or IrExpression.IntegerTextPublish or IrExpression.StringFromCodeUnits or IrExpression.StringConcat or IrExpression.EnumEntry { Name: true } or IrExpression.EnumQuery { Message: true })
                                Guard(Value(value.Id) + " == nullptr", "context.pending == 0u ? 5u : context.pending", value.ILOffset);
                            else if (value.Expression is (IrExpression.LoadElement or IrExpression.LoadStatic) && value.Type.Kind == IrTypeKind.Reference)
                            {
                                Flush(); Check(Value(value.Id), value.Type.Class!, false, value.ILOffset);
                            }
                            else if (value.Expression is IrExpression.Call or IrExpression.CallDelegate or IrExpression.IntegerTextFind or IrExpression.RuntimeTypeHash or IrExpression.ObjectIdentityHash or IrExpression.EnumEntry or IrExpression.EnumQuery) Pending();
                            break;
                        }
                        case IrStatement.StoreLocal store:
                            code.Append("    ").Append(layout.Storage(ObjectRootKind.Local, store.Local)).Append(" = ").Append(Value(store.Value)).Append(";\n");
                            if (method.Locals[store.Local].Kind == IrTypeKind.ManagedLocation)
                                PublishLocation(ObjectRootKind.Local, store.Local, method.Locals[store.Local]);
                            break;
                        case IrStatement.StoreLocation store:
                            Guard("store_location_" + LocationKind(values[store.Location].Type.Element!)
                                + "(context, " + Value(store.Location) + ", " + Value(store.Value) + ") != LocationStatus::complete", "6u", store.ILOffset); break;
                        case IrStatement.InitializeLocation initialize:
                            bool integer = values[initialize.Location].Type.Element!.Kind is IrTypeKind.Int32 or IrTypeKind.Int64;
                            Guard("store_location_" + LocationKind(values[initialize.Location].Type.Element!) + "(context, " + Value(initialize.Location) + ", "
                                + (integer ? "0u" : "nullptr") + ") != LocationStatus::complete", "6u", initialize.ILOffset); break;
                        case IrStatement.InvokeDelegate invoke:
                            string delegateInvocation = DelegateCall(invoke.Type, invoke.Delegate, invoke.Arguments, invoke.ILOffset);
                            code.Append("    ").Append(delegateInvocation).Append(";\n"); Pending(); break;
                        case IrStatement.InitializeType initialize:
                            var init = initializers[initialize.Target];
                            Guard("initialize_type(context, " + N(init.index) + ", &" + TypeSymbol(exceptions!.TypeInitialization!) + "_descriptor, initialization_name_" + N(init.index)
                                + ", " + N(init.binding.TypeInitializerName!.Length) + ", " + CppEmitter.Symbol(initialize.Target) + ") != 0u", "context.pending", initialize.ILOffset); break;
                        case IrStatement.Invoke invoke:
                            if (invoke.VirtualDispatch) SelectDispatch(invoke.Target, invoke.Arguments, invoke.ILOffset);
                            else if (invoke.NullCheckReceiver) Guard(Value(invoke.Arguments[0]) + " == nullptr", "4u", invoke.ILOffset);
                            string invocation = Call(invoke.Target, invoke.Arguments, dispatch: invoke.VirtualDispatch, offset: invoke.ILOffset);
                            code.Append("    ").Append(invocation).Append(";\n"); Pending(); break;
                        case IrStatement.StoreElement store:
                            ArrayCheck(store.Array, store.ArrayType, store.Index, store.ILOffset);
                            if (store.ArrayType.ArrayElement!.Integer32)
                                code.Append("    store_integer_element(").Append(Value(store.Array)).Append(", ").Append(Value(store.Index)).Append(", ")
                                    .Append(Value(store.Value)).Append(store.ArrayType.ArrayElement.Kind == ObjectArrayElementKind.Byte ? " & 255u"
                                        : store.ArrayType.ArrayElement.Kind == ObjectArrayElementKind.Char ? " & 65535u" : "").Append(");\n");
                            else if (store.ArrayType.ArrayElement.Kind == ObjectArrayElementKind.Int64)
                                code.Append("    store_wide_element(").Append(Value(store.Array)).Append(", ").Append(Value(store.Index)).Append(", ").Append(Value(store.Value)).Append(");\n");
                            else if (store.CopyCast)
                            {
                                code.Append("    if (!store_reference_element(context, ").Append(Value(store.Array)).Append(", ").Append(Value(store.Index)).Append(", ").Append(Value(store.Value)).Append(")) {\n");
                                CastFault("context.pending == 11u", Value(store.Value), "copy_element_type(" + Value(store.Array) + "->type)", store.ILOffset, dynamicTarget: true);
                                Fault("context.pending", store.ILOffset); code.Append("    }\n");
                            }
                            else
                                Guard("!store_reference_element(context, " + Value(store.Array) + ", " + Value(store.Index) + ", " + Value(store.Value) + ")", "context.pending", store.ILOffset);
                            break;
                        case IrStatement.StoreStatic store:
                            Guard("!ensure_static_storage(context)", "context.pending == 0u ? 5u : context.pending", store.ILOffset); Flush();
                            if (statics[store.Field].f.Type.Class is { } staticClass) Check(Value(store.Value), staticClass, false, store.ILOffset);
                            code.Append("    context.instance.statics[").Append(N(statics[store.Field].index)).Append("].")
                                .Append(statics[store.Field].f.Type == IrType.Int32 ? "bits" : statics[store.Field].f.Type == IrType.Int64 ? "wide" : "reference").Append(" = ").Append(Value(store.Value)).Append(";\n"); break;
                        case IrStatement.StoreField store:
                            Check(Value(store.Receiver), store.Field.Owner, true, store.ILOffset);
                            if (values[store.Value].Type.Kind == IrTypeKind.Reference)
                                Check(Value(store.Value), values[store.Value].Type.Class!, false, store.ILOffset);
                            code.Append("    ").Append(Field(store.Field, store.Receiver)).Append(" = ").Append(Value(store.Value)).Append(";\n"); break;
                    }
                    if (isolatedOperation || ++statementsInStep == StatementsPerStep) { Flush(); statementsInStep = 0; }
                }
                Flush();
                switch (block.Terminator)
                {
                    case IrThrow thrown:
                        code.Append("    (void)raise_exception(context, ").Append(Value(thrown.Value)).Append(", \"").Append(method.Source.ContentHash)
                            .Append("\", ").Append(N(method.Source.Token)).Append("u, ").Append(N(thrown.ILOffset)).Append("u);\n");
                        Guard("context.pending == 0u", "6u", thrown.ILOffset); code.Append("    return frame_done;\n"); Flush(terminal: true); break;
                    case IrRethrow:
                        code.Append("    rethrow_exception(context, f, ").Append(N(exceptionRules!.CatchAt(method, block.Id))).Append("u);\n    return frame_done;\n"); Flush(terminal: true); break;
                    case IrLeave leave:
                        code.Append("    return leave_exception_scope(context, f, ").Append(N(leave.Edge.Target)).Append("u);\n"); Flush(terminal: true); break;
                    case IrEndFinally:
                        code.Append("    return finish_exception_scope(context, f, ").Append(N(exceptionRules!.CleanupAt(method, block.Id))).Append("u, false);\n"); Flush(terminal: true); break;
                    case IrEndFilter ended:
                        code.Append("    return finish_exception_scope(context, f, ").Append(N(exceptionRules!.FilterAt(method, block.Id))).Append("u, ")
                            .Append(Value(ended.Value)).Append(" != 0u);\n"); Flush(terminal: true); break;
                    case IrReturn returned:
                        code.Append("    f->result.").Append(PacketMember(method.ReturnType)).Append(" = ")
                            .Append(Value(returned.Value)).Append(";\n    return frame_done;\n"); Flush(terminal: true); break;
                    case IrVoidReturn: code.Append("    return frame_done;\n"); Flush(terminal: true); break;
                    case IrJump jump: Edge(jump.Edge); break;
                    case IrBranch branch:
                        string taken = "taken" + N(block.Id), other = "other" + N(block.Id);
                        code.Append("    return (").Append(CppEmitter.Comparison(branch.Comparison, branch.Left, branch.Right,
                            values[branch.Left].Type.Kind is IrTypeKind.Reference or IrTypeKind.NullReference, Value,
                            wide: values[branch.Left].Type == IrType.Int64)).Append(") ? ").Append(taken).Append(" : ").Append(other).Append(";\n");
                        Flush(terminal: true);
                        Label(taken); Edge(branch.Taken); Label(other); Edge(branch.NotTaken); break;
                }
            }
            code = output;
            string stepScope = CppEmitter.Symbol(IrMethodId.From(method.Source)) + "_steps";
            code.Append("namespace ").Append(stepScope).Append(" {\n");
            foreach (var label in labels) code.Append("[[maybe_unused]] static constexpr std::uint32_t ").Append(label.Key).Append(" = ").Append(N(label.Value)).Append("u;\n");
            if (handlers) ObjectCppExceptionFlow.Append(code, method, layout, FrameType(method), TypeSymbol);
            for (int i = 0; i < steps.Count; i++)
                code.Append("[[gnu::noinline]] static std::uint32_t s").Append(N(i)).Append("([[maybe_unused]] Context& context, [[maybe_unused]] ")
                    .Append(FrameType(method)).Append("* f) noexcept {\n").Append(steps[i]).Append("}\n");
            code.Append("static constexpr std::uint32_t (*table[])(Context&, ").Append(FrameType(method)).Append("*) noexcept = {")
                .Append(string.Join(", ", Enumerable.Range(0, steps.Count).Select(i => "s" + N(i)))).Append("};\n}\n");
            inStep = false;
            Header(method); code.Append(" {\n");
            if (handlers)
            {
                code.Append("    if (context.pending != 0u) ").Append(Exit()).Append('\n');
                Guard("context.exception != nullptr", "6u", method.Blocks[0].ILOffset);
                code.Append("    if (context.depth == 0) {\n        const auto preparation = prepare_exceptions(context);\n");
                Guard("preparation != 0u", "preparation", method.Blocks[0].ILOffset); code.Append("    }\n");
            }
            Guard("context.depth >= 64u", "8u", method.Blocks[0].ILOffset);
            code.Append("    Storage<").Append(FrameType(method)).Append("> storage(context);\n");
            Guard("storage.value == nullptr", "9u", method.Blocks[0].ILOffset);
            code.Append("    auto* f = storage.value; f->incoming = arguments;\n")
                .Append("    RootScope roots(context, f->header, f->roots.data(), f->roots.size());\n");
            if (layout.BorrowsFrame)
            {
                code.Append("    BorrowScope borrowed(context, f, ")
                    .Append(FrameType(method)).Append("_root_types.data());\n");
                Guard("borrowed.status != LocationStatus::complete", "borrowed.status == LocationStatus::limit ? 9u : 6u", method.Blocks[0].ILOffset);
                code.Append("    f->borrow_token = borrowed.frame.token;\n");
            }
            if (handlers) code.Append("    std::uint32_t step = 0;\n    while (step != frame_done) {\n        f->exception_routed = false;\n        step = ").Append(stepScope)
                .Append("::table[step](context, f);\n        if (context.pending != 0u && !f->exception_routed) step = ").Append(stepScope).Append("::dispatch_exception(context, f);\n    }\n");
            else code.Append("    std::uint32_t step = 0;\n    while (step != frame_done && context.pending == 0u) step = ").Append(stepScope).Append("::table[step](context, f);\n");
            code.Append("    ").Append(method.ReturnType.Kind switch
                {
                    IrTypeKind.Void => "return;",
                    IrTypeKind.Int32 => "return std::bit_cast<std::int32_t>(f->result.bits);",
                    IrTypeKind.Int64 => "return std::bit_cast<std::int64_t>(f->result.wide_bits);",
                    _ => "return f->result.reference;",
                }).Append("\n}\n");
            void Edge(IrEdge edge)
            {
                // Inputs and source values have distinct slots, including on self edges.
                for (int i = 0; i < edge.Arguments.Length; i++)
                {
                    code.Append("    ").Append(layout.Storage(ObjectRootKind.BlockInput, i, edge.Target)).Append(" = ").Append(Value(edge.Arguments[i])).Append(";\n");
                    if ((i + 1) % CopiesPerStep == 0) Flush();
                }
                code.Append("    return b").Append(N(edge.Target)).Append(";\n"); Flush(terminal: true);
            }
        }
    }

    private static string CType(IrType type) => type.Kind switch { IrTypeKind.Int32 => "std::int32_t", IrTypeKind.Int64 => "std::int64_t", IrTypeKind.Void => "void", _ => "Object*" };
    private static string StorageType(ObjectStorage storage) => storage switch
        { ObjectStorage.Int32 => "std::uint32_t", ObjectStorage.Int64 => "std::uint64_t", _ => "Object*" };
    private static string StorageKind(ObjectStorage storage) => storage switch
        { ObjectStorage.Int32 => "integer", ObjectStorage.Int64 => "wide_integer", _ => "reference" };
    private static string LocationKind(IrType type) => type.Kind switch
        { IrTypeKind.Int32 => "integer", IrTypeKind.Int64 => "wide_integer", _ => "reference" };
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string TypeSymbol(ObjectTypeId type) => "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture)
        + (type.Specialization.Length == 0 ? "" : "_g_" + type.Specialization)
        + (type.ArrayElement is null ? "" : type.ArrayElement.Kind == ObjectArrayElementKind.Int32 ? "_vector_i4"
            : type.ArrayElement.Kind == ObjectArrayElementKind.Int64 ? "_vector_i8"
            : type.ArrayElement.Kind == ObjectArrayElementKind.Byte ? "_vector_u1"
            : type.ArrayElement.Kind == ObjectArrayElementKind.Char ? "_vector_char"
            : (type.ArrayElement.Kind == ObjectArrayElementKind.Value ? "_vector_value_" : "_vector_ref_") + TypeSymbol(type.ArrayElement.Type!));
    private static string DispatchSymbol(IrMethodId method) => CppEmitter.Symbol(method) + "_dispatch";
    private static string ContractSymbol(ObjectTypeId type) => TypeSymbol(type) + "_implements";
    internal static string ReferencePredicate(ObjectTypeCatalog catalog, ObjectTypeId type) =>
        catalog.Interfaces.Any(i => ObjectTypeId.From(i.Source) == type) ? ContractSymbol(type) : TypeSymbol(type) + "_accepts";
    internal static string ReferenceDescriptor(ObjectTypeId type) => TypeSymbol(type) + "_descriptor";
    private static string FieldSymbol(ObjectFieldId field) => "f_" + field.Token.ToString("x8", CultureInfo.InvariantCulture) + (field.Component < 0 ? "" : "_c" + N(field.Component));
}
