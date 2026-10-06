using System.Globalization;
using System.Linq;

namespace Cilica.Compiler;

internal static class EmbeddingObjectAbi
{
    internal const string Runtime = """
        static uint32_t enter(uint64_t runtime, Slot*& slot) noexcept {
            RegistryGuard guard; slot = find(runtime);
            if (slot == nullptr) return CILICA_V3_INVALID_RUNTIME;
            if (slot->busy) return CILICA_V3_BUSY;
            slot->busy = true; return CILICA_V3_OK;
        }
        static uint32_t handle_status(HandleStatus status) noexcept {
            switch (status) {
                case HandleStatus::complete: return CILICA_V3_OK;
                case HandleStatus::limit: return CILICA_V3_LIMIT;
                case HandleStatus::allocation_failed: return CILICA_V3_ALLOCATION_FAILED;
                case HandleStatus::busy: return CILICA_V3_BUSY;
                default: return CILICA_V3_INVALID_HANDLE;
            }
        }
        struct HostParameter { uint32_t kind; bool (*accepts)(const Descriptor*) noexcept; };
        static uint32_t validate_arguments(Instance& heap, const cilica_v3_value* parameters, const HostParameter* schema, std::size_t count) noexcept {
            for (std::size_t i = 0; i < count; ++i) {
                const auto& value = parameters[i];
                if (value.reserved != 0 || value.kind != schema[i].kind) return CILICA_V3_INVALID_ARGUMENT;
                if (value.kind == 1u) {
                    if (value.payload > UINT32_MAX) return CILICA_V3_INVALID_ARGUMENT;
                } else if (value.kind == 2u && value.payload != 0) {
                    const auto resolved = resolve_handle(heap, {value.payload});
                    if (resolved.status != HandleStatus::complete) return handle_status(resolved.status);
                    if (resolved.value != nullptr && !schema[i].accepts(resolved.value->type)) return CILICA_V3_TYPE_MISMATCH;
                }
            }
            return CILICA_V3_OK;
        }
        struct ResultHandleReservation {
            Instance& heap; Handle token{};
            explicit ResultHandleReservation(Instance& owner) noexcept : heap(owner) {}
            ResultHandleReservation(const ResultHandleReservation&) = delete;
            ResultHandleReservation& operator=(const ResultHandleReservation&) = delete;
            uint32_t reserve() noexcept {
                const auto result = retain_handle(heap, nullptr);
                if (result.status == HandleStatus::complete) token = result.handle;
                return handle_status(result.status);
            }
            uint32_t publish(Object* value, uint64_t& output) noexcept {
                // No managed allocation or safepoint may occur between the return and
                // publication: the callee's result root has already unwound.
                if (value == nullptr) { output = 0; return CILICA_V3_OK; }
                const auto status = publish_handle(heap, token, value);
                if (status == HandleStatus::complete) { output = token.token; token = {}; }
                return handle_status(status);
            }
            ~ResultHandleReservation() noexcept { if (token.token != 0) (void)release_handle(heap, token); }
        };
        """;

    internal static string Header(string prefix, string hash, uint schemaVersion, EmbeddingObjectExport[] exports, EmbeddingReferenceBinding[] references, bool utf16, bool captureExceptions, EmbeddingRecordPlan.Record[] records)
    {
        string macro = prefix.ToUpperInvariant();
        return $$"""
            /* Cilica embedding-object-handles-v3. See docs/embedding-abi.md. */
            #ifndef {{macro}}_H
            #define {{macro}}_H
            #include <stdint.h>
            #ifndef CILICA_EMBEDDING_V3_TYPES
            #define CILICA_EMBEDDING_V3_TYPES
            #define CILICA_V3_OK UINT32_C(0)
            #define CILICA_V3_INVALID_ARGUMENT UINT32_C(1)
            #define CILICA_V3_UNSUPPORTED_VERSION UINT32_C(2)
            #define CILICA_V3_INVALID_RUNTIME UINT32_C(3)
            #define CILICA_V3_BUSY UINT32_C(4)
            #define CILICA_V3_LIMIT UINT32_C(5)
            #define CILICA_V3_EXECUTION_FAILED UINT32_C(6)
            #define CILICA_V3_ALLOCATION_FAILED UINT32_C(7)
            #define CILICA_V3_UNKNOWN_EXPORT UINT32_C(8)
            #define CILICA_V3_CONTRACT_MISMATCH UINT32_C(9)
            #define CILICA_V3_INVALID_HANDLE UINT32_C(10)
            #define CILICA_V3_TYPE_MISMATCH UINT32_C(11)
            #define CILICA_V3_VOID UINT32_C(0)
            #define CILICA_V3_INT32 UINT32_C(1)
            #define CILICA_V3_HANDLE UINT32_C(2)
            #define CILICA_V3_COLLECT_BEFORE_ALLOCATION UINT32_C(1)
            #define CILICA_V3_CAP_INT32 UINT64_C(1)
            #define CILICA_V3_CAP_PERSISTENT UINT64_C(2)
            #define CILICA_V3_CAP_MULTI_EXPORT UINT64_C(4)
            #define CILICA_V3_CAP_COPIED_FAULT UINT64_C(8)
            #define CILICA_V3_CAP_GC_STRESS UINT64_C(16)
            #define CILICA_V3_CAP_QUOTAS UINT64_C(32)
            #define CILICA_V3_CAP_HANDLES UINT64_C(64)
            #define CILICA_V3_CAP_VOID UINT64_C(128)
            typedef struct cilica_v3_config {
                uint32_t struct_size, flags, heap_bytes, object_limit, execution_bytes, handle_limit;
            } cilica_v3_config;
            #define CILICA_V3_CONFIG_INIT {sizeof(cilica_v3_config), 0, 1048576, 4096, 1048576, 1024}
            typedef struct cilica_v3_value { uint32_t kind, reserved; uint64_t payload; } cilica_v3_value;
            typedef struct cilica_v3_result {
                cilica_v3_value value;
                uint32_t fault_code, method_token, il_offset;
                char image_hash[65];
            } cilica_v3_result;
            #ifdef __cplusplus
            #define CILICA_V3_NOEXCEPT noexcept
            #else
            #define CILICA_V3_NOEXCEPT
            #endif
            #define CILICA_V3_API __attribute__((visibility("default")))
            #endif
            /* Additive constants also work when an older v3 header came first. */
            #ifndef CILICA_V3_BUFFER_TOO_SMALL
            #define CILICA_V3_BUFFER_TOO_SMALL UINT32_C(12)
            #endif
            #ifndef CILICA_V3_CAP_UTF16
            #define CILICA_V3_CAP_UTF16 UINT64_C(256)
            #endif
            #ifndef CILICA_V3_INTEGER64
            #define CILICA_V3_INTEGER64 UINT32_C(3)
            #endif
            #ifndef CILICA_V3_CAP_INTEGER64
            #define CILICA_V3_CAP_INTEGER64 UINT64_C(512)
            #endif
            {{(captureExceptions ? EmbeddingExceptionAbi.Types : "")}}
            {{(records.Length != 0 ? EmbeddingRecordAbi.Types : "")}}
            #define {{macro}}_CONTRACT_HASH "{{hash}}"
            #define {{macro}}_SCHEMA_VERSION UINT32_C({{schemaVersion.ToString(CultureInfo.InvariantCulture)}})
            {{string.Join("\n", exports.Select(e => "#define " + macro + "_EXPORT_" + e.Name.ToUpperInvariant() + " UINT32_C(" + e.Id.ToString(CultureInfo.InvariantCulture) + ")"))}}
            {{string.Join("\n", references.Select(r => "#define " + macro + "_TYPE_" + r.Name.ToUpperInvariant() + " UINT32_C(" + r.Id.ToString(CultureInfo.InvariantCulture) + ")"))}}
            {{string.Join("\n", records.Select(r => "#define " + macro + "_RECORD_" + r.Binding.Name.ToUpperInvariant() + " UINT32_C(" + r.Binding.Id.ToString(CultureInfo.InvariantCulture) + ")"))}}
            #ifdef __cplusplus
            extern "C" {
            #endif
            CILICA_V3_API uint32_t {{prefix}}_version(void) CILICA_V3_NOEXCEPT;
            CILICA_V3_API uint64_t {{prefix}}_capabilities(void) CILICA_V3_NOEXCEPT;
            CILICA_V3_API uint32_t {{prefix}}_create(uint32_t version, const char* contract_hash, const cilica_v3_config* config, uint64_t* runtime) CILICA_V3_NOEXCEPT;
            CILICA_V3_API uint32_t {{prefix}}_invoke(uint64_t runtime, uint32_t export_id, const cilica_v3_value* arguments, uint32_t count, cilica_v3_result* result) CILICA_V3_NOEXCEPT;
            CILICA_V3_API uint32_t {{prefix}}_retain_handle(uint64_t runtime, uint64_t handle, uint64_t* retained) CILICA_V3_NOEXCEPT;
            CILICA_V3_API uint32_t {{prefix}}_release_handle(uint64_t runtime, uint64_t handle) CILICA_V3_NOEXCEPT;
            CILICA_V3_API uint32_t {{prefix}}_destroy(uint64_t runtime) CILICA_V3_NOEXCEPT;
            {{(utf16 ? EmbeddingStringAbi.Declarations(prefix) : "")}}
            {{(captureExceptions ? EmbeddingExceptionAbi.Declarations(prefix) : "")}}
            {{(records.Length != 0 ? EmbeddingRecordAbi.Declaration(prefix) : "")}}
            #ifdef __cplusplus
            }
            #endif
            #endif

            """;
    }
}
