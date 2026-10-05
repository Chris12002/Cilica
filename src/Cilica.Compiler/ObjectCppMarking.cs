namespace Cilica.Compiler;

/// <summary>Bounded allocation-free marking and scoped reachability diagnostics and atomic mark/sweep.</summary>
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
[[gnu::noinline]] static MarkReport mark_graph(Context& context, MarkLimits limits, MarkState& state) noexcept {
    Instance& heap = context.instance;
    if (heap.count > limits.objects) return {MarkStatus::limit};
    // Validate the entire allocation inventory, including unreachable headers,
    // before touching scratch state or trusting it for reachability reporting.
    std::size_t count = 0, bytes = (heap.runtime_types == nullptr ? 0 : runtime_type_count * sizeof(Object*))
        + (heap.statics == nullptr ? 0 : static_field_count * sizeof(StaticSlot))
        + (heap.initializers == nullptr ? 0 : type_initializer_count * sizeof(InitializationSlot));
    for (Object* value = heap.head; value != nullptr; value = value->next) {
        if (count == heap.count || count == limits.objects) return {MarkStatus::invalid};
        ++count;
        if (value->owner != &heap || !known_type(value->type)) return {MarkStatus::invalid};
        if (bytes > heap.bytes || value->allocation_bytes > heap.bytes - bytes ||
            value->allocation_bytes < value->type->size) return {MarkStatus::invalid};
        bytes += value->allocation_bytes;
    }
    if (count != heap.count || bytes != heap.bytes) return {MarkStatus::invalid};
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
    return {MarkStatus::complete, state.reachable, count - state.reachable, state.edges};
}
[[maybe_unused, gnu::noinline]] static MarkReport mark_reachable(Context& context, MarkLimits limits = {},
    ReachabilityVisitor visitor = nullptr, void* opaque = nullptr) noexcept {
    if (context.instance.marking) return {MarkStatus::busy};
    MarkState state(context.instance, limits.edges);
    auto report = mark_graph(context, limits, state);
    if (report.status != MarkStatus::complete) return report;
    if (visitor != nullptr) for (Object* value = context.instance.head; value != nullptr; value = value->next)
        if (!visitor(opaque, value, value->marked)) return {MarkStatus::stopped};
    return report;
}
// No user callback or managed execution may intervene between marking and sweep.
[[maybe_unused, gnu::noinline]] static MarkReport collect(Context& context, MarkLimits limits = {}) noexcept {
    Instance& heap = context.instance;
    if (heap.marking) return {MarkStatus::busy};
    MarkState state(heap, limits.edges);
    auto report = mark_graph(context, limits, state);
    if (report.status != MarkStatus::complete) return report;
    Object** link = &heap.head;
    while (*link != nullptr) {
        Object* value = *link;
        if (value->marked) { link = &value->next; continue; }
        *link = value->next;
        heap.bytes -= value->allocation_bytes; --heap.count;
        value->type->destroy(value);
    }
    return report;
}
[[gnu::noinline]] static bool collect_for_allocation(Context& context, bool& attempted) noexcept {
    if (attempted) return false;
    attempted = true;
    const auto report = collect(context);
    if (report.status == MarkStatus::complete) return true;
    if (context.pending == 0 && (report.status == MarkStatus::invalid || report.status == MarkStatus::busy)) context.pending = 6u;
    return false;
}
[[gnu::noinline]] static bool allocation_room(Context& context, std::size_t bytes, bool& attempted) noexcept {
    Instance& heap = context.instance;
    if (heap.marking) { if (context.pending == 0) context.pending = 6u; return false; }
    if (heap.collect_before_allocation && !attempted && !collect_for_allocation(context, attempted)) return false;
    const auto fits = [&]() noexcept { return heap.bytes <= heap.limit && bytes <= heap.limit - heap.bytes && heap.count < heap.object_limit; };
    if (fits()) return true;
    return collect_for_allocation(context, attempted) && fits();
}
""";
}
