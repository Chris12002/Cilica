namespace Cilica.Compiler;

/// <summary>Optional v3 copying interface: host buffers never become managed storage.</summary>
internal static class EmbeddingStringAbi
{
    internal static string Declarations(string prefix) => $$"""
        CILICA_V3_API uint32_t {{prefix}}_string_create(uint64_t runtime, const uint16_t* units, uint32_t length, uint64_t* handle) CILICA_V3_NOEXCEPT;
        CILICA_V3_API uint32_t {{prefix}}_string_copy(uint64_t runtime, uint64_t handle, uint16_t* units, uint32_t capacity, uint32_t* length) CILICA_V3_NOEXCEPT;
        """;

    internal static string Emit(string prefix, string descriptor) => $$"""
        extern "C" uint32_t {{prefix}}_string_create(uint64_t runtime, const uint16_t* units, uint32_t length, uint64_t* handle) noexcept {
            if (handle == nullptr || (length != 0 && units == nullptr)) return CILICA_V3_INVALID_ARGUMENT;
            if (length > INT32_MAX) return CILICA_V3_LIMIT;
            using namespace {{prefix}}_private;
            Slot* slot; const auto status = enter(runtime, slot);
            if (status != CILICA_V3_OK) return status;
            Invocation invocation{*slot}; Context context(*slot->heap, slot->execution_limit);
            ResultHandleReservation reservation{*slot->heap};
            const auto reserved = reservation.reserve();
            if (reserved != CILICA_V3_OK) return reserved;
            auto* value = allocate_string(context, {{descriptor}}, units, length, nullptr, 0);
            if (value == nullptr) return CILICA_V3_ALLOCATION_FAILED;
            // The only post-allocation step publishes into a pre-reserved root.
            return reservation.publish(value, *handle);
        }
        extern "C" uint32_t {{prefix}}_string_copy(uint64_t runtime, uint64_t handle, uint16_t* units, uint32_t capacity, uint32_t* length) noexcept {
            if (length == nullptr || (capacity != 0 && units == nullptr)) return CILICA_V3_INVALID_ARGUMENT;
            using namespace {{prefix}}_private;
            Slot* slot; const auto status = enter(runtime, slot);
            if (status != CILICA_V3_OK) return status;
            Invocation invocation{*slot};
            const auto resolved = resolve_handle(*slot->heap, {handle});
            if (resolved.status != HandleStatus::complete) return handle_status(resolved.status);
            if (resolved.value == nullptr || resolved.value->type != &{{descriptor}}) return CILICA_V3_TYPE_MISMATCH;
            const auto* text = reinterpret_cast<const StringObject*>(resolved.value);
            if (capacity < text->length) { *length = text->length; return CILICA_V3_BUFFER_TOO_SMALL; }
            for (std::uint32_t i = 0; i < text->length; ++i) units[i] = text->units[i];
            *length = text->length; return CILICA_V3_OK;
        }
        """;
}
