namespace Cilica.Compiler;

/// <summary>Private instance-owned strong handles; no public embedding ABI.</summary>
internal static class ObjectCppHandles
{
    internal const string Text = """
enum class HandleStatus { complete, invalid, limit, allocation_failed, busy };
struct HandleResult { HandleStatus status; Handle handle{}; };
struct HandleResolution { HandleStatus status; Object* value = nullptr; };
// Shared by instances of this compiled closure, including successive lifetimes.
// Relaxed atomics make issuance safe between otherwise independent instances.
// Exhaustion is permanent: token values never wrap or become valid again.
static std::atomic<std::uint64_t> next_handle_token{1};
[[gnu::noinline]] static std::uint64_t issue_handle_token() noexcept {
    auto token = next_handle_token.load(std::memory_order_relaxed);
    while (token != std::numeric_limits<std::uint64_t>::max()) {
        if (next_handle_token.compare_exchange_weak(token, token + 1, std::memory_order_relaxed)) return token;
    }
    return 0;
}
[[maybe_unused, gnu::noinline]] static HandleResult retain_handle(Instance& heap, Object* value) noexcept {
    if (heap.marking) return {HandleStatus::busy};
    if (!traceable_reference(heap, value)) return {HandleStatus::invalid};
    if (heap.handle_count >= heap.handle_limit || heap.handle_count >= handle_capacity) return {HandleStatus::limit};
    if (heap.handles == nullptr) {
        heap.handles = new (std::nothrow) HandleSlot[handle_capacity]{};
        if (heap.handles == nullptr) return {HandleStatus::allocation_failed};
    }
    for (std::size_t i = 0; i < handle_capacity; ++i) if (heap.handles[i].token == 0) {
        const auto token = issue_handle_token();
        if (token == 0) return {HandleStatus::limit};
        heap.handles[i] = {value, token}; ++heap.handle_count;
        return {HandleStatus::complete, {token}};
    }
    return {HandleStatus::limit};
}
[[maybe_unused, gnu::noinline]] static HandleResolution resolve_handle(Instance& heap, Handle handle) noexcept {
    if (heap.marking) return {HandleStatus::busy};
    if (handle.token == 0 || heap.handles == nullptr) return {HandleStatus::invalid};
    for (std::size_t i = 0; i < handle_capacity; ++i) if (heap.handles[i].token == handle.token) {
        if (!traceable_reference(heap, heap.handles[i].value)) return {HandleStatus::invalid};
        return {HandleStatus::complete, heap.handles[i].value};
    }
    return {HandleStatus::invalid};
}
[[maybe_unused, gnu::noinline]] static HandleStatus release_handle(Instance& heap, Handle handle) noexcept {
    if (heap.marking) return HandleStatus::busy;
    if (handle.token == 0 || heap.handles == nullptr) return HandleStatus::invalid;
    for (std::size_t i = 0; i < handle_capacity; ++i) if (heap.handles[i].token == handle.token) {
        heap.handles[i] = {}; --heap.handle_count;
        return HandleStatus::complete;
    }
    return HandleStatus::invalid;
}
""";
}
