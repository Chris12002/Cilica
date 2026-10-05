using System;
using System.Globalization;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>A generated C header and C++ implementation for one explicit compiled export.</summary>
public sealed record EmbeddingCppArtifact(string SymbolPrefix, string Header, string Text);

/// <summary>
/// Versioned, bounded persistent instances around the existing native backend.
/// This first embedding profile admits only Int32 parameters and results.
/// </summary>
public static class EmbeddingCppEmitter
{
    public const string Profile = "embedding-int32-v1";

    public static EmbeddingCppArtifact EmitProgram(ObjectIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var native = ObjectCppEmitter.EmitProgram(program);
        return Wrap(native, program.Flow, locations: false);
    }

    public static EmbeddingCppArtifact EmitExceptions(ScalarManagedExceptionIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var native = ObjectCppEmitter.EmitExceptions(program);
        return Wrap(native, program.Flow.Flow.Flow.Flow.Flow, locations: true);
    }

    private static EmbeddingCppArtifact Wrap(CppArtifact native, ReferenceIrProgram flow, bool locations)
    {
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        if (entry.ReturnType != IrType.Int32 || entry.Parameters.Any(p => p != IrType.Int32))
            throw new ArgumentException("Embedding v1 requires an Int32 result and only Int32 parameters.", nameof(flow));
        string prefix = CppEmitter.Symbol(flow.Entry) + "_abi_v1";
        string scope = CppEmitter.Symbol(flow.Entry) + "_objects_packed_closure";
        string guard = prefix.ToUpperInvariant() + "_H";
        string header = $$"""
            /* Cilica embedding-int32-v1. See docs/embedding-abi.md for ownership and limits. */
            #ifndef {{guard}}
            #define {{guard}}
            #include <stdint.h>
            #ifndef CILICA_EMBEDDING_V1_TYPES
            #define CILICA_EMBEDDING_V1_TYPES
            #define CILICA_V1_OK UINT32_C(0)
            #define CILICA_V1_INVALID_ARGUMENT UINT32_C(1)
            #define CILICA_V1_UNSUPPORTED_VERSION UINT32_C(2)
            #define CILICA_V1_INVALID_RUNTIME UINT32_C(3)
            #define CILICA_V1_BUSY UINT32_C(4)
            #define CILICA_V1_LIMIT UINT32_C(5)
            #define CILICA_V1_EXECUTION_FAILED UINT32_C(6)
            #define CILICA_V1_ALLOCATION_FAILED UINT32_C(7)
            #define CILICA_V1_COLLECT_BEFORE_ALLOCATION UINT32_C(1)
            typedef struct cilica_v1_result {
                int32_t value;
                uint32_t fault_code;
                uint32_t method_token;
                uint32_t il_offset;
                char image_hash[65];
            } cilica_v1_result;
            #ifdef __cplusplus
            #define CILICA_V1_NOEXCEPT noexcept
            #else
            #define CILICA_V1_NOEXCEPT
            #endif
            #define CILICA_V1_API __attribute__((visibility("default")))
            #endif
            #ifdef __cplusplus
            extern "C" {
            #endif
            CILICA_V1_API uint32_t {{prefix}}_version(void) CILICA_V1_NOEXCEPT;
            CILICA_V1_API uint32_t {{prefix}}_create(uint32_t version, uint32_t flags, uint64_t* runtime) CILICA_V1_NOEXCEPT;
            CILICA_V1_API uint32_t {{prefix}}_invoke(uint64_t runtime, const int32_t* arguments, uint32_t count, cilica_v1_result* result) CILICA_V1_NOEXCEPT;
            CILICA_V1_API uint32_t {{prefix}}_destroy(uint64_t runtime) CILICA_V1_NOEXCEPT;
            #ifdef __cplusplus
            }
            #endif
            #endif
            """;
        string argumentType = locations ? "LocationArgument" : "Argument";
        string count = entry.Parameters.Length.ToString(CultureInfo.InvariantCulture);
        string implementation = $$"""
            namespace {{prefix}}_private {
            using namespace {{scope}};
            struct Slot { std::uint64_t token = 0; Instance* heap = nullptr; bool busy = false; };
            static std::array<Slot, 64> slots{};
            static std::uint64_t next_token = 1;
            static std::atomic_flag registry_lock = ATOMIC_FLAG_INIT;
            // Only the tiny registry transition is locked. Managed work, allocation
            // and teardown happen outside it; independent instances can execute together.
            struct RegistryGuard {
                RegistryGuard() noexcept { while (registry_lock.test_and_set(std::memory_order_acquire)) {} }
                ~RegistryGuard() noexcept { registry_lock.clear(std::memory_order_release); }
                RegistryGuard(const RegistryGuard&) = delete;
                RegistryGuard& operator=(const RegistryGuard&) = delete;
            };
            static Slot* find(std::uint64_t token) noexcept {
                if (token != 0) for (auto& slot : slots) if (slot.token == token) return &slot;
                return nullptr;
            }
            struct Invocation {
                Slot& slot;
                ~Invocation() noexcept { RegistryGuard guard; slot.busy = false; }
            };
            }
            extern "C" uint32_t {{prefix}}_version(void) noexcept { return 1; }
            extern "C" uint32_t {{prefix}}_create(uint32_t version, uint32_t flags, uint64_t* runtime) noexcept {
                if (runtime == nullptr || (flags & ~CILICA_V1_COLLECT_BEFORE_ALLOCATION) != 0) return CILICA_V1_INVALID_ARGUMENT;
                if (version != 1) return CILICA_V1_UNSUPPORTED_VERSION;
                using namespace {{prefix}}_private;
                auto* heap = new (std::nothrow) Instance;
                if (heap == nullptr) return CILICA_V1_ALLOCATION_FAILED;
                heap->collect_before_allocation = (flags & CILICA_V1_COLLECT_BEFORE_ALLOCATION) != 0;
                {
                    RegistryGuard guard;
                    if (next_token != std::numeric_limits<std::uint64_t>::max()) {
                        for (auto& slot : slots) if (slot.token == 0) {
                            slot = {next_token++, heap, false};
                            *runtime = slot.token;
                            return CILICA_V1_OK;
                        }
                    }
                }
                delete heap;
                return CILICA_V1_LIMIT;
            }
            extern "C" uint32_t {{prefix}}_destroy(uint64_t runtime) noexcept {
                using namespace {{prefix}}_private;
                Instance* heap;
                {
                    RegistryGuard guard;
                    auto* slot = find(runtime);
                    if (slot == nullptr) return CILICA_V1_INVALID_RUNTIME;
                    if (slot->busy) return CILICA_V1_BUSY;
                    heap = slot->heap; *slot = {};
                }
                delete heap;
                return CILICA_V1_OK;
            }
            extern "C" uint32_t {{prefix}}_invoke(uint64_t runtime, const int32_t* parameters, uint32_t count, cilica_v1_result* result) noexcept {
                if (result == nullptr || count != {{count}}u || (count != 0 && parameters == nullptr)) return CILICA_V1_INVALID_ARGUMENT;
                using namespace {{prefix}}_private;
                Slot* slot;
                {
                    RegistryGuard guard;
                    slot = find(runtime);
                    if (slot == nullptr) return CILICA_V1_INVALID_RUNTIME;
                    if (slot->busy) return CILICA_V1_BUSY;
                    slot->busy = true;
                }
                // Keep the slot busy until the context, roots and arena have gone away.
                Invocation invocation{*slot};
                Context context(*slot->heap);
                cilica_v1_result completed{};
                {
                    Storage<std::array<{{argumentType}}, {{count}}>> arguments(context);
                    if (arguments.value == nullptr) {
                        context.pending = 9u;
                        context.image = "{{entry.Source.ContentHash.ToLowerInvariant()}}";
                        context.method = {{entry.Source.Token.ToString(CultureInfo.InvariantCulture)}}u;
                        context.il = {{entry.Blocks[0].ILOffset.ToString(CultureInfo.InvariantCulture)}}u;
                    } else {
                        for (std::size_t i = 0; i < count; ++i) (*arguments.value)[i].bits = std::bit_cast<std::uint32_t>(parameters[i]);
                        const auto value = {{CppEmitter.Symbol(flow.Entry)}}(context, arguments.value->data());
                        if (context.pending == 0) completed.value = value;
                    }
                }
                completed.fault_code = context.pending;
                completed.method_token = context.method;
                completed.il_offset = context.il;
                if (context.image != nullptr) for (std::size_t i = 0; i < 64 && context.image[i] != '\0'; ++i) completed.image_hash[i] = context.image[i];
                *result = completed;
                return context.pending == 0 ? CILICA_V1_OK : CILICA_V1_EXECUTION_FAILED;
            }
            """;
        return new(prefix, header + "\n", header + "\n" + native.Text + "\n" + implementation + "\n");
    }
}
