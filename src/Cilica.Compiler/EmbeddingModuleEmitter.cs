using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cilica.Compiler;

/// <summary>An application-owned ID/name and expected scalar arity bound to an exact compiled method.</summary>
public sealed record EmbeddingExport(uint Id, string Name, IrMethodId Method, int ParameterCount);
public sealed record EmbeddingModuleContract(string Name, uint SchemaVersion, ImmutableArray<EmbeddingExport> Exports);
public sealed record EmbeddingModuleArtifact(string SymbolPrefix, string Header, string Text, string ManifestJson, string ContractHash);

/// <summary>Exports selected public static Int32 methods from ONE validated closure and shared runtime.</summary>
public static class EmbeddingModuleEmitter
{
    public const string Profile = "embedding-module-int32-v2";
    public const int MaximumExports = 64;

    public static EmbeddingModuleArtifact EmitProgram(ObjectIrProgram program, EmbeddingModuleContract contract)
    {
        ArgumentNullException.ThrowIfNull(program);
        var native = ObjectCppEmitter.EmitProgram(program);
        return Wrap(native, program, contract, locations: false);
    }

    public static EmbeddingModuleArtifact EmitExceptions(ManagedExceptionIrProgram program, EmbeddingModuleContract contract)
    {
        ArgumentNullException.ThrowIfNull(program);
        _ = ValueRootAssignment.Assign(program);
        // Validate the logical signature before value lowering can flatten a struct
        // into scalar leaves that happen to look like an Int32 host signature.
        _ = ValidateContract(program.Flow.Flow.Flow.Methods, program.Flow.Bindings, contract);
        var lowered = ValueScalarLowering.LowerExceptions(program);
        var native = ObjectCppEmitter.EmitExceptions(lowered);
        return Wrap(native, lowered.Flow.Flow.Flow.Flow, contract, locations: true);
    }

    private static EmbeddingExport[] ValidateContract(ImmutableArray<IrMethod> bodies, ImmutableArray<ObjectMethodBinding> declarations, EmbeddingModuleContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!ValidName(contract.Name) || contract.SchemaVersion == 0 || contract.Exports.IsDefaultOrEmpty || contract.Exports.Length > MaximumExports)
            throw new ArgumentException("A module needs a lowercase ASCII name, nonzero schema version and 1..64 exports.", nameof(contract));
        var methods = bodies.ToDictionary(m => IrMethodId.From(m.Source));
        var bindings = declarations.ToDictionary(b => b.Method);
        if (contract.Exports.Any(e => e is null || e.Id == 0 || !ValidName(e.Name) || e.Method is null)
            || contract.Exports.Select(e => e.Id).Distinct().Count() != contract.Exports.Length
            || contract.Exports.Select(e => e.Name).Distinct(StringComparer.Ordinal).Count() != contract.Exports.Length
            || contract.Exports.Select(e => e.Method).Distinct().Count() != contract.Exports.Length)
            throw new ArgumentException("Export IDs, names and methods must be nonempty, valid and unique; ID zero is reserved.", nameof(contract));
        var exports = contract.Exports.OrderBy(e => e.Id).ToArray();
        foreach (var export in exports)
        {
            if (!methods.TryGetValue(export.Method, out var method) || !bindings.TryGetValue(export.Method, out var binding))
                throw new ArgumentException("Export '" + export.Name + "' is outside this compiled closure.", nameof(contract));
            if (binding.Kind != ObjectMethodKind.Static || binding.TypeInitializerName is not null
                || !binding.OwnerExternallyVisible || (binding.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public
                || (binding.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) != 0
                || method.ReturnType != IrType.Int32 || method.Parameters.Any(p => p != IrType.Int32)
                || method.Parameters.Length != export.ParameterCount)
                throw new ArgumentException("Export '" + export.Name + "' requires a public static Int32 method on a public owner with the declared Int32 arity.", nameof(contract));
        }
        return exports;
    }

    private static EmbeddingModuleArtifact Wrap(CppArtifact native, ObjectIrProgram program, EmbeddingModuleContract contract, bool locations)
    {
        var exports = ValidateContract(program.Flow.Methods, program.Bindings, contract);
        var methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        // The compatibility fingerprint deliberately excludes build identity and method
        // tokens: rebuilding unchanged application contracts must not renumber exports.
        var schema = new {
            abiVersion = 2, name = contract.Name, schemaVersion = contract.SchemaVersion,
            exports = exports.Select(e => new { id = e.Id, name = e.Name,
                parameters = Enumerable.Repeat("int32", e.ParameterCount).ToArray(), result = "int32" }).ToArray()
        };
        string schemaJson = JsonSerializer.Serialize(schema);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(schemaJson)));
        string manifest = JsonSerializer.Serialize(new {
            profile = Profile, contractHash = hash, contract = schema,
            bindings = exports.Select(e => new { id = e.Id, imageHash = e.Method.ContentHash,
                methodToken = e.Method.Token, specialization = e.Method.Specialization }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        string prefix = "cilica_" + contract.Name + "_abi_v2";
        string macro = prefix.ToUpperInvariant();
        string scope = CppEmitter.Symbol(program.Flow.Entry) + "_objects_packed_closure";
        string packet = locations ? "LocationArgument" : "Argument";
        string header = Header(prefix, macro, hash, contract.SchemaVersion, exports);
        var dispatch = new StringBuilder();
        var helpers = new StringBuilder();
        var arities = new StringBuilder();
        foreach (var export in exports)
        {
            var method = methods[export.Method];
            arities.Append(CultureInfo.InvariantCulture, $"case {export.Id}u: arity = {export.ParameterCount}u; break;\n");
            dispatch.Append(CultureInfo.InvariantCulture, $"case {export.Id}u: completed.value = invoke_{export.Id}(context, parameters); break;\n");
            helpers.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static std::int32_t invoke_{{export.Id}}(Context& context, [[maybe_unused]] const std::int32_t* parameters) noexcept {
                    Storage<std::array<{{packet}}, {{export.ParameterCount}}>> arguments(context);
                    if (arguments.value == nullptr) {
                        context.pending = 9u; context.image = "{{method.Source.ContentHash.ToLowerInvariant()}}";
                        context.method = {{method.Source.Token}}u; context.il = {{method.Blocks[0].ILOffset}}u;
                        return 0;
                    }
                    for (std::size_t i = 0; i < {{export.ParameterCount}}u; ++i) (*arguments.value)[i].bits = std::bit_cast<std::uint32_t>(parameters[i]);
                    return {{CppEmitter.Symbol(export.Method)}}(context, arguments.value->data());
                }
                """);
        }
        string implementation = $$"""
            namespace {{prefix}}_private {
            using namespace {{scope}};
            {{EmbeddingCppRegistry.Text}}
            {{helpers}}
            }
            {{EmbeddingModuleLifecycle.Emit(prefix, 2, hash, handles: false)}}
            extern "C" uint32_t {{prefix}}_invoke(uint64_t runtime, uint32_t export_id, const int32_t* parameters, uint32_t count, cilica_v2_result* result) noexcept {
                if (result == nullptr) return CILICA_V2_INVALID_ARGUMENT;
                std::uint32_t arity;
                switch (export_id) {
                    {{arities}}
                    default: return CILICA_V2_UNKNOWN_EXPORT;
                }
                if (count != arity || (count != 0 && parameters == nullptr)) return CILICA_V2_INVALID_ARGUMENT;
                using namespace {{prefix}}_private;
                Slot* slot;
                {
                    RegistryGuard guard;
                    slot = find(runtime);
                    if (slot == nullptr) return CILICA_V2_INVALID_RUNTIME;
                    if (slot->busy) return CILICA_V2_BUSY;
                    slot->busy = true;
                }
                Invocation invocation{*slot};
                Context context(*slot->heap, slot->execution_limit);
                cilica_v2_result completed{};
                switch (export_id) { {{dispatch}} }
                if (context.pending != 0) {
                    completed.value = 0;
                    completed.fault_code = context.pending;
                    completed.method_token = context.method; completed.il_offset = context.il;
                    if (context.image != nullptr) for (std::size_t i = 0; i < 64 && context.image[i] != '\0'; ++i) completed.image_hash[i] = context.image[i];
                }
                *result = completed;
                return context.pending == 0 ? CILICA_V2_OK : CILICA_V2_EXECUTION_FAILED;
            }
            """;
        return new(prefix, header, header + native.Text + "\n" + implementation + "\n", manifest, hash);
    }

    internal static bool ValidName(string? value) => value is { Length: > 0 and <= 48 }
        && value[0] is >= 'a' and <= 'z' && value[^1] != '_' && !value.Contains("__", StringComparison.Ordinal)
        && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    private static string Header(string prefix, string macro, string hash, uint schemaVersion, EmbeddingExport[] exports) => $$"""
        /* Cilica embedding-module-int32-v2. See docs/embedding-abi.md. */
        #ifndef {{macro}}_H
        #define {{macro}}_H
        #include <stdint.h>
        #ifndef CILICA_EMBEDDING_V2_TYPES
        #define CILICA_EMBEDDING_V2_TYPES
        #define CILICA_V2_OK UINT32_C(0)
        #define CILICA_V2_INVALID_ARGUMENT UINT32_C(1)
        #define CILICA_V2_UNSUPPORTED_VERSION UINT32_C(2)
        #define CILICA_V2_INVALID_RUNTIME UINT32_C(3)
        #define CILICA_V2_BUSY UINT32_C(4)
        #define CILICA_V2_LIMIT UINT32_C(5)
        #define CILICA_V2_EXECUTION_FAILED UINT32_C(6)
        #define CILICA_V2_ALLOCATION_FAILED UINT32_C(7)
        #define CILICA_V2_UNKNOWN_EXPORT UINT32_C(8)
        #define CILICA_V2_CONTRACT_MISMATCH UINT32_C(9)
        #define CILICA_V2_COLLECT_BEFORE_ALLOCATION UINT32_C(1)
        #define CILICA_V2_CAP_INT32 UINT64_C(1)
        #define CILICA_V2_CAP_PERSISTENT UINT64_C(2)
        #define CILICA_V2_CAP_MULTI_EXPORT UINT64_C(4)
        #define CILICA_V2_CAP_COPIED_FAULT UINT64_C(8)
        #define CILICA_V2_CAP_GC_STRESS UINT64_C(16)
        #define CILICA_V2_CAP_QUOTAS UINT64_C(32)
        typedef struct cilica_v2_config {
            uint32_t struct_size, flags, heap_bytes, object_limit, execution_bytes;
        } cilica_v2_config;
        #define CILICA_V2_CONFIG_INIT {sizeof(cilica_v2_config), 0, 1048576, 4096, 1048576}
        typedef struct cilica_v2_result {
            int32_t value;
            uint32_t fault_code, method_token, il_offset;
            char image_hash[65];
        } cilica_v2_result;
        #ifdef __cplusplus
        #define CILICA_V2_NOEXCEPT noexcept
        #else
        #define CILICA_V2_NOEXCEPT
        #endif
        #define CILICA_V2_API __attribute__((visibility("default")))
        #endif
        #define {{macro}}_CONTRACT_HASH "{{hash}}"
        #define {{macro}}_SCHEMA_VERSION UINT32_C({{schemaVersion.ToString(CultureInfo.InvariantCulture)}})
        {{string.Join("\n", exports.Select(e => "#define " + macro + "_EXPORT_" + e.Name.ToUpperInvariant() + " UINT32_C(" + e.Id.ToString(CultureInfo.InvariantCulture) + ")"))}}
        #ifdef __cplusplus
        extern "C" {
        #endif
        CILICA_V2_API uint32_t {{prefix}}_version(void) CILICA_V2_NOEXCEPT;
        CILICA_V2_API uint64_t {{prefix}}_capabilities(void) CILICA_V2_NOEXCEPT;
        CILICA_V2_API uint32_t {{prefix}}_create(uint32_t version, const char* contract_hash, const cilica_v2_config* config, uint64_t* runtime) CILICA_V2_NOEXCEPT;
        CILICA_V2_API uint32_t {{prefix}}_invoke(uint64_t runtime, uint32_t export_id, const int32_t* arguments, uint32_t count, cilica_v2_result* result) CILICA_V2_NOEXCEPT;
        CILICA_V2_API uint32_t {{prefix}}_destroy(uint64_t runtime) CILICA_V2_NOEXCEPT;
        #ifdef __cplusplus
        }
        #endif
        #endif

        """;
}
