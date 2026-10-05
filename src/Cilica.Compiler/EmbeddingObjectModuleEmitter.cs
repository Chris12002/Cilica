using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cilica.Compiler;

public enum EmbeddingValueKind { Void = 0, Int32 = 1, Handle = 2 }
public sealed record EmbeddingValueShape(EmbeddingValueKind Kind, uint ReferenceTypeId = 0);
public sealed record EmbeddingReferenceBinding(uint Id, string Name, ObjectTypeId Type);
public sealed record EmbeddingObjectExport(uint Id, string Name, IrMethodId Method,
    ImmutableArray<EmbeddingValueShape> Parameters, EmbeddingValueShape Result);
public sealed record EmbeddingObjectContract(string Name, uint SchemaVersion,
    ImmutableArray<EmbeddingReferenceBinding> References, ImmutableArray<EmbeddingObjectExport> Exports,
    uint Utf16StringTypeId = 0);

/// <summary>Version 3: Int32, void and runtime-owned strong handles, from one validated closure.</summary>
public static class EmbeddingObjectModuleEmitter
{
    public const string Profile = "embedding-object-handles-v3";
    public const int MaximumReferenceTypes = 64;

    public static EmbeddingModuleArtifact EmitProgram(ObjectIrProgram program, EmbeddingObjectContract contract)
    {
        ArgumentNullException.ThrowIfNull(program);
        var native = ObjectCppEmitter.EmitProgram(program);
        return Wrap(native, program, contract, locations: false);
    }

    public static EmbeddingModuleArtifact EmitExceptions(ManagedExceptionIrProgram program, EmbeddingObjectContract contract)
    {
        ArgumentNullException.ThrowIfNull(program);
        _ = ValueRootAssignment.Assign(program);
        Validate(program.Flow.Flow.Flow.Methods, program.Flow.Bindings, program.Flow.Flow.Flow.Types.References, contract);
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var native = ObjectCppEmitter.EmitExceptions(scalar);
        return Wrap(native, scalar.Flow.Flow.Flow.Flow, contract, locations: true);
    }

    private static void Validate(ImmutableArray<IrMethod> methods, ImmutableArray<ObjectMethodBinding> bindings, ObjectTypeCatalog catalog, EmbeddingObjectContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        void Require(bool condition, string reason) { if (!condition) throw new ArgumentException("Invalid object export contract: " + reason, nameof(contract)); }
        Require(EmbeddingModuleEmitter.ValidName(contract.Name) && contract.SchemaVersion != 0, "module name/schema version.");
        Require(!contract.Exports.IsDefaultOrEmpty && contract.Exports.Length <= EmbeddingModuleEmitter.MaximumExports, "1..64 exports required.");
        Require(!contract.References.IsDefault && contract.References.Length <= MaximumReferenceTypes, "bounded reference declarations required.");
        Require(contract.References.All(r => r is not null && r.Id != 0 && EmbeddingModuleEmitter.ValidName(r.Name) && r.Type is not null), "reference declaration.");
        Require(contract.References.Select(r => r.Id).Distinct().Count() == contract.References.Length
            && contract.References.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count() == contract.References.Length
            && contract.References.Select(r => r.Type).Distinct().Count() == contract.References.Length, "duplicate reference ID/name/type.");
        var references = contract.References.ToDictionary(r => r.Id);
        if (contract.Utf16StringTypeId != 0)
            Require(references.TryGetValue(contract.Utf16StringTypeId, out var text) && text.Type == catalog.String,
                "UTF-16 capability must bind the closure's exact String type.");
        Require(contract.Exports.All(e => e is not null && e.Id != 0 && EmbeddingModuleEmitter.ValidName(e.Name) && e.Method is not null && !e.Parameters.IsDefault), "export declaration.");
        Require(contract.Exports.Select(e => e.Id).Distinct().Count() == contract.Exports.Length
            && contract.Exports.Select(e => e.Name).Distinct(StringComparer.Ordinal).Count() == contract.Exports.Length
            && contract.Exports.Select(e => e.Method).Distinct().Count() == contract.Exports.Length, "duplicate export ID/name/method.");
        var bodies = methods.ToDictionary(m => IrMethodId.From(m.Source));
        var declarations = bindings.ToDictionary(b => b.Method);
        IrType Shape(EmbeddingValueShape? shape, bool result)
        {
            Require(shape is not null, "missing value shape.");
            if (shape!.Kind == EmbeddingValueKind.Handle) {
                Require(references.ContainsKey(shape.ReferenceTypeId), "unknown reference type ID.");
                return IrType.Reference(references[shape.ReferenceTypeId].Type);
            }
            Require(shape.ReferenceTypeId == 0, "scalar/void shape cannot name a reference type.");
            Require(shape.Kind == EmbeddingValueKind.Int32 || result && shape.Kind == EmbeddingValueKind.Void, "unsupported value kind.");
            return shape.Kind == EmbeddingValueKind.Int32 ? IrType.Int32 : IrType.Void;
        }
        foreach (var export in contract.Exports) {
            Require(bodies.ContainsKey(export.Method) && declarations.ContainsKey(export.Method), "export is outside this compiled closure.");
            var body = bodies[export.Method]; var binding = declarations[export.Method];
            Require(binding.Kind == ObjectMethodKind.Static && binding.OwnerExternallyVisible && binding.TypeInitializerName is null
                && (binding.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public
                && (binding.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) == 0, "public ordinary static method required.");
            Require(body.Parameters.Length == export.Parameters.Length, "parameter count mismatch.");
            Require(body.ReturnType == Shape(export.Result, result: true), "result shape mismatch.");
            for (int i = 0; i < body.Parameters.Length; ++i)
                Require(body.Parameters[i] == Shape(export.Parameters[i], result: false), "parameter shape mismatch.");
        }
        var used = contract.Exports.SelectMany(e => e.Parameters.Append(e.Result)).Where(s => s.Kind == EmbeddingValueKind.Handle).Select(s => s.ReferenceTypeId).Distinct().Count();
        Require(used == references.Count, "unused reference declaration.");
    }

    private static EmbeddingModuleArtifact Wrap(CppArtifact native, ObjectIrProgram program, EmbeddingObjectContract contract, bool locations)
    {
        Validate(program.Flow.Methods, program.Bindings, program.Flow.Types, contract);
        var exports = contract.Exports.OrderBy(e => e.Id).ToArray();
        var references = contract.References.OrderBy(r => r.Id).ToArray();
        var types = references.ToDictionary(r => r.Id, r => r.Type);
        var methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        object Shape(EmbeddingValueShape s) => new { kind = s.Kind switch { EmbeddingValueKind.Int32 => "int32", EmbeddingValueKind.Handle => "handle", _ => "void" }, referenceTypeId = s.ReferenceTypeId };
        var schema = new { abiVersion = 3, name = contract.Name, schemaVersion = contract.SchemaVersion,
            references = references.Select(r => new { id = r.Id, name = r.Name }).ToArray(),
            exports = exports.Select(e => new { id = e.Id, name = e.Name, parameters = e.Parameters.Select(Shape).ToArray(), result = Shape(e.Result) }).ToArray() };
        // Preserve the original v3 fingerprint when this optional capability is disabled.
        object publicSchema = contract.Utf16StringTypeId == 0 ? schema : new {
            schema.abiVersion, schema.name, schema.schemaVersion, schema.references, schema.exports,
            utf16StringTypeId = contract.Utf16StringTypeId };
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(publicSchema))));
        string manifest = JsonSerializer.Serialize(new { profile = Profile, contractHash = hash, contract = publicSchema,
            bindings = exports.Select(e => new { id = e.Id, imageHash = e.Method.ContentHash, methodToken = e.Method.Token, specialization = e.Method.Specialization }).ToArray(),
            referenceBindings = references.Select(r => new { id = r.Id, type = r.Type }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        string prefix = "cilica_" + contract.Name + "_abi_v3";
        string scope = CppEmitter.Symbol(program.Flow.Entry) + "_objects_packed_closure";
        string packet = locations ? "LocationArgument" : "Argument";
        string Predicate(EmbeddingValueShape s) => ObjectCppEmitter.ReferencePredicate(program.Flow.Types, types[s.ReferenceTypeId]);
        bool utf16 = contract.Utf16StringTypeId != 0;
        string header = EmbeddingObjectAbi.Header(prefix, hash, contract.SchemaVersion, exports, references, utf16);
        var helpers = new StringBuilder(); var arities = new StringBuilder(); var dispatch = new StringBuilder();
        foreach (var export in exports) {
            var method = methods[export.Method];
            arities.Append(CultureInfo.InvariantCulture, $"case {export.Id}u: arity = {export.Parameters.Length}u; break;\n");
            dispatch.Append(CultureInfo.InvariantCulture, $"case {export.Id}u: status = invoke_{export.Id}(context, parameters, completed.value); break;\n");
            helpers.Append(CultureInfo.InvariantCulture, $"static constexpr std::array<HostParameter, {export.Parameters.Length}> schema_{export.Id} = {{{{")
                .AppendJoin(", ", export.Parameters.Select(s => "{" + ((int)s.Kind).ToString(CultureInfo.InvariantCulture) + "u, " + (s.Kind == EmbeddingValueKind.Handle ? Predicate(s) : "nullptr") + "}"))
                .Append("}};\n");
            string call = CppEmitter.Symbol(export.Method) + "(context, arguments.value->data())";
            string result = export.Result.Kind switch {
                EmbeddingValueKind.Int32 => "const auto value = " + call + ";\nif (context.pending == 0) result = {1u, 0u, std::bit_cast<std::uint32_t>(value)};",
                EmbeddingValueKind.Void => call + ";\nif (context.pending == 0) result = {};",
                _ => "Object* value = " + call + ";\nif (context.pending == 0) {\n"
                    + "    if (!traceable_reference(context.instance, value) || (value != nullptr && !" + Predicate(export.Result) + "(value->type))) context.pending = 6u;\n"
                    + "    else { result.kind = 2u; if (reservation.publish(value, result.payload) != CILICA_V3_OK) context.pending = 6u; }\n}"
            };
            helpers.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static uint32_t invoke_{{export.Id}}(Context& context, const cilica_v3_value* parameters, cilica_v3_value& result) noexcept {
                    const auto validation = validate_arguments(context.instance, parameters, schema_{{export.Id}}.data(), {{export.Parameters.Length}}u);
                    if (validation != CILICA_V3_OK) return validation;
                    ResultHandleReservation reservation{context.instance};
                    {{(export.Result.Kind == EmbeddingValueKind.Handle ? "const auto reserved = reservation.reserve(); if (reserved != CILICA_V3_OK) return reserved;" : "")}}
                    Storage<std::array<{{packet}}, {{export.Parameters.Length}}>> arguments(context);
                    if (arguments.value == nullptr) {
                        context.pending = 9u; context.image = "{{method.Source.ContentHash.ToLowerInvariant()}}";
                        context.method = {{method.Source.Token}}u; context.il = {{method.Blocks[0].ILOffset}}u;
                    } else {
                        // Admission excludes releases; validated handles remain strong roots.
                        for (std::size_t i = 0; i < {{export.Parameters.Length}}u; ++i) {
                            if (parameters[i].kind == 1u) (*arguments.value)[i].bits = static_cast<std::uint32_t>(parameters[i].payload);
                            else (*arguments.value)[i].reference = parameters[i].payload == 0 ? nullptr : resolve_handle(context.instance, {parameters[i].payload}).value;
                        }
                        {{result}}
                    }
                    return context.pending == 0 ? CILICA_V3_OK : CILICA_V3_EXECUTION_FAILED;
                }
                """);
        }
        string implementation = $$"""
            namespace {{prefix}}_private {
            using namespace {{scope}};
            {{EmbeddingCppRegistry.Text}}
            {{EmbeddingObjectAbi.Runtime}}
            {{helpers}}
            }
            {{EmbeddingModuleLifecycle.Emit(prefix, 3, hash, handles: true, utf16)}}
            {{(utf16 ? EmbeddingStringAbi.Emit(prefix, ObjectCppEmitter.ReferenceDescriptor(program.Flow.Types.String!)) : "")}}
            extern "C" uint32_t {{prefix}}_invoke(uint64_t runtime, uint32_t export_id, const cilica_v3_value* parameters, uint32_t count, cilica_v3_result* result) noexcept {
                if (result == nullptr) return CILICA_V3_INVALID_ARGUMENT;
                std::uint32_t arity;
                switch (export_id) { {{arities}} default: return CILICA_V3_UNKNOWN_EXPORT; }
                if (count != arity || (count != 0 && parameters == nullptr)) return CILICA_V3_INVALID_ARGUMENT;
                using namespace {{prefix}}_private;
                Slot* slot; auto status = enter(runtime, slot);
                if (status != CILICA_V3_OK) return status;
                Invocation invocation{*slot}; Context context(*slot->heap, slot->execution_limit);
                cilica_v3_result completed{};
                switch (export_id) { {{dispatch}} }
                if (status != CILICA_V3_OK && status != CILICA_V3_EXECUTION_FAILED) return status;
                if (context.pending != 0) {
                    completed.value = {}; completed.fault_code = context.pending;
                    completed.method_token = context.method; completed.il_offset = context.il;
                    if (context.image != nullptr) for (std::size_t i = 0; i < 64 && context.image[i] != '\0'; ++i) completed.image_hash[i] = context.image[i];
                }
                *result = completed; return status;
            }
            extern "C" uint32_t {{prefix}}_retain_handle(uint64_t runtime, uint64_t handle, uint64_t* retained) noexcept {
                if (retained == nullptr) return CILICA_V3_INVALID_ARGUMENT;
                using namespace {{prefix}}_private;
                Slot* slot; const auto status = enter(runtime, slot);
                if (status != CILICA_V3_OK) return status;
                Invocation invocation{*slot};
                if (handle == 0) { *retained = 0; return CILICA_V3_OK; }
                const auto resolved = resolve_handle(*slot->heap, {handle});
                if (resolved.status != HandleStatus::complete) return handle_status(resolved.status);
                const auto result = retain_handle(*slot->heap, resolved.value);
                if (result.status == HandleStatus::complete) *retained = result.handle.token;
                return handle_status(result.status);
            }
            extern "C" uint32_t {{prefix}}_release_handle(uint64_t runtime, uint64_t handle) noexcept {
                using namespace {{prefix}}_private;
                Slot* slot; const auto status = enter(runtime, slot);
                if (status != CILICA_V3_OK) return status;
                Invocation invocation{*slot};
                return handle == 0 ? CILICA_V3_OK : handle_status(release_handle(*slot->heap, {handle}));
            }
            """;
        return new(prefix, header, header + native.Text + "\n" + implementation + "\n", manifest, hash);
    }
}
