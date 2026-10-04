namespace Cilica.Compiler;

/// <summary>Allocation-free precise reference enumeration; no marking or reclamation.</summary>
internal static class ObjectCppTracing
{
    internal const string Text = """
enum class ReferenceKind { frame, literal, handle, field, element, pending_exception, emergency_exception, exception_resource };
enum class TraceResult { complete, stopped, invalid };
struct ReferenceEdge {
    ReferenceKind kind;
    Object* value;
    Object* owner;             // Field/element owner; null for roots.
    const Frame* frame;        // Published frame for a frame root; otherwise null.
    std::size_t index;         // Root slot, literal ID, field byte offset, or element index.
};
// Callbacks must not allocate managed objects, change references/topology, or
// re-enter execution. They may keep external bookkeeping and stop with false.
// Edge records are borrowed for the callback only; values remain instance-owned.
using ReferenceVisitor = bool (*)(void*, const ReferenceEdge&) noexcept;
[[maybe_unused]] static bool traceable_reference(const Instance& heap, const Object* value) noexcept {
    return value == nullptr || (heap.owns(value) && value->owner == &heap && known_type(value->type));
}
[[maybe_unused, gnu::noinline]] static TraceResult visit_edge(const Instance& heap, ReferenceVisitor visitor,
    void* state, const ReferenceEdge& edge) noexcept {
    if (!traceable_reference(heap, edge.value)) return TraceResult::invalid;
    return visitor(state, edge) ? TraceResult::complete : TraceResult::stopped;
}
// Frames, slot buffers, allocation headers and descriptor tables are runtime-owned
// storage. Validate contained managed pointers, not arbitrary native memory blocks.
// Incomplete walks can have delivered a prefix; they must never authorize sweeping.
[[maybe_unused, gnu::noinline]] static TraceResult visit_exception_resources(const Context& context, ReferenceVisitor visitor, void* state) noexcept {
    std::size_t count = 0;
    for (const StringObject* resource = context.instance.exception_resources; resource != nullptr; resource = resource->literal_next) {
        auto* value = reinterpret_cast<Object*>(const_cast<StringObject*>(resource));
        if (++count > 9 || count > context.instance.count || !traceable_reference(context.instance, value)
            || !string_type(value->type)) return TraceResult::invalid;
        const auto result = visit_edge(context.instance, visitor, state, {ReferenceKind::exception_resource, value, nullptr, nullptr, resource->literal_id});
        if (result != TraceResult::complete) return result;
    }
    return TraceResult::complete;
}
[[maybe_unused, gnu::noinline]] static TraceResult visit_roots(const Context& context, ReferenceVisitor visitor, void* state) noexcept {
    if (visitor == nullptr) return TraceResult::invalid;
    std::size_t depth = 0;
    for (const Frame* frame = context.top; frame != nullptr; frame = frame->previous) {
        if (++depth > 64 || frame->count > 1024 || (frame->count != 0 && frame->slots == nullptr)) return TraceResult::invalid;
        for (std::size_t i = 0; i < frame->count; ++i) {
            const auto result = visit_edge(context.instance, visitor, state, {ReferenceKind::frame, frame->slots[i], nullptr, frame, i});
            if (result != TraceResult::complete) return result;
        }
    }
    if (depth != context.depth) return TraceResult::invalid;
    if (context.exception != nullptr) {
        const auto result = visit_edge(context.instance, visitor, state, {ReferenceKind::pending_exception, context.exception, nullptr, nullptr, 0});
        if (result != TraceResult::complete) return result;
    }
    if (context.instance.emergency_exception != nullptr) {
        const auto result = visit_edge(context.instance, visitor, state, {ReferenceKind::emergency_exception, context.instance.emergency_exception, nullptr, nullptr, 0});
        if (result != TraceResult::complete) return result;
    }
    std::size_t count = 0;
    for (const StringObject* literal = context.instance.literals; literal != nullptr; literal = literal->literal_next) {
        // Convert the address before reading the header, even for an unreadable pointer.
        auto* value = reinterpret_cast<Object*>(const_cast<StringObject*>(literal));
        if (++count > context.instance.count || count > 257 || !traceable_reference(context.instance, value)
            || !string_type(value->type)) return TraceResult::invalid;
        const auto result = visit_edge(context.instance, visitor, state, {ReferenceKind::literal, value, nullptr, nullptr, literal->literal_id});
        if (result != TraceResult::complete) return result;
    }
    const auto resources = visit_exception_resources(context, visitor, state);
    if (resources != TraceResult::complete) return resources;
    std::size_t active = 0;
    const Instance& heap = context.instance;
    if (heap.handle_count > handle_capacity || (heap.handle_count != 0 && heap.handles == nullptr)) return TraceResult::invalid;
    if (heap.handles != nullptr) for (std::size_t i = 0; i < handle_capacity; ++i) {
        const HandleSlot& slot = heap.handles[i];
        if (slot.token == 0) { if (slot.value != nullptr) return TraceResult::invalid; continue; }
        ++active;
        const auto result = visit_edge(heap, visitor, state, {ReferenceKind::handle, slot.value, nullptr, nullptr, i});
        if (result != TraceResult::complete) return result;
    }
    if (active != heap.handle_count) return TraceResult::invalid;
    return TraceResult::complete;
}
[[maybe_unused, gnu::noinline]] static TraceResult visit_object_references(const Instance& heap, Object* value,
    ReferenceVisitor visitor, void* state) noexcept {
    if (visitor == nullptr || !traceable_reference(heap, value)) return TraceResult::invalid;
    if (value == nullptr) return TraceResult::complete;
    const Descriptor& type = *value->type;
    // Flattened maps already include inherited fields exactly once. Typed field
    // accessors preserve C++ subobject provenance; offsets are selectors only.
    for (std::size_t i = 0; i < type.reference_count; ++i) {
        auto* slot = static_cast<Object**>(type.field_address(value, type.references[i]));
        const auto result = visit_edge(heap, visitor, state, {ReferenceKind::field, *slot, value, nullptr, type.references[i]});
        if (result != TraceResult::complete) return result;
    }
    if (type.dynamic_reference_count != nullptr) {
        const std::size_t count = type.dynamic_reference_count(value);
        Object** slots = type.dynamic_references(value);
        for (std::size_t i = 0; i < count; ++i) {
            const auto result = visit_edge(heap, visitor, state, {ReferenceKind::element, slots[i], value, nullptr, i});
            if (result != TraceResult::complete) return result;
        }
    }
    return TraceResult::complete;
}
""";
}
