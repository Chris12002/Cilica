using System.Globalization;

namespace Cilica.Compiler;

/// <summary>Common create/destroy contract; versions choose their public config and capabilities.</summary>
internal static class EmbeddingModuleLifecycle
{
    internal static string Emit(string prefix, uint version, string hash, bool handles, bool utf16 = false)
    {
        string v = version.ToString(CultureInfo.InvariantCulture), macro = "CILICA_V" + v;
        return $$"""
            extern "C" uint32_t {{prefix}}_version(void) noexcept { return {{v}}; }
            extern "C" uint64_t {{prefix}}_capabilities(void) noexcept { return UINT64_C({{(utf16 ? "511" : handles ? "255" : "63")}}); }
            extern "C" uint32_t {{prefix}}_create(uint32_t version, const char* contract_hash, const cilica_v{{v}}_config* config, uint64_t* runtime) noexcept {
                if (runtime == nullptr || contract_hash == nullptr) return {{macro}}_INVALID_ARGUMENT;
                if (version != {{v}}) return {{macro}}_UNSUPPORTED_VERSION;
                // The caller supplies 65 readable bytes, including the terminator.
                static constexpr char expected[] = "{{hash}}";
                for (std::size_t i = 0; i < sizeof(expected); ++i)
                    if (contract_hash[i] != expected[i]) return {{macro}}_CONTRACT_MISMATCH;
                cilica_v{{v}}_config selected{sizeof(cilica_v{{v}}_config), 0, 1048576, 4096, 1048576{{(handles ? ", 1024" : "")}}};
                if (config != nullptr) {
                    if (config->struct_size != sizeof(cilica_v{{v}}_config)) return {{macro}}_INVALID_ARGUMENT;
                    selected = *config;
                    if ((selected.flags & ~{{macro}}_COLLECT_BEFORE_ALLOCATION) != 0
                        || selected.heap_bytes > 1048576 || selected.object_limit > 4096 || selected.execution_bytes > 1048576
                        {{(handles ? "|| selected.handle_limit > 1024" : "")}})
                        return {{macro}}_INVALID_ARGUMENT;
                }
                using namespace {{prefix}}_private;
                auto* heap = new (std::nothrow) Instance;
                if (heap == nullptr) return {{macro}}_ALLOCATION_FAILED;
                heap->collect_before_allocation = (selected.flags & {{macro}}_COLLECT_BEFORE_ALLOCATION) != 0;
                heap->limit = selected.heap_bytes; heap->object_limit = selected.object_limit;
                {{(handles ? "heap->handle_limit = selected.handle_limit;" : "")}}
                {
                    RegistryGuard guard;
                    if (next_token != std::numeric_limits<std::uint64_t>::max()) {
                        for (auto& slot : slots) if (slot.token == 0) {
                            slot = {next_token++, heap, false, selected.execution_bytes};
                            *runtime = slot.token; return {{macro}}_OK;
                        }
                    }
                }
                delete heap; return {{macro}}_LIMIT;
            }
            extern "C" uint32_t {{prefix}}_destroy(uint64_t runtime) noexcept {
                using namespace {{prefix}}_private;
                Instance* heap;
                {
                    RegistryGuard guard;
                    auto* slot = find(runtime);
                    if (slot == nullptr) return {{macro}}_INVALID_RUNTIME;
                    if (slot->busy) return {{macro}}_BUSY;
                    heap = slot->heap; *slot = {};
                }
                delete heap; return {{macro}}_OK;
            }
            """;
    }
}
