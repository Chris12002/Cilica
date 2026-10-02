namespace Cilica.Compiler;

/// <summary>Private single-thread experimental runtime, emitted inside each program namespace.</summary>
internal static class ObjectCppRuntime
{
    // Define once even when several independently emitted closures share a TU.
    internal const string DiagnosticIncludes = """
#ifndef CILICA_ARENA_ASAN
#if defined(__SANITIZE_ADDRESS__)
#define CILICA_ARENA_ASAN 1
#elif defined(__has_feature)
#if __has_feature(address_sanitizer)
#define CILICA_ARENA_ASAN 1
#endif
#endif
#ifndef CILICA_ARENA_ASAN
#define CILICA_ARENA_ASAN 0
#endif
#endif
#if CILICA_ARENA_ASAN
#include <sanitizer/asan_interface.h>
#endif

""";

    internal const string Text = """
struct Object;
using FieldAccessor = void* (*)(Object*) noexcept;
struct Descriptor {
    std::size_t size, alignment;
    const std::size_t* references;
    std::size_t reference_count;
    void (*destroy)(Object*) noexcept;
    const Descriptor* base;
    const std::size_t* fields;
    std::size_t field_count;
    const FieldAccessor* accessors;
    void* (*field_address)(Object*, std::size_t) noexcept;
};
struct Instance;
struct Object {
    const Descriptor* type = nullptr;
    Instance* owner = nullptr;
    Object* next = nullptr;
    std::uintptr_t synchronization = 0;
};
[[maybe_unused]] static bool derives_from(const Descriptor* actual, const Descriptor* expected) noexcept {
    for (const Descriptor* type = actual; type != nullptr; type = type->base)
        if (type == expected) return true;
    return false;
}
// Keep descriptor lookup out of statement helpers: repeated arena expressions
// otherwise amplify Clang O0 sanitizer spills even for a single field store.
[[maybe_unused, gnu::noinline]] static void* field_address(Object* value, std::size_t index) noexcept {
    return value->type->accessors[index](value);
}
// Offset-based diagnostic/tracing lookup. Declared fields are emitted in order,
// so measured offsets are strictly increasing; execution uses direct indices.
[[maybe_unused, gnu::noinline]] static void* field_by_offset(Object* value, std::size_t offset) noexcept {
    const Descriptor& type = *value->type;
    std::size_t first = 0, last = type.field_count;
    while (first < last) {
        const std::size_t middle = first + (last - first) / 2;
        if (type.fields[middle] < offset) first = middle + 1;
        else last = middle;
    }
    return first < type.field_count && type.fields[first] == offset ? type.accessors[first](value) : nullptr;
}
struct Instance {
    Object* head = nullptr;
    std::size_t bytes = 0, count = 0;
    std::size_t limit = 1048576, object_limit = 4096;
    Instance() noexcept = default;
    Instance(const Instance&) = delete;
    Instance& operator=(const Instance&) = delete;
    bool owns(const Object* value) const noexcept {
        if (value == nullptr) return true;
        // Compare addresses before reading a pointer supplied at a native boundary.
        for (const Object* p = head; p != nullptr; p = p->next) if (p == value) return true;
        return false;
    }
    ~Instance() noexcept {
        while (head != nullptr) { Object* p = head; head = p->next; p->type->destroy(p); }
    }
};
// Argument packets live in the caller's execution storage. Reference arguments
// also remain in the caller's published roots until the callee publishes its copy.
static constexpr std::uint32_t frame_done = 0xffffffffu;
struct Argument { std::uint32_t bits = 0; Object* reference = nullptr; };
struct Frame {
    Frame* previous = nullptr;
    Object** slots = nullptr;
    std::size_t count = 0;
};
struct Context {
    Instance& instance;
    Frame* top = nullptr;
    std::size_t depth = 0;
    const std::size_t execution_limit;
    std::byte* execution = nullptr;
    std::size_t used = 0, peak = 0;
    std::uint32_t pending = 0;
    const char* image = nullptr;
    std::uint32_t method = 0, il = 0;
    // Private diagnostic observer, called before each object allocation. It cannot collect yet.
    void (*observe)(Context&) noexcept = nullptr;
    explicit Context(Instance& heap, std::size_t bytes = 1048576) noexcept
        : instance(heap), execution_limit(bytes) {}
    Context(const Context&) = delete;
    Context& operator=(const Context&) = delete;
    ~Context() noexcept { ::operator delete(execution); }
};
[[maybe_unused]] static void poison_storage(void* address, std::size_t bytes) noexcept {
#if CILICA_ARENA_ASAN
    __asan_poison_memory_region(address, bytes);
#else
    (void)address; (void)bytes;
#endif
}
[[maybe_unused]] static void unpoison_storage(void* address, std::size_t bytes) noexcept {
#if CILICA_ARENA_ASAN
    __asan_unpoison_memory_region(address, bytes);
#else
    (void)address; (void)bytes;
#endif
}
// Owns one typed, stable, non-GC arena reservation. All size checks use
// subtraction before addition; failed reservations do not mutate the cursor.
template<class T> struct Storage {
    Context& context;
    const std::size_t mark;
    T* value = nullptr;
    explicit Storage(Context& c) noexcept : context(c), mark(c.used) {
        static_assert(alignof(T) <= alignof(std::max_align_t) && alignof(std::max_align_t) >= 8);
        // Keep live reservations in separate ASan shadow granules on the tested
        // targets. Apply the same layout in diagnostic and ordinary builds.
        constexpr std::size_t alignment = alignof(T) < 8 ? 8 : alignof(T);
        static_assert(std::is_trivially_destructible_v<T>);
        const std::size_t padding = (alignment - mark % alignment) % alignment;
        if (mark > c.execution_limit || padding > c.execution_limit - mark ||
            sizeof(T) > c.execution_limit - mark - padding) return;
        if (c.execution == nullptr) {
            c.execution = static_cast<std::byte*>(::operator new(c.execution_limit, std::nothrow));
            if (c.execution == nullptr) return;
            poison_storage(c.execution, c.execution_limit);
        }
        // Default member initializers zero roots/locals without a stack-sized T temporary.
        unpoison_storage(c.execution + mark + padding, sizeof(T));
        value = ::new (static_cast<void*>(c.execution + mark + padding)) T;
        c.used = mark + padding + sizeof(T);
        if (c.used > c.peak) c.peak = c.used;
    }
    Storage(const Storage&) = delete;
    Storage& operator=(const Storage&) = delete;
    ~Storage() noexcept {
        if (value != nullptr) { value->~T(); poison_storage(value, sizeof(T)); context.used = mark; }
    }
};
struct RootScope {
    Context& context;
    Frame& frame;
    RootScope(Context& c, Frame& f, Object** roots, std::size_t n) noexcept
        : context(c), frame(f) {
        f.previous = c.top; f.slots = roots; f.count = n; c.top = &f; ++c.depth;
    }
    RootScope(const RootScope&) = delete;
    RootScope& operator=(const RootScope&) = delete;
    ~RootScope() noexcept { context.top = frame.previous; --context.depth; }
};
[[maybe_unused]] static bool valid_roots(const Context& context) noexcept {
    for (const Frame* f = context.top; f != nullptr; f = f->previous)
        for (std::size_t i = 0; i < f->count; ++i)
            if (!context.instance.owns(f->slots[i])) return false;
    return true;
}
template<class T> static Object* allocate(Context& context, const Descriptor& descriptor) noexcept {
    if (!valid_roots(context)) { context.pending = 6u; return nullptr; }
    if (context.observe != nullptr) context.observe(context);
    Instance& heap = context.instance;
    if (heap.bytes > heap.limit || sizeof(T) > heap.limit - heap.bytes || heap.count >= heap.object_limit)
        return nullptr;
    T* value = new (std::nothrow) T{};
    if (value == nullptr) return nullptr;
    value->header.type = &descriptor; value->header.owner = &heap; value->header.next = heap.head;
    heap.head = &value->header; heap.bytes += sizeof(T); ++heap.count;
    return &value->header;
}
[[maybe_unused]] static std::uint32_t cilica_shr_i4(std::uint32_t value, std::uint32_t count) noexcept {
    count &= 31u;
    const std::uint64_t bits = value;
    const std::uint64_t sign = std::uint64_t{0} - (bits >> 31u);
    const std::uint64_t fill = count == 0u ? std::uint64_t{0} : sign << (32u - count);
    return static_cast<std::uint32_t>((bits >> count) | fill);
}

""";
}
