using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cilica.Compiler;

public enum EmbeddingValueKind { Void = 0, Int32 = 1, Handle = 2, Integer64 = 3, Record = 4 }
public sealed record EmbeddingValueShape(EmbeddingValueKind Kind, uint ReferenceTypeId = 0, uint RecordTypeId = 0);
public sealed record EmbeddingReferenceBinding(uint Id, string Name, ObjectTypeId Type);
public sealed record EmbeddingObjectExport(uint Id, string Name, IrMethodId Method,
    ImmutableArray<EmbeddingValueShape> Parameters, EmbeddingValueShape Result);
public sealed record EmbeddingObjectContract(string Name, uint SchemaVersion,
    ImmutableArray<EmbeddingReferenceBinding> References, ImmutableArray<EmbeddingObjectExport> Exports,
    uint Utf16StringTypeId = 0, bool CaptureExceptions = false)
{
    public ImmutableArray<EmbeddingRecordBinding> Records { get; init; } = [];
}

/// <summary>Version 3: integer bits, void and runtime-owned strong handles, from one validated closure.</summary>
public static class EmbeddingObjectModuleEmitter
{
    public const string Profile = "embedding-object-handles-v3";
    public const int MaximumReferenceTypes = 64;

    public static EmbeddingModuleArtifact EmitProgram(ObjectIrProgram program, EmbeddingObjectContract contract)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.CaptureExceptions) throw new ArgumentException("Exception capture requires EmitExceptions and a validated managed-exception closure.", nameof(contract));
        if (!contract.Records.IsDefaultOrEmpty) throw new ArgumentException("Record exports require EmitExceptions and its validated value lowering.", nameof(contract));
        var native = ObjectCppEmitter.EmitProgram(program);
        return Wrap(native, program, contract, locations: false);
    }

    public static EmbeddingModuleArtifact EmitExceptions(ManagedExceptionIrProgram program, EmbeddingObjectContract contract)
    {
        ArgumentNullException.ThrowIfNull(program);
        _ = ValueRootAssignment.Assign(program);
        ArgumentNullException.ThrowIfNull(contract);
        var records = EmbeddingRecordPlan.Build(program.Flow.Flow.Flow.Types, contract.Records);
        Validate(program.Flow.Flow.Flow.Methods, program.Flow.Bindings, program.Flow.Flow.Flow.Types.References, contract, records);
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var native = ObjectCppEmitter.EmitExceptions(scalar);
        return Wrap(native, scalar.Flow.Flow.Flow.Flow, contract, locations: true, program.Exceptions, records);
    }

    private static void Validate(ImmutableArray<IrMethod> methods, ImmutableArray<ObjectMethodBinding> bindings, ObjectTypeCatalog catalog, EmbeddingObjectContract contract, EmbeddingRecordPlan records, bool lowered = false)
    {
        ArgumentNullException.ThrowIfNull(contract);
        void Require(bool condition, string reason) { if (!condition) throw new ArgumentException("Invalid object export contract: " + reason, nameof(contract)); }
        Require(EmbeddingModuleEmitter.ValidName(contract.Name) && contract.SchemaVersion != 0, "module name/schema version.");
        Require(!contract.Exports.IsDefaultOrEmpty && contract.Exports.Length <= EmbeddingModuleEmitter.MaximumExports, "1..64 exports required.");
        Require(!contract.References.IsDefault && contract.References.Length <= MaximumReferenceTypes, "bounded reference declarations required.");
        Require(!contract.Records.IsDefault, "record declarations must be initialized.");
        Require(contract.References.All(r => r is not null && r.Id != 0 && EmbeddingModuleEmitter.ValidName(r.Name) && r.Type is not null), "reference declaration.");
        Require(contract.References.Select(r => r.Id).Distinct().Count() == contract.References.Length
            && contract.References.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count() == contract.References.Length
            && contract.References.Select(r => r.Type).Distinct().Count() == contract.References.Length, "duplicate reference ID/name/type.");
        var references = contract.References.ToDictionary(r => r.Id);
        Require(!contract.CaptureExceptions || catalog.String is not null, "exception capture requires the closure's intrinsic String type.");
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
            if (shape!.Kind == EmbeddingValueKind.Record) {
                Require(shape.ReferenceTypeId == 0 && records.Records.ContainsKey(shape.RecordTypeId), "unknown record ID or reference ID on record shape.");
                return IrType.Value(records.Records[shape.RecordTypeId].Binding.Type);
            }
            Require(shape.RecordTypeId == 0, "non-record shape cannot name a record type.");
            if (shape.Kind == EmbeddingValueKind.Handle) {
                Require(references.ContainsKey(shape.ReferenceTypeId), "unknown reference type ID.");
                return IrType.Reference(references[shape.ReferenceTypeId].Type);
            }
            Require(shape.ReferenceTypeId == 0, "scalar/void shape cannot name a reference type.");
            Require(shape.Kind is EmbeddingValueKind.Int32 or EmbeddingValueKind.Integer64 || result && shape.Kind == EmbeddingValueKind.Void, "unsupported value kind.");
            return shape.Kind switch { EmbeddingValueKind.Int32 => IrType.Int32, EmbeddingValueKind.Integer64 => IrType.Int64, _ => IrType.Void };
        }
        foreach (var export in contract.Exports) {
            Require(bodies.ContainsKey(export.Method) && declarations.ContainsKey(export.Method), "export is outside this compiled closure.");
            var body = bodies[export.Method]; var binding = declarations[export.Method];
            Require(binding.Kind == ObjectMethodKind.Static && binding.OwnerExternallyVisible && binding.TypeInitializerName is null
                && (binding.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public
                && (binding.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) == 0, "public ordinary static method required.");
            var resultType = Shape(export.Result, result: true);
            var parameterTypes = export.Parameters.Select(s => Shape(s, result: false)).ToImmutableArray();
            if (lowered) {
                parameterTypes = [.. parameterTypes.SelectMany(t => t.Kind == IrTypeKind.Value
                    ? records.Records.Values.Single(r => r.Binding.Type == t.Class).Layout.Slots.Select(s => EmbeddingRecordPlan.Scalar(s.Storage)) : [t])];
                if (resultType.Kind == IrTypeKind.Value) {
                    parameterTypes = [.. parameterTypes, .. records.Records[export.Result.RecordTypeId].Layout.Slots.Select(s => IrType.Location(EmbeddingRecordPlan.Scalar(s.Storage)))];
                    resultType = IrType.Void;
                }
            }
            Require(body.Parameters.SequenceEqual(parameterTypes), "parameter shape/count mismatch.");
            Require(body.ReturnType == resultType, "result shape mismatch.");
        }
        var used = contract.Exports.SelectMany(e => e.Parameters.Append(e.Result)).Where(s => s.Kind == EmbeddingValueKind.Handle).Select(s => s.ReferenceTypeId).Distinct().Count();
        Require(used == references.Count, "unused reference declaration.");
        Require(contract.Exports.SelectMany(e => e.Parameters.Append(e.Result)).Where(s => s.Kind == EmbeddingValueKind.Record)
            .Select(s => s.RecordTypeId).Distinct().Count() == records.Records.Count, "unused record declaration.");
    }

    private static EmbeddingModuleArtifact Wrap(CppArtifact native, ObjectIrProgram program, EmbeddingObjectContract contract, bool locations, ManagedExceptionTypes? exceptions = null, EmbeddingRecordPlan? records = null)
    {
        records ??= EmbeddingRecordPlan.Empty;
        Validate(program.Flow.Methods, program.Bindings, program.Flow.Types, contract, records, lowered: true);
        var exports = contract.Exports.OrderBy(e => e.Id).ToArray();
        var references = contract.References.OrderBy(r => r.Id).ToArray();
        var types = references.ToDictionary(r => r.Id, r => r.Type);
        var methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        object Shape(EmbeddingValueShape s) => s.Kind == EmbeddingValueKind.Record ? new { kind = "record", recordTypeId = s.RecordTypeId }
            : new { kind = s.Kind switch { EmbeddingValueKind.Int32 => "int32", EmbeddingValueKind.Integer64 => "integer64", EmbeddingValueKind.Handle => "handle", _ => "void" }, referenceTypeId = s.ReferenceTypeId };
        var schema = new { abiVersion = 3, name = contract.Name, schemaVersion = contract.SchemaVersion,
            references = references.Select(r => new { id = r.Id, name = r.Name }).ToArray(),
            exports = exports.Select(e => new { id = e.Id, name = e.Name, parameters = e.Parameters.Select(Shape).ToArray(), result = Shape(e.Result) }).ToArray() };
        // Preserve the original v3 fingerprint when this optional capability is disabled.
        object publicSchema = contract.Utf16StringTypeId == 0 ? schema : new {
            schema.abiVersion, schema.name, schema.schemaVersion, schema.references, schema.exports,
            utf16StringTypeId = contract.Utf16StringTypeId };
        if (contract.CaptureExceptions) {
            JsonObject extended = JsonSerializer.SerializeToNode(publicSchema)!.AsObject();
            extended.Add("captureExceptions", true); publicSchema = extended;
        }
        var recordDeclarations = records.Records.Values.OrderBy(r => r.Binding.Id).ToArray();
        if (recordDeclarations.Length != 0) {
            JsonObject extended = JsonSerializer.SerializeToNode(publicSchema)!.AsObject();
            extended.Add("records", JsonSerializer.SerializeToNode(recordDeclarations.Select(r => new {
                id = r.Binding.Id, name = r.Binding.Name,
                fields = r.Fields.Select(f => new { id = f.Binding.Id, name = f.Binding.Name, shape = Shape(new(f.Binding.Kind)) }).ToArray()
            }).ToArray())); publicSchema = extended;
        }
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(publicSchema))));
        var manifestNode = JsonSerializer.SerializeToNode(new { profile = Profile, contractHash = hash, contract = publicSchema,
            bindings = exports.Select(e => new { id = e.Id, imageHash = e.Method.ContentHash, methodToken = e.Method.Token, specialization = e.Method.Specialization }).ToArray(),
            referenceBindings = references.Select(r => new { id = r.Id, type = r.Type }).ToArray()
        })!.AsObject();
        if (recordDeclarations.Length != 0) manifestNode.Add("recordBindings", JsonSerializer.SerializeToNode(recordDeclarations.Select(r => new {
            id = r.Binding.Id, type = r.Binding.Type, fields = r.Fields.Select(f => new { id = f.Binding.Id, path = f.Binding.Path }).ToArray()
        }).ToArray()));
        string manifest = manifestNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        string prefix = "cilica_" + contract.Name + "_abi_v3";
        string scope = CppEmitter.Symbol(program.Flow.Entry) + "_objects_packed_closure";
        string packet = locations ? "LocationArgument" : "Argument";
        string Predicate(EmbeddingValueShape s) => ObjectCppEmitter.ReferencePredicate(program.Flow.Types, types[s.ReferenceTypeId]);
        bool utf16 = contract.Utf16StringTypeId != 0;
        bool integer64 = exports.Any(e => e.Result.Kind == EmbeddingValueKind.Integer64 || e.Parameters.Any(p => p.Kind == EmbeddingValueKind.Integer64))
            || recordDeclarations.Any(r => r.Fields.Any(f => f.Binding.Kind == EmbeddingValueKind.Integer64));
        bool hasRecords = recordDeclarations.Length != 0;
        string header = EmbeddingObjectAbi.Header(prefix, hash, contract.SchemaVersion, exports, references, utf16, contract.CaptureExceptions, recordDeclarations);
        var helpers = new StringBuilder(); var arities = new StringBuilder(); var dispatch = new StringBuilder();
        foreach (var export in exports) {
            var method = methods[export.Method];
            var inputs = records.Inputs(export.Parameters);
            var output = export.Result.Kind == EmbeddingValueKind.Record ? records.Records[export.Result.RecordTypeId] : null;
            if (output is not null) helpers.Append(EmbeddingRecordAbi.ReturnHelper(output, method, export.Id, inputs.Length)).Append('\n');
            bool requiresRecords = output is not null || export.Parameters.Any(p => p.Kind == EmbeddingValueKind.Record);
            arities.Append(CultureInfo.InvariantCulture, $"case {export.Id}u: arity = {inputs.Length}u; output_count = {output?.Fields.Length ?? 0}u; requires_records = {(requiresRecords ? "true" : "false")}; break;\n");
            dispatch.Append(CultureInfo.InvariantCulture, $"case {export.Id}u: status = invoke_{export.Id}(context, parameters, completed.value, captured == nullptr ? nullptr : &capture, fields); break;\n");
            helpers.Append(CultureInfo.InvariantCulture, $"static constexpr std::array<std::size_t, {inputs.Length}> mapping_{export.Id} = {{")
                .AppendJoin(", ", inputs.Select(i => i.NativeSlot.ToString(CultureInfo.InvariantCulture) + "u")).Append("};\n");
            helpers.Append(CultureInfo.InvariantCulture, $"static constexpr std::array<HostParameter, {inputs.Length}> schema_{export.Id} = {{{{")
                .AppendJoin(", ", inputs.Select(i => i.Shape).Select(s => "{" + ((int)s.Kind).ToString(CultureInfo.InvariantCulture) + "u, " + (s.Kind == EmbeddingValueKind.Handle ? Predicate(s) : "nullptr") + "}"))
                .Append("}};\n");
            string call = CppEmitter.Symbol(export.Method) + "(context, arguments.value->data())";
            string result = export.Result.Kind switch {
                EmbeddingValueKind.Int32 => "const auto value = " + call + ";\nif (context.pending == 0) result = {1u, 0u, std::bit_cast<std::uint32_t>(value)};",
                EmbeddingValueKind.Integer64 => "const auto value = " + call + ";\nif (context.pending == 0) result = {3u, 0u, std::bit_cast<std::uint64_t>(value)};",
                EmbeddingValueKind.Void => call + ";\nif (context.pending == 0) result = {};",
                EmbeddingValueKind.Record => "return_" + export.Id.ToString(CultureInfo.InvariantCulture) + "(context, arguments.value->data(), result, fields);",
                _ => "Object* value = " + call + ";\nif (context.pending == 0) {\n"
                    + "    if (!traceable_reference(context.instance, value) || (value != nullptr && !" + Predicate(export.Result) + "(value->type))) context.pending = 6u;\n"
                    + "    else { result.kind = 2u; if (reservation.publish(value, result.payload) != CILICA_V3_OK) context.pending = 6u; }\n}"
            };
            helpers.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static uint32_t invoke_{{export.Id}}(Context& context, const cilica_v3_value* parameters, cilica_v3_value& result, ResultHandleReservation* capture, [[maybe_unused]] cilica_v3_value* fields) noexcept {
                    const auto validation = validate_arguments(context.instance, parameters, schema_{{export.Id}}.data(), {{inputs.Length}}u);
                    if (validation != CILICA_V3_OK) return validation;
                    ResultHandleReservation local{context.instance};
                    auto& reservation = capture == nullptr ? local : *capture;
                    if ({{(export.Result.Kind == EmbeddingValueKind.Handle ? "true" : "capture != nullptr")}}) {
                        const auto reserved = reservation.reserve(); if (reserved != CILICA_V3_OK) return reserved;
                    }
                    Storage<std::array<{{packet}}, {{method.Parameters.Length}}>> arguments(context);
                    if (arguments.value == nullptr) {
                        context.pending = 9u; context.image = "{{method.Source.ContentHash.ToLowerInvariant()}}";
                        context.method = {{method.Source.Token}}u; context.il = {{method.Blocks[0].ILOffset}}u;
                    } else {
                        // Admission excludes releases; validated handles remain strong roots.
                        for (std::size_t i = 0; i < {{inputs.Length}}u; ++i) {
                            auto& argument = (*arguments.value)[mapping_{{export.Id}}[i]];
                            if (parameters[i].kind == 1u) argument.bits = static_cast<std::uint32_t>(parameters[i].payload);
                            else if (parameters[i].kind == 3u) argument.wide_bits = parameters[i].payload;
                            else argument.reference = parameters[i].payload == 0 ? nullptr : resolve_handle(context.instance, {parameters[i].payload}).value;
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
            {{EmbeddingModuleLifecycle.Emit(prefix, 3, hash, handles: true, utf16, integer64, contract.CaptureExceptions, hasRecords)}}
            {{(utf16 ? EmbeddingStringAbi.Emit(prefix, ObjectCppEmitter.ReferenceDescriptor(program.Flow.Types.String!)) : "")}}
            static uint32_t {{prefix}}_invoke_core(uint64_t runtime, uint32_t export_id, const cilica_v3_value* parameters, uint32_t count, cilica_v3_result* result, uint64_t* captured, cilica_v3_value* fields, uint32_t capacity, bool record_api) noexcept {
                if (result == nullptr) return CILICA_V3_INVALID_ARGUMENT;
                std::uint32_t arity, output_count; bool requires_records;
                switch (export_id) { {{arities}} default: return CILICA_V3_UNKNOWN_EXPORT; }
                if (count != arity || (count != 0 && parameters == nullptr)) return CILICA_V3_INVALID_ARGUMENT;
                if ((requires_records && !record_api) || (capacity != 0 && fields == nullptr)) return CILICA_V3_INVALID_ARGUMENT;
                if (capacity < output_count) return CILICA_V3_BUFFER_TOO_SMALL;
                using namespace {{prefix}}_private;
                Slot* slot; auto status = enter(runtime, slot);
                if (status != CILICA_V3_OK) return status;
                Invocation invocation{*slot}; Context context(*slot->heap, slot->execution_limit);
                ResultHandleReservation capture{*slot->heap};
                cilica_v3_result completed{};
                switch (export_id) { {{dispatch}} }
                if (status != CILICA_V3_OK && status != CILICA_V3_EXECUTION_FAILED) return status;
                if (context.pending != 0) {
                    completed.value = {}; completed.fault_code = context.pending;
                    completed.method_token = context.method; completed.il_offset = context.il;
                    if (context.image != nullptr) for (std::size_t i = 0; i < 64 && context.image[i] != '\0'; ++i) completed.image_hash[i] = context.image[i];
                }
                uint64_t exception = 0;
                {{(contract.CaptureExceptions ? "if (captured != nullptr && context.pending != 0 && valid_exception(context, context.exception)\n    && (context.pending == 13u || context.exception->type == runtime_exception(context.pending)))\n    (void)capture.publish(context.exception, exception);" : "")}}
                *result = completed; if (captured != nullptr) *captured = exception; return status;
            }
            extern "C" uint32_t {{prefix}}_invoke(uint64_t runtime, uint32_t export_id, const cilica_v3_value* parameters, uint32_t count, cilica_v3_result* result) noexcept {
                return {{prefix}}_invoke_core(runtime, export_id, parameters, count, result, nullptr, nullptr, 0, false);
            }
            {{(hasRecords ? EmbeddingRecordAbi.Emit(prefix, contract.CaptureExceptions) : "")}}
            {{(contract.CaptureExceptions ? EmbeddingExceptionAbi.Emit(prefix, exceptions!, program.Flow.Types) : "")}}
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
