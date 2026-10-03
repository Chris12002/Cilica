namespace Cilica.Compiler;

/// <summary>Bounded allocation-free marking and scoped reachability diagnostics; no sweep.</summary>
internal static class ObjectCppMarking
{
    internal const string Text = """
enum class MarkStatus { complete, invalid, limit, stopped, busy };
struct MarkLimits {
    std::size_t objects = 4096;
    std::size_t edges = 262144;
};
struct MarkReport {
    MarkStatus status;
    // Counts are published only on complete success, including diagnostic delivery.
    std::size_t reachable = 0, unreachable = 0, edges = 0;
};
// Called once per retained allocation only after the entire reachable graph is
// validated. External bookkeeping is allowed; runtime state (including marking
// scratch) must remain unchanged. No managed allocation or execution during reporting.
using ReachabilityVisitor = bool (*)(void*, Object*, bool) noexcept;
struct MarkState {
    Instance& heap;
    const std::size_t edge_limit;
    Object* work = nullptr;
    std::size_t reachable = 0, edges = 0;
    bool prepared = false;
    MarkState(Instance& instance, std::size_t limit) noexcept : heap(instance), edge_limit(limit) { heap.marking = true; }
    MarkState(const MarkState&) = delete;
    MarkState& operator=(const MarkState&) = delete;
    ~MarkState() noexcept {
        // Topology was validated before any scratch write, and cannot change
        // during the pass. Never retain a mark snapshot across managed execution.
        if (prepared) for (Object* value = heap.head; value != nullptr; value = value->next) {
            value->marked = false; value->mark_next = nullptr;
        }
        heap.marking = false;
    }
};
[[maybe_unused, gnu::noinline]] static bool mark_edge(void* opaque, const ReferenceEdge& edge) noexcept {
    auto& state = *static_cast<MarkState*>(opaque);
    if (state.edges == state.edge_limit) return false;
    ++state.edges; // Nulls and aliases consume work as well as new discoveries.
    Object* value = edge.value; // The precise visitor already validated this value.
    if (value != nullptr && !value->marked) {
        value->marked = true; ++state.reachable;
        value->mark_next = state.work; state.work = value;
    }
    return true;
}
[[maybe_unused, gnu::noinline]] static MarkReport mark_reachable(Context& context, MarkLimits limits = {},
    ReachabilityVisitor visitor = nullptr, void* opaque = nullptr) noexcept {
    Instance& heap = context.instance;
    if (heap.marking) return {MarkStatus::busy};
    MarkState state(heap, limits.edges);
    if (heap.count > limits.objects) return {MarkStatus::limit};
    // Validate the entire allocation inventory, including unreachable headers,
    // before touching scratch state or trusting it for reachability reporting.
    std::size_t count = 0;
    for (Object* value = heap.head; value != nullptr; value = value->next) {
        if (count == heap.count || count == limits.objects) return {MarkStatus::invalid};
        ++count;
        if (value->owner != &heap || !known_type(value->type)) return {MarkStatus::invalid};
    }
    if (count != heap.count) return {MarkStatus::invalid};
    state.prepared = true;
    for (Object* value = heap.head; value != nullptr; value = value->next) {
        value->marked = false; value->mark_next = nullptr;
    }
    auto result = visit_roots(context, mark_edge, &state);
    if (result != TraceResult::complete) return {result == TraceResult::invalid ? MarkStatus::invalid : MarkStatus::limit};
    while (state.work != nullptr) {
        Object* value = state.work; state.work = value->mark_next; value->mark_next = nullptr;
        result = visit_object_references(heap, value, mark_edge, &state);
        if (result != TraceResult::complete) return {result == TraceResult::invalid ? MarkStatus::invalid : MarkStatus::limit};
    }
    if (visitor != nullptr) for (Object* value = heap.head; value != nullptr; value = value->next)
        if (!visitor(opaque, value, value->marked)) return {MarkStatus::stopped};
    return {MarkStatus::complete, state.reachable, count - state.reachable, state.edges};
}
""";
}
