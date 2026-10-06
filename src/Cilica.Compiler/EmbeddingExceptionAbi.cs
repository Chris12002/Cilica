using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Optional retained exception capture and caller-owned UTF-16 detail copying.</summary>
internal static class EmbeddingExceptionAbi
{
    internal const string Types = """
        #ifndef CILICA_EMBEDDING_V3_EXCEPTION_TYPES
        #define CILICA_EMBEDDING_V3_EXCEPTION_TYPES
        #define CILICA_V3_CAP_EXCEPTION_CAPTURE UINT64_C(1024)
        typedef struct cilica_v3_exception_info {
            uint32_t type_name_length, message_length, type_token;
            char type_image_hash[65];
        } cilica_v3_exception_info;
        #endif
        """;

    internal static string Declarations(string prefix) => $$"""
        CILICA_V3_API uint32_t {{prefix}}_invoke_capture(uint64_t runtime, uint32_t export_id, const cilica_v3_value* arguments, uint32_t count, cilica_v3_result* result, uint64_t* exception) CILICA_V3_NOEXCEPT;
        CILICA_V3_API uint32_t {{prefix}}_exception_copy(uint64_t runtime, uint64_t exception, uint16_t* type_name, uint32_t type_capacity, uint16_t* message, uint32_t message_capacity, cilica_v3_exception_info* info) CILICA_V3_NOEXCEPT;
        """;

    internal static string Emit(string prefix, ManagedExceptionTypes exceptions, ObjectTypeCatalog catalog)
    {
        var types = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var names = new StringBuilder(); var entries = new StringBuilder();
        for (int i = 0; i < exceptions.Types.Length; ++i) {
            var type = exceptions.Types[i]; var source = types[type].Source;
            names.Append(CultureInfo.InvariantCulture, $"static constexpr std::uint16_t captured_name_{i}[] = {{")
                .AppendJoin(", ", source.Name.Select(c => ((ushort)c).ToString(CultureInfo.InvariantCulture))).Append("};\n");
            entries.Append(CultureInfo.InvariantCulture, $"{{&{ObjectCppEmitter.ReferenceDescriptor(type)}, captured_name_{i}, {source.Name.Length}u, {source.Token}u, \"{source.ContentHash.ToLowerInvariant()}\"}},\n");
        }
        return $$"""
            namespace {{prefix}}_private {
            {{names}}
            struct CapturedExceptionType { const Descriptor* type; const uint16_t* name; uint32_t length, token; const char* image; };
            static constexpr CapturedExceptionType captured_exception_types[] = { {{entries}} };
            }
            extern "C" uint32_t {{prefix}}_invoke_capture(uint64_t runtime, uint32_t export_id, const cilica_v3_value* parameters, uint32_t count, cilica_v3_result* result, uint64_t* exception) noexcept {
                if (exception == nullptr) return CILICA_V3_INVALID_ARGUMENT;
                return {{prefix}}_invoke_core(runtime, export_id, parameters, count, result, exception, nullptr, 0, false);
            }
            extern "C" uint32_t {{prefix}}_exception_copy(uint64_t runtime, uint64_t exception, uint16_t* type_name, uint32_t type_capacity, uint16_t* message, uint32_t message_capacity, cilica_v3_exception_info* info) noexcept {
                if (info == nullptr || (type_capacity != 0 && type_name == nullptr) || (message_capacity != 0 && message == nullptr)) return CILICA_V3_INVALID_ARGUMENT;
                using namespace {{prefix}}_private;
                Slot* slot; const auto status = enter(runtime, slot);
                if (status != CILICA_V3_OK) return status;
                Invocation invocation{*slot}; Context context(*slot->heap, slot->execution_limit);
                const auto resolved = resolve_handle(*slot->heap, {exception});
                if (resolved.status != HandleStatus::complete) return handle_status(resolved.status);
                if (!valid_exception(context, resolved.value)) return CILICA_V3_TYPE_MISMATCH;
                const CapturedExceptionType* declaration = nullptr;
                for (const auto& entry : captured_exception_types) if (entry.type == resolved.value->type) { declaration = &entry; break; }
                if (declaration == nullptr) return CILICA_V3_TYPE_MISMATCH;
                // The incoming lease roots the exception and all its payloads while
                // the existing runtime formats its message, which may collect.
                const auto formatted = exception_message(context, resolved.value);
                if (formatted.status != 0) return formatted.status == 5u ? CILICA_V3_ALLOCATION_FAILED : CILICA_V3_EXECUTION_FAILED;
                if (formatted.value == nullptr || !traceable_reference(context.instance, formatted.value) || !string_type(formatted.value->type)) return CILICA_V3_EXECUTION_FAILED;
                // A fresh message can be unrooted here. Do not allocate or collect
                // between its return and completion of the host copy.
                const auto* text = reinterpret_cast<const StringObject*>(formatted.value);
                cilica_v3_exception_info completed{};
                completed.type_name_length = declaration->length; completed.message_length = text->length; completed.type_token = declaration->token;
                for (std::size_t i = 0; i < 64; ++i) completed.type_image_hash[i] = declaration->image[i];
                if (type_capacity < declaration->length || message_capacity < text->length) {
                    *info = completed; return CILICA_V3_BUFFER_TOO_SMALL;
                }
                for (uint32_t i = 0; i < declaration->length; ++i) type_name[i] = declaration->name[i];
                for (uint32_t i = 0; i < text->length; ++i) message[i] = text->units[i];
                *info = completed; return CILICA_V3_OK;
            }
            """;
    }
}
