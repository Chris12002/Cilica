namespace Cilica.Compiler;

/// <summary>Shared private admission machinery for versioned embedding boundaries.</summary>
internal static class EmbeddingCppRegistry
{
    internal const string Text = """
            struct Slot { std::uint64_t token = 0; Instance* heap = nullptr; bool busy = false; std::size_t execution_limit = 1048576; };
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
            """;
}
