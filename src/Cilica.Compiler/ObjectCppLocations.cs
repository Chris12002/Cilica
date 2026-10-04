namespace Cilica.Compiler;

/// <summary>Private checked scalar storage borrows; native pointer values never identify a frame lifetime.</summary>
internal static class ObjectCppLocations
{
    internal const string Text = """
enum class LocationStatus { complete, invalid, read_only, limit, busy, null_reference };
enum class LocationOrigin { invalid, frame_integer, frame_reference, heap_field };
struct Location {
    // A heap location's owner must be published as a root across allocations.
    // A frame location borrows the registered frame's already-published storage.
    Object* owner = nullptr;
    std::uint64_t frame = 0;
    std::size_t slot = 0;
    const Descriptor* declaring = nullptr;
    LocationOrigin origin = LocationOrigin::invalid;
    bool read_only = false;
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
        const ReferencePredicate* accepts) noexcept : context(c) {
        if (c.instance.marking) { status = LocationStatus::busy; return; }
        if (!active_root_frame(c, &roots) || roots.count > 1024 || count > 65536 ||
            (roots.count != 0 && (roots.slots == nullptr || accepts == nullptr)) || (count != 0 && integers == nullptr)) return;
        for (std::size_t i = 0; i < roots.count; ++i) if (accepts[i] == nullptr) return;
        std::size_t registered = 0;
        for (const BorrowFrame* current = c.borrows; current != nullptr; current = current->previous) {
            if (++registered >= 64) { status = LocationStatus::limit; return; }
            if (current->roots == &roots) return;
        }
        const auto token = issue_borrow_token();
        if (token == 0) { status = LocationStatus::limit; return; }
        frame = {c.borrows, &roots, roots.slots, roots.count, accepts, integers, count, token};
        c.borrows = &frame; status = LocationStatus::complete;
    }
    BorrowScope(const BorrowScope&) = delete;
    BorrowScope& operator=(const BorrowScope&) = delete;
    ~BorrowScope() noexcept { if (status == LocationStatus::complete) context.borrows = frame.previous; }
};
// Even a registration whose native scope ends later cannot authenticate a root
// frame after it has been popped, including replacement at the same address.
static void retire_borrows(Context& context, const Frame& roots) noexcept {
    std::size_t count = 0;
    for (BorrowFrame* frame = context.borrows; frame != nullptr && count++ < 64; frame = frame->previous)
        if (frame->roots == &roots) frame->roots = nullptr;
}
[[gnu::noinline]] static const BorrowFrame* find_borrow(const Context& context, std::uint64_t token) noexcept {
    if (token == 0) return nullptr;
    const BorrowFrame* found = nullptr;
    std::size_t count = 0;
    for (const BorrowFrame* frame = context.borrows; frame != nullptr; frame = frame->previous) {
        if (++count > 64) return nullptr;
        if (frame->token == token) found = frame;
    }
    if (found == nullptr || found->roots == nullptr || !active_root_frame(context, found->roots)) return nullptr;
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
[[gnu::noinline]] static LocationStorage frame_location_storage(Context& context, const Location& location) noexcept {
    if (location.owner != nullptr || location.declaring != nullptr) return {LocationStatus::invalid};
    const BorrowFrame* frame = find_borrow(context, location.frame);
    if (frame == nullptr) return {LocationStatus::invalid};
    if (location.origin == LocationOrigin::frame_integer) {
        if (location.slot >= frame->integer_count) return {LocationStatus::invalid};
        return {LocationStatus::complete, StorageKind::integer, &frame->integers[location.slot]};
    }
    if (location.slot >= frame->reference_count) return {LocationStatus::invalid};
    return {LocationStatus::complete, StorageKind::reference, &frame->references[location.slot], frame->accepts[location.slot]};
}
[[gnu::noinline]] static LocationStorage heap_location_storage(Context& context, const Location& location, bool write) noexcept {
    if (location.origin != LocationOrigin::heap_field || location.frame != 0 || location.owner == nullptr ||
        !traceable_reference(context.instance, location.owner) || !known_type(location.declaring)) return {LocationStatus::invalid};
    const Descriptor& actual = *location.owner->type;
    if (!derives_from(&actual, location.declaring) || location.slot >= location.declaring->field_count ||
        location.slot >= actual.field_count || actual.storage == nullptr) return {LocationStatus::invalid};
    const auto& storage = actual.storage[location.slot];
    if (write && storage.read_only) return {LocationStatus::read_only};
    return {LocationStatus::complete, storage.kind, field_address(location.owner, location.slot), storage.accepts};
}
[[gnu::noinline]] static LocationStorage location_storage(Context& context, const Location& location, bool write) noexcept {
    if (context.instance.marking) return {LocationStatus::busy};
    if (write && location.read_only) return {LocationStatus::read_only};
    if (location.origin == LocationOrigin::frame_integer || location.origin == LocationOrigin::frame_reference)
        return frame_location_storage(context, location);
    return heap_location_storage(context, location, write);
}
[[maybe_unused, gnu::noinline]] static LocationResult frame_location(Context& context, std::uint64_t token,
    LocationOrigin origin, std::size_t slot, bool read_only = false) noexcept {
    Location value{nullptr, token, slot, nullptr, origin, read_only};
    if (origin != LocationOrigin::frame_integer && origin != LocationOrigin::frame_reference) return {LocationStatus::invalid};
    const auto storage = location_storage(context, value, false);
    return storage.status == LocationStatus::complete ? LocationResult{storage.status, value} : LocationResult{storage.status};
}
[[maybe_unused, gnu::noinline]] static bool valid_location_argument(Context& context, const Location& location,
    StorageKind kind, ReferencePredicate accepts, bool read_only) noexcept {
    const auto storage = location_storage(context, location, !read_only);
    return storage.status == LocationStatus::complete && storage.kind == kind && storage.accepts == accepts;
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
[[maybe_unused, gnu::noinline]] static LocationStatus load_location_integer(Context& context, const Location& location, std::uint32_t& result) noexcept {
    const auto storage = location_storage(context, location, false);
    if (storage.status != LocationStatus::complete) return storage.status;
    if (storage.kind != StorageKind::integer) return LocationStatus::invalid;
    result = *static_cast<const std::uint32_t*>(storage.address); return LocationStatus::complete;
}
[[maybe_unused, gnu::noinline]] static LocationStatus store_location_integer(Context& context, const Location& location, std::uint32_t value) noexcept {
    const auto storage = location_storage(context, location, true);
    if (storage.status != LocationStatus::complete) return storage.status;
    if (storage.kind != StorageKind::integer) return LocationStatus::invalid;
    *static_cast<std::uint32_t*>(storage.address) = value; return LocationStatus::complete;
}
[[gnu::noinline]] static bool location_reference(const Instance& heap, ReferencePredicate accepts, Object* value) noexcept {
    return accepts != nullptr && traceable_reference(heap, value) && (value == nullptr || accepts(value->type));
}
[[maybe_unused, gnu::noinline]] static LocationStatus load_location_reference(Context& context, const Location& location, Object*& result) noexcept {
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
