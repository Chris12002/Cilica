namespace Cilica.Compiler;

/// <summary>Private checked scalar storage borrows; native pointer values never identify a frame lifetime.</summary>
internal static class ObjectCppLocations
{
    internal const string Text = """
// Exact registered Int32 views and their reduced Int32/UInt32/enum array owners.
[[maybe_unused]] static bool integer_array_location_type(const Descriptor* actual, const Descriptor* view) noexcept;
enum class LocationStatus { complete, invalid, read_only, limit, busy, null_reference, index_bounds, array_type_mismatch };
enum class LocationNarrow { none, Int8, UInt8, Int16, UInt16 };
enum class LocationOrigin { invalid, frame_integer, frame_reference, heap_field, frame_wide_integer, array_slot, static_slot };
struct Location {
    // A heap location's owner must be published as a root across allocations.
    // A frame location borrows the registered frame's already-published storage.
    Object* owner = nullptr;
    std::uint64_t frame = 0;
    std::size_t slot = 0;
    const Descriptor* declaring = nullptr;
    LocationOrigin origin = LocationOrigin::invalid;
    bool read_only = false;
    std::size_t element = 0;
    LocationNarrow narrow = LocationNarrow::none;
};
struct LocationResult { LocationStatus status; Location value{}; };
struct BorrowFrame {
    BorrowFrame* previous = nullptr;
    const Frame* roots = nullptr;
    Object** references = nullptr;
    std::size_t reference_count = 0;
    const ReferencePredicate* accepts = nullptr;
    std::uint32_t* integers = nullptr;
    std::size_t integer_count = 0;
    std::uint64_t token = 0;
    std::uint64_t* wide_integers = nullptr;
    std::size_t wide_integer_count = 0;
};
// Registration lists, slot extents and predicate tables are runtime-owned.
// Validate the carrier and lifetime, not arbitrary native memory corruption.
[[gnu::noinline]] static bool active_root_frame(const Context& context, const Frame* expected) noexcept {
    std::size_t depth = 0;
    bool found = false;
    for (const Frame* frame = context.top; frame != nullptr; frame = frame->previous) {
        if (++depth > 64) return false;
        if (frame == expected) found = true;
    }
    return found && depth == context.depth;
}
static std::atomic<std::uint64_t> next_borrow_token{1};
[[gnu::noinline]] static std::uint64_t issue_borrow_token() noexcept {
    auto token = next_borrow_token.load(std::memory_order_relaxed);
    while (token != std::numeric_limits<std::uint64_t>::max())
        if (next_borrow_token.compare_exchange_weak(token, token + 1, std::memory_order_relaxed)) return token;
    return 0;
}
// Storage lives in the owning execution frame; only this small registration
// record is on the native stack. It must outlive all synchronous borrowers.
struct BorrowScope {
    Context& context;
    BorrowFrame frame;
    LocationStatus status = LocationStatus::invalid;
    BorrowScope(Context& c, const Frame& roots, std::uint32_t* integers, std::size_t count,
        const ReferencePredicate* accepts, std::uint64_t* wide_integers = nullptr, std::size_t wide_count = 0) noexcept : context(c) {
        if (c.instance.marking) { status = LocationStatus::busy; return; }
        if (!active_root_frame(c, &roots) || roots.count > frame_root_limit || count > 65536 || wide_count > 65536 - count ||
            (roots.count != 0 && (roots.slots == nullptr || accepts == nullptr)) || (count != 0 && integers == nullptr)
            || (wide_count != 0 && wide_integers == nullptr)) return;
        for (std::size_t i = 0; i < roots.count; ++i) if (accepts[i] == nullptr) return;
        std::size_t registered = 0;
        for (const BorrowFrame* current = c.borrows; current != nullptr; current = current->previous) {
            if (++registered >= 64) { status = LocationStatus::limit; return; }
            if (current->roots == &roots) return;
        }
        const auto token = issue_borrow_token();
        if (token == 0) { status = LocationStatus::limit; return; }
        frame = {c.borrows, &roots, roots.slots, roots.count, accepts, integers, count, token, wide_integers, wide_count};
        c.borrows = &frame;
        if (c.borrow_cache == nullptr) c.borrow_cache = new(std::nothrow) BorrowFrame*[64]{};
        if (c.borrow_cache != nullptr) {
            auto& cached = c.borrow_cache[token % 64];
            if (cached == nullptr) cached = &frame;
        }
        status = LocationStatus::complete;
    }
    // Keep per-frame array access and sanitizer instrumentation out of the
    // generated method driver, whose native stack must remain bounded at O0.
    template<class F> [[gnu::noinline]] BorrowScope(Context& c, F* storage, const ReferencePredicate* accepts) noexcept
        : BorrowScope(c, storage->header, storage->integers.data(), storage->integers.size(), accepts,
            storage->wide_integers.data(), storage->wide_integers.size()) {}
    BorrowScope(const BorrowScope&) = delete;
    BorrowScope& operator=(const BorrowScope&) = delete;
    ~BorrowScope() noexcept {
        if (status != LocationStatus::complete) return;
        if (context.borrow_cache != nullptr) {
            auto& cached = context.borrow_cache[frame.token % 64];
            if (cached == &frame) cached = nullptr;
        }
        context.borrows = frame.previous;
    }
};
// Even a registration whose native scope ends later cannot authenticate a root
// frame after it has been popped, including replacement at the same address.
static void retire_borrows(Context& context, const Frame& roots) noexcept {
    std::size_t count = 0;
    for (BorrowFrame* frame = context.borrows; frame != nullptr && count++ < 64; frame = frame->previous)
        if (frame->roots == &roots) frame->roots = nullptr;
}
CILICA_FRAME_BORROW_HELPER static const BorrowFrame* find_borrow(const Context& context, std::uint64_t token) noexcept {
    if (token == 0) return nullptr;
    const BorrowFrame* found = context.borrow_cache == nullptr ? nullptr : context.borrow_cache[token % 64];
    // The cache is only an accelerator. Full tokens authenticate a context's
    // registration; collisions preserve the original bounded list lookup.
    if (CILICA_VERIFY_BORROW_LIFETIMES || found == nullptr || found->token != token) {
        found = nullptr;
        std::size_t count = 0;
        for (const BorrowFrame* frame = context.borrows; frame != nullptr; frame = frame->previous) {
            if (++count > 64) return nullptr;
            if (frame->token == token) found = frame;
        }
    }
    if (found == nullptr || found->roots == nullptr) return nullptr;
    // Registration checked membership; every RootScope pop retires it before
    // storage can be reused. Diagnostic mode also re-walks the active root list.
    if (CILICA_VERIFY_BORROW_LIFETIMES && !active_root_frame(context, found->roots)) return nullptr;
    // Only read the root frame after authenticating its active registration.
    if (found->roots->slots != found->references || found->roots->count != found->reference_count) return nullptr;
    return found;
}
struct LocationStorage {
    LocationStatus status;
    StorageKind kind = StorageKind::integer;
    void* address = nullptr;
    ReferencePredicate accepts = nullptr;
};
CILICA_FRAME_BORROW_HELPER static LocationStorage frame_location_storage(Context& context, const Location& location) noexcept {
    if (location.owner != nullptr || location.declaring != nullptr || location.element != 0) return {LocationStatus::invalid};
    const BorrowFrame* frame = find_borrow(context, location.frame);
    if (frame == nullptr) return {LocationStatus::invalid};
    if (location.origin == LocationOrigin::frame_integer) {
        if (location.slot >= frame->integer_count) return {LocationStatus::invalid};
        return {LocationStatus::complete, StorageKind::integer, &frame->integers[location.slot]};
    }
    if (location.origin == LocationOrigin::frame_wide_integer) {
        if (location.slot >= frame->wide_integer_count) return {LocationStatus::invalid};
        return {LocationStatus::complete, StorageKind::wide_integer, &frame->wide_integers[location.slot]};
    }
    if (location.slot >= frame->reference_count) return {LocationStatus::invalid};
    return {LocationStatus::complete, StorageKind::reference, &frame->references[location.slot], frame->accepts[location.slot]};
}
[[gnu::noinline]] static LocationStorage heap_location_storage(Context& context, const Location& location, bool write) noexcept {
    if (location.origin != LocationOrigin::heap_field || location.element != 0 || location.frame != 0 || location.owner == nullptr ||
        !managed_reference(context.instance, location.owner) || !known_type(location.declaring)) return {LocationStatus::invalid};
    const Descriptor& actual = *location.owner->type;
    if (!derives_from(&actual, location.declaring) || location.slot >= location.declaring->field_count ||
        location.slot >= actual.field_count || actual.storage == nullptr) return {LocationStatus::invalid};
    const auto& storage = actual.storage[location.slot];
    if (write && storage.read_only) return {LocationStatus::read_only};
    return {LocationStatus::complete, storage.kind, field_address(location.owner, location.slot), storage.accepts};
}
// The dispatcher validates ownership, exact type, backing buffers and element bounds.
// Keep component address arithmetic separate from those checks to bound O0 ASan frames.
[[gnu::noinline]] static LocationStorage array_component_location_storage(ArrayObject* array,
    const Descriptor& type, std::size_t element, std::size_t slot) noexcept {
    if (slot >= type.array_component_count || type.array_components == nullptr)
        return {LocationStatus::invalid};
    const auto& component = type.array_components[slot];
    if (component.kind == StorageKind::integer)
        return {LocationStatus::complete, component.kind, &array->integers[element * type.array_int32_count + component.offset]};
    if (component.kind == StorageKind::wide_integer)
        return {LocationStatus::complete, component.kind, &array->wide_integers[element * type.array_int64_count + component.offset]};
    return {LocationStatus::complete, component.kind, &array->references[element * type.array_reference_slots + component.offset], component.accepts};
}
[[gnu::noinline]] static LocationStorage array_location_storage(Context& context, const Location& location) noexcept {
    if (location.frame != 0 || location.owner == nullptr || !managed_reference(context.instance, location.owner)
        || !known_type(location.declaring) || !valid_array_storage(location.owner)) return {LocationStatus::invalid};
    const bool integer = integer_array_location_type(location.owner->type, location.declaring);
    if (!integer && (location.owner->type != location.declaring
        || (location.declaring->array_kind != 2 && location.declaring->array_kind != 3))) return {LocationStatus::invalid};
    auto* array = reinterpret_cast<ArrayObject*>(location.owner);
    const auto& type = *location.declaring;
    if (location.element >= array->length) return {LocationStatus::invalid};
    if (integer) {
        if (location.slot != 0) return {LocationStatus::invalid};
        return {LocationStatus::complete, StorageKind::integer, &array->integers[location.element]};
    }
    if (type.array_kind == 2) {
        if (location.slot != 0 || type.accepts_element == nullptr) return {LocationStatus::invalid};
        return {LocationStatus::complete, StorageKind::reference, &array->references[location.element], type.accepts_element};
    }
    return array_component_location_storage(array, type, location.element, location.slot);
}
// Static borrows reuse an active frame token. A token from another context or a
// retired/replaced frame never grants access to this instance's static slots.
static const FieldStorage* static_location_field(std::size_t slot) noexcept;
[[gnu::noinline]] static LocationStorage static_location_storage(Context& context, const Location& location, bool write) noexcept {
    if (location.owner != nullptr || location.declaring != nullptr || location.element != 0
        || find_borrow(context, location.frame) == nullptr || context.instance.statics == nullptr) return {LocationStatus::invalid};
    const auto* field = static_location_field(location.slot);
    if (field == nullptr) return {LocationStatus::invalid};
    if (write && field->read_only) return {LocationStatus::read_only};
    auto& slot = context.instance.statics[location.slot];
    if (field->kind == StorageKind::integer) return {LocationStatus::complete, field->kind, &slot.bits};
    if (field->kind == StorageKind::wide_integer) return {LocationStatus::complete, field->kind, &slot.wide};
    return {LocationStatus::complete, field->kind, &slot.reference, field->accepts};
}
CILICA_FRAME_BORROW_HELPER static LocationStorage location_storage(Context& context, const Location& location, bool write) noexcept {
    if (context.instance.marking) return {LocationStatus::busy};
    if (location.narrow != LocationNarrow::none && (location.origin != LocationOrigin::frame_integer
        || (location.narrow != LocationNarrow::Int8 && location.narrow != LocationNarrow::UInt8
            && location.narrow != LocationNarrow::Int16 && location.narrow != LocationNarrow::UInt16))) return {LocationStatus::invalid};
    if (write && location.read_only) return {LocationStatus::read_only};
    if (location.origin == LocationOrigin::frame_integer || location.origin == LocationOrigin::frame_reference || location.origin == LocationOrigin::frame_wide_integer)
        return frame_location_storage(context, location);
    if (location.origin == LocationOrigin::static_slot) return static_location_storage(context, location, write);
    if (location.origin == LocationOrigin::array_slot) return array_location_storage(context, location);
    return heap_location_storage(context, location, write);
}
CILICA_FRAME_BORROW_HELPER static LocationResult frame_location(Context& context, std::uint64_t token,
    LocationOrigin origin, std::size_t slot, bool read_only = false, LocationNarrow narrow = LocationNarrow::none) noexcept {
    Location value{nullptr, token, slot, nullptr, origin, read_only, 0, narrow};
    if (origin != LocationOrigin::frame_integer && origin != LocationOrigin::frame_reference && origin != LocationOrigin::frame_wide_integer) return {LocationStatus::invalid};
    const auto storage = location_storage(context, value, false);
    return storage.status == LocationStatus::complete ? LocationResult{storage.status, value} : LocationResult{storage.status};
}
[[maybe_unused, gnu::noinline]] static LocationResult static_location(Context& context, std::uint64_t token,
    std::size_t slot, bool read_only = false) noexcept {
    Location value{nullptr, token, slot, nullptr, LocationOrigin::static_slot, read_only};
    const auto storage = location_storage(context, value, false);
    if (storage.status != LocationStatus::complete) return {storage.status};
    value.read_only |= static_location_field(slot)->read_only;
    return {LocationStatus::complete, value};
}
CILICA_FRAME_BORROW_HELPER static bool valid_location_argument(Context& context, const Location& location,
    StorageKind kind, ReferencePredicate accepts, bool read_only, LocationNarrow narrow = LocationNarrow::none) noexcept {
    const auto storage = location_storage(context, location, !read_only);
    return storage.status == LocationStatus::complete && storage.kind == kind && storage.accepts == accepts && location.narrow == narrow;
}
[[maybe_unused, gnu::noinline]] static LocationResult array_location(Context& context, Object* owner,
    const Descriptor* declaring, std::uint32_t index, std::size_t slot, bool read_only = false) noexcept {
    if (context.instance.marking) return {LocationStatus::busy};
    if (owner == nullptr) return {LocationStatus::null_reference};
    if (!managed_reference(context.instance, owner) || !known_type(declaring) || !valid_array_storage(owner)) return {LocationStatus::invalid};
    const bool integer = integer_array_location_type(owner->type, declaring);
    if (!integer && ((declaring->array_kind != 2 && declaring->array_kind != 3)
        || owner->type->array_kind != declaring->array_kind)) return {LocationStatus::invalid};
    if (declaring->array_kind == 3 && owner->type != declaring) return {LocationStatus::invalid};
    if (index >= reinterpret_cast<ArrayObject*>(owner)->length) return {LocationStatus::index_bounds};
    // A writable reference slot is invariant even when its array view is covariant.
    if (!integer && owner->type != declaring) return {declaring->array_kind == 2 ? LocationStatus::array_type_mismatch : LocationStatus::invalid};
#if defined(__OPTIMIZE__)
    // The checks above already authenticate this new carrier. Validate only its
    // slot shape here; later accesses still resolve and validate the full carrier.
    if (integer || declaring->array_kind == 2) {
        if (slot != 0 || (!integer && declaring->accepts_element == nullptr)) return {LocationStatus::invalid};
    } else {
        const auto storage = array_component_location_storage(
            reinterpret_cast<ArrayObject*>(owner), *declaring, index, slot);
        if (storage.status != LocationStatus::complete) return {storage.status};
    }
    return {LocationStatus::complete, {owner, 0, slot, declaring, LocationOrigin::array_slot, read_only, index}};
#else
    // Preserve the diagnostic O0 frame budget and the original checked path.
    Location value{owner, 0, slot, declaring, LocationOrigin::array_slot, read_only, index};
    const auto storage = location_storage(context, value, false);
    return storage.status == LocationStatus::complete ? LocationResult{storage.status, value} : LocationResult{storage.status};
#endif
}
[[maybe_unused, gnu::noinline]] static LocationResult heap_location(Context& context, Object* owner,
    const Descriptor* declaring, std::size_t slot, bool read_only = false) noexcept {
    if (context.instance.marking) return {LocationStatus::busy};
    if (owner == nullptr) return {LocationStatus::null_reference};
    Location value{owner, 0, slot, declaring, LocationOrigin::heap_field, read_only};
    const auto storage = location_storage(context, value, false);
    if (storage.status != LocationStatus::complete) return {storage.status};
    value.read_only |= owner->type->storage[slot].read_only;
    return {LocationStatus::complete, value};
}
static std::uint32_t normalize_location_integer(std::uint32_t value, LocationNarrow narrow) noexcept {
    switch (narrow) {
        case LocationNarrow::Int8: value &= 255u; return (value ^ 128u) - 128u;
        case LocationNarrow::UInt8: return value & 255u;
        case LocationNarrow::Int16: value &= 65535u; return (value ^ 32768u) - 32768u;
        case LocationNarrow::UInt16: return value & 65535u;
        default: return value;
    }
}
CILICA_FRAME_BORROW_HELPER static LocationStatus load_location_integer(Context& context, const Location& location, std::uint32_t& result) noexcept {
    const auto storage = location_storage(context, location, false);
    if (storage.status != LocationStatus::complete) return storage.status;
    if (storage.kind != StorageKind::integer) return LocationStatus::invalid;
    result = normalize_location_integer(*static_cast<const std::uint32_t*>(storage.address), location.narrow); return LocationStatus::complete;
}
CILICA_FRAME_BORROW_HELPER static LocationStatus store_location_integer(Context& context, const Location& location, std::uint32_t value) noexcept {
    const auto storage = location_storage(context, location, true);
    if (storage.status != LocationStatus::complete) return storage.status;
    if (storage.kind != StorageKind::integer) return LocationStatus::invalid;
    *static_cast<std::uint32_t*>(storage.address) = normalize_location_integer(value, location.narrow); return LocationStatus::complete;
}
[[maybe_unused, gnu::noinline]] static LocationStatus load_location_wide_integer(Context& context, const Location& location, std::uint64_t& result) noexcept {
    const auto storage = location_storage(context, location, false);
    if (storage.status != LocationStatus::complete) return storage.status;
    if (storage.kind != StorageKind::wide_integer) return LocationStatus::invalid;
    result = *static_cast<const std::uint64_t*>(storage.address); return LocationStatus::complete;
}
[[maybe_unused, gnu::noinline]] static LocationStatus store_location_wide_integer(Context& context, const Location& location, std::uint64_t value) noexcept {
    const auto storage = location_storage(context, location, true);
    if (storage.status != LocationStatus::complete) return storage.status;
    if (storage.kind != StorageKind::wide_integer) return LocationStatus::invalid;
    *static_cast<std::uint64_t*>(storage.address) = value; return LocationStatus::complete;
}
[[gnu::noinline]] static bool location_reference(const Instance& heap, ReferencePredicate accepts, Object* value) noexcept {
    return accepts != nullptr && managed_reference(heap, value) && (value == nullptr || accepts(value->type));
}
CILICA_FRAME_BORROW_HELPER static LocationStatus load_location_reference(Context& context, const Location& location, Object*& result) noexcept {
    const auto storage = location_storage(context, location, false);
    if (storage.status != LocationStatus::complete) return storage.status;
    if (storage.kind != StorageKind::reference) return LocationStatus::invalid;
    Object* value = *static_cast<Object**>(storage.address);
    if (!location_reference(context.instance, storage.accepts, value)) return LocationStatus::invalid;
    result = value; return LocationStatus::complete;
}
[[maybe_unused, gnu::noinline]] static LocationStatus store_location_reference(Context& context, const Location& location, Object* value) noexcept {
    const auto storage = location_storage(context, location, true);
    if (storage.status != LocationStatus::complete) return storage.status;
    if (storage.kind != StorageKind::reference || !location_reference(context.instance, storage.accepts, value)) return LocationStatus::invalid;
    *static_cast<Object**>(storage.address) = value; return LocationStatus::complete;
}
""";
}
