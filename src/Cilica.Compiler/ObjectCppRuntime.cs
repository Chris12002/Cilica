namespace Cilica.Compiler;

/// <summary>Private single-thread experimental runtime, emitted inside each program namespace.</summary>
internal static class ObjectCppRuntime
{
    // Define once even when several independently emitted closures share a TU.
    internal const string DiagnosticIncludes = """
#ifndef CILICA_VERIFY_ALLOCATION_ROOTS
#define CILICA_VERIFY_ALLOCATION_ROOTS 0
#endif
#if CILICA_VERIFY_ALLOCATION_ROOTS != 0 && CILICA_VERIFY_ALLOCATION_ROOTS != 1
#error CILICA_VERIFY_ALLOCATION_ROOTS must be 0 or 1
#endif
#ifndef CILICA_VERIFY_INTERNAL_REFERENCES
#define CILICA_VERIFY_INTERNAL_REFERENCES 0
#endif
#if CILICA_VERIFY_INTERNAL_REFERENCES != 0 && CILICA_VERIFY_INTERNAL_REFERENCES != 1
#error CILICA_VERIFY_INTERNAL_REFERENCES must be 0 or 1
#endif
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
struct Descriptor;
using ReferencePredicate = bool (*)(const Descriptor*) noexcept;
enum class StorageKind { integer, reference, wide_integer };
struct FieldStorage { StorageKind kind; bool read_only; ReferencePredicate accepts; };
struct ArrayComponent { StorageKind kind; std::size_t offset; ReferencePredicate accepts; };
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
    // 0 nominal, 1 Int32 vector, 2 reference vector, 3 scalarized value vector, 4 Int64 vector. Dynamic slots complement
    // fixed reference-field offsets; neither is a collector implementation.
    unsigned array_kind = 0;
    bool (*accepts_element)(const Descriptor*) noexcept = nullptr;
    std::size_t (*dynamic_reference_count)(Object*) noexcept = nullptr;
    Object** (*dynamic_references)(Object*) noexcept = nullptr;
    const FieldStorage* storage = nullptr;
    std::size_t array_int32_count = 0, array_int64_count = 0, array_reference_slots = 0;
    const ArrayComponent* array_components = nullptr;
    std::size_t array_component_count = 0;
};
[[maybe_unused]] static bool known_type(const Descriptor*) noexcept;
[[maybe_unused]] static bool string_type(const Descriptor*) noexcept;
[[maybe_unused]] static bool runtime_type_storage(const Descriptor*) noexcept;
struct Instance;
struct Object {
    const Descriptor* type = nullptr;
    Instance* owner = nullptr;
    Object* next = nullptr;
    std::uintptr_t synchronization = 0;
    // Collector scratch is separate from identity/lock state and ownership links.
    Object* mark_next = nullptr;
    bool marked = false;
    std::size_t allocation_bytes = 0;
};
struct ArrayObject {
    Object header;
    std::uint32_t length = 0;
    std::uint32_t* integers = nullptr;
    std::uint64_t* wide_integers = nullptr;
    Object** references = nullptr;
    ArrayObject* empty_next = nullptr;
};
static_assert(std::is_standard_layout_v<ArrayObject> && offsetof(ArrayObject, header) == 0);
[[maybe_unused]] static void destroy_array(Object* value) noexcept {
    auto* array = reinterpret_cast<ArrayObject*>(value);
    delete[] array->integers; delete[] array->wide_integers; delete[] array->references; delete array;
}
[[maybe_unused]] static std::size_t array_reference_count(Object* value) noexcept {
    return std::size_t{reinterpret_cast<ArrayObject*>(value)->length} * (value->type->array_kind == 3 ? value->type->array_reference_slots : 1);
}
[[maybe_unused]] static Object** array_references(Object* value) noexcept {
    return reinterpret_cast<ArrayObject*>(value)->references;
}
// Counts come from bounded, compiler-owned descriptors. Buffers use their true
// scalar types so later borrowed addresses retain C++ object provenance.
[[maybe_unused]] static std::size_t array_int32_count(const Descriptor& type) noexcept {
    return type.array_kind == 1 ? 1 : type.array_int32_count;
}
[[maybe_unused]] static std::size_t array_reference_slots(const Descriptor& type) noexcept {
    return type.array_kind == 2 ? 1 : type.array_reference_slots;
}
[[maybe_unused]] static std::size_t array_stride(const Descriptor& type) noexcept {
    return array_int32_count(type) * sizeof(std::uint32_t) + type.array_int64_count * sizeof(std::uint64_t)
        + array_reference_slots(type) * sizeof(Object*);
}
[[maybe_unused]] static bool valid_array_storage(Object* value) noexcept {
    if (value->allocation_bytes < sizeof(ArrayObject)) return false;
    const auto& type = *value->type;
    const auto* array = reinterpret_cast<ArrayObject*>(value);
    const auto stride = array_stride(type);
    if (stride == 0 || array->length > 0x7fffffffu
        || std::size_t{array->length} > (std::numeric_limits<std::size_t>::max() - sizeof(ArrayObject)) / stride
        || value->allocation_bytes != sizeof(ArrayObject) + std::size_t{array->length} * stride) return false;
    return (array->integers != nullptr) == (array->length != 0 && array_int32_count(type) != 0)
        && (array->wide_integers != nullptr) == (array->length != 0 && type.array_int64_count != 0)
        && (array->references != nullptr) == (array->length != 0 && array_reference_slots(type) != 0);
}
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
struct StringObject {
    Object header;
    std::uint32_t length = 0;
    const std::uint16_t* units = nullptr;
    StringObject* literal_next = nullptr;
    std::size_t literal_id = 0;
};
static_assert(std::is_standard_layout_v<StringObject> && offsetof(StringObject, header) == 0);
[[maybe_unused]] static void destroy_string(Object* value) noexcept {
    auto* text = reinterpret_cast<StringObject*>(value);
    delete[] text->units; delete text;
}
struct Handle { std::uint64_t token = 0; };
struct HandleSlot { Object* value = nullptr; std::uint64_t token = 0; };
static constexpr std::size_t handle_capacity = 1024;
struct StaticSlot { std::uint32_t bits = 0; std::uint64_t wide = 0; Object* reference = nullptr; };
struct InitializationSlot { std::uint32_t state = 0, fatal = 0; Object* failure = nullptr; };
[[maybe_unused]] static bool initialization_shape(const InitializationSlot& slot) noexcept {
    return slot.state <= 4 && ((slot.state == 1 || slot.state == 3) == (slot.failure != nullptr))
        && (slot.state == 4 ? slot.fatal == 6 || slot.fatal == 8 || slot.fatal == 9 : slot.fatal == 0);
}
struct Instance {
    InitializationSlot* initializers = nullptr;
    StaticSlot* statics = nullptr;
    bool marking = false;
    bool collect_before_allocation = false;
    HandleSlot* handles = nullptr;
    std::size_t handle_count = 0, handle_limit = handle_capacity;
    StringObject* literals = nullptr;
    StringObject* exception_resources = nullptr;
    StringObject* integer_texts = nullptr;
    ArrayObject* empty_arrays = nullptr;
    Object* emergency_exception = nullptr;
    Object* invariant_culture = nullptr;
    Object** runtime_types = nullptr;
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
        delete[] runtime_types;
        delete[] initializers;
        delete[] statics;
        delete[] handles;
        while (head != nullptr) { Object* p = head; head = p->next; p->type->destroy(p); }
    }
};
// Only generated execution may use this shortcut. Public handles, tracing and
// collection must use owns/traceable_reference and compare before dereferencing.
[[maybe_unused]] static bool internal_reference_owned(const Instance& heap, const Object* value) noexcept {
    return !CILICA_VERIFY_INTERNAL_REFERENCES || heap.owns(value);
}
[[maybe_unused]] static bool managed_reference(const Instance& heap, const Object* value) noexcept {
    return value == nullptr || (internal_reference_owned(heap, value) && value->owner == &heap && known_type(value->type));
}
// Argument packets live in the caller's execution storage. Reference arguments
// also remain in the caller's published roots until the callee publishes its copy.
static constexpr std::uint32_t frame_done = 0xffffffffu;
struct Argument { std::uint32_t bits = 0; Object* reference = nullptr; std::uint64_t wide_bits = 0; };
struct Frame {
    Frame* previous = nullptr;
    Object** slots = nullptr;
    std::size_t count = 0;
};
struct BorrowFrame;
struct Context {
    Instance& instance;
    Frame* top = nullptr;
    std::size_t depth = 0;
    BorrowFrame* borrows = nullptr;
    const std::size_t execution_limit;
    std::byte* execution = nullptr;
    std::size_t used = 0, peak = 0;
    std::uint32_t pending = 0;
    Object* exception = nullptr;
    const char* image = nullptr;
    std::uint32_t method = 0, il = 0;
    // Private diagnostic observer, called at published allocation safepoints.
    void (*observe)(Context&) noexcept = nullptr;
    explicit Context(Instance& heap, std::size_t bytes = 1048576) noexcept
        : instance(heap), execution_limit(bytes) {}
    Context(const Context&) = delete;
    Context& operator=(const Context&) = delete;
    ~Context() noexcept { ::operator delete(execution); }
};
static void retire_borrows(Context&, const Frame&) noexcept;
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
    ~RootScope() noexcept {
        if (context.borrows != nullptr) retire_borrows(context, frame);
        context.top = frame.previous; --context.depth;
    }
};
[[maybe_unused]] static bool valid_roots(const Context& context) noexcept {
    if (!context.instance.owns(context.exception) || !context.instance.owns(context.instance.emergency_exception)
        || !context.instance.owns(context.instance.invariant_culture)
        || !context.instance.owns(reinterpret_cast<Object*>(context.instance.integer_texts))) return false;
    if (context.instance.runtime_types != nullptr) for (std::size_t i = 0; i < runtime_type_count; ++i)
        if (!context.instance.owns(context.instance.runtime_types[i])) return false;
    for (const Frame* f = context.top; f != nullptr; f = f->previous)
        for (std::size_t i = 0; i < f->count; ++i)
            if (!context.instance.owns(f->slots[i])) return false;
    return true;
}
// Compiler-published roots remain registered in every build. A full ownership
// scan diagnoses runtime corruption; collection validates its own root walk.
// Private observers retain validation before delivery, even in normal builds.
[[maybe_unused]] static bool allocation_safepoint(Context& context) noexcept {
    if ((CILICA_VERIFY_ALLOCATION_ROOTS || context.observe != nullptr) && !valid_roots(context)) {
        context.pending = 6u; return false;
    }
    if (context.observe != nullptr) context.observe(context);
    return true;
}
[[maybe_unused]] static bool collect_for_allocation(Context&, bool&) noexcept;
[[maybe_unused]] static bool allocation_room(Context&, std::size_t, bool&) noexcept;
// Buffers are filled before publication and exposed only through const units.
// Callers validate the owned, exact String descriptors before reading operands.
[[maybe_unused, gnu::noinline]] static Object* allocate_string(Context& context, const Descriptor& descriptor,
    const std::uint16_t* left, std::uint32_t first, const std::uint16_t* right, std::uint32_t second,
    const std::uint16_t* extra = nullptr, std::uint32_t third = 0, const std::uint16_t* tail = nullptr, std::uint32_t fourth = 0, const std::uint32_t* code_units = nullptr) noexcept {
    if (!allocation_safepoint(context)) return nullptr;
    Instance& heap = context.instance;
    if (first > 0x7fffffffu || second > 0x7fffffffu - first) return nullptr;
    if (third > 0x7fffffffu - first - second || fourth > 0x7fffffffu - first - second - third) return nullptr;
    const std::uint32_t length = first + second + third + fourth;
    if (std::size_t{length} > (std::numeric_limits<std::size_t>::max() - sizeof(StringObject)) / sizeof(std::uint16_t)) return nullptr;
    const std::size_t charged = sizeof(StringObject) + std::size_t{length} * sizeof(std::uint16_t);
    bool attempted = false;
    for (;;) {
        if (!allocation_room(context, charged, attempted)) return nullptr;
        auto* value = new (std::nothrow) StringObject{};
        if (value == nullptr) { if (collect_for_allocation(context, attempted)) continue; return nullptr; }
        auto* units = length == 0 ? nullptr : new (std::nothrow) std::uint16_t[length];
        if (length != 0 && units == nullptr) { delete value; if (collect_for_allocation(context, attempted)) continue; return nullptr; }
        for (std::uint32_t i = 0; i < first; ++i) units[i] = code_units == nullptr ? left[i] : static_cast<std::uint16_t>(code_units[i]);
        for (std::uint32_t i = 0; i < second; ++i) units[first + i] = right[i];
        for (std::uint32_t i = 0; i < third; ++i) units[first + second + i] = extra[i];
        for (std::uint32_t i = 0; i < fourth; ++i) units[first + second + third + i] = tail[i];
        value->length = length; value->units = units;
        value->header.type = &descriptor; value->header.owner = &heap; value->header.next = heap.head;
        value->header.allocation_bytes = charged;
        heap.head = &value->header; heap.bytes += charged; ++heap.count;
        return &value->header;
    }
}
[[maybe_unused, gnu::noinline]] static Object* string_literal(Context& context, const Descriptor& descriptor,
    std::size_t id, const std::uint16_t* units, std::uint32_t length) noexcept {
    for (auto* value = context.instance.literals; value != nullptr; value = value->literal_next)
        if (value->literal_id == id) return &value->header;
    Object* result = allocate_string(context, descriptor, units, length, nullptr, 0);
    if (result == nullptr) return nullptr;
    auto* value = reinterpret_cast<StringObject*>(result);
    value->literal_id = id; value->literal_next = context.instance.literals; context.instance.literals = value;
    return result;
}
[[maybe_unused, gnu::noinline]] static std::uint32_t string_length(Object* value) noexcept {
    return reinterpret_cast<StringObject*>(value)->length;
}
[[maybe_unused, gnu::noinline]] static std::uint32_t string_code_unit(Object* value, std::uint32_t index) noexcept {
    return reinterpret_cast<StringObject*>(value)->units[index];
}
[[maybe_unused, gnu::noinline]] static std::uint32_t string_equals(Object* left, Object* right) noexcept {
    if (left == right) return 1u;
    if (left == nullptr || right == nullptr) return 0u;
    const auto* a = reinterpret_cast<StringObject*>(left);
    const auto* b = reinterpret_cast<StringObject*>(right);
    if (a->length != b->length) return 0u;
    for (std::uint32_t i = 0; i < a->length; ++i) if (a->units[i] != b->units[i]) return 0u;
    return 1u;
}
[[maybe_unused, gnu::noinline]] static Object* string_concat(Context& context, const Descriptor& descriptor, Object* left, Object* right) noexcept {
    const auto* a = reinterpret_cast<StringObject*>(left);
    const auto* b = reinterpret_cast<StringObject*>(right);
    const std::uint32_t first = a == nullptr ? 0u : a->length, second = b == nullptr ? 0u : b->length;
    if (first == 0 && second == 0) return string_literal(context, descriptor, 0, nullptr, 0);
    if (first == 0) return right;
    if (second == 0) return left;
    return allocate_string(context, descriptor, a->units, first, b->units, second);
}
// The owned Int32 source stays rooted across collection; publication copies its
// low UTF-16 units and never aliases mutable vector storage.
[[maybe_unused, gnu::noinline]] static Object* string_from_code_units(Context& context, const Descriptor& descriptor, Object* source) noexcept {
    const auto* array = reinterpret_cast<ArrayObject*>(source);
    if (array->length == 0) return string_literal(context, descriptor, 0, nullptr, 0);
    return allocate_string(context, descriptor, nullptr, array->length, nullptr, 0, nullptr, 0, nullptr, 0, array->integers);
}
// Private bounded decimal cache. Formatting stays in managed code; publication
// copies its text so numeric cache identity never aliases a string literal.
[[maybe_unused, gnu::noinline]] static Object* integer_text_find(Context& context, const Descriptor& descriptor, std::uint32_t index) noexcept {
    if (index >= 300u) { context.pending = 6u; return nullptr; }
    std::size_t count = 0;
    for (auto* entry = context.instance.integer_texts; entry != nullptr; entry = entry->literal_next) {
        auto* value = reinterpret_cast<Object*>(entry);
        if (++count > 300 || !context.instance.owns(value) || value->owner != &context.instance
            || value->type != &descriptor || entry->literal_id >= 300u) { context.pending = 6u; return nullptr; }
        if (entry->literal_id == index) return value;
    }
    return nullptr;
}
[[maybe_unused, gnu::noinline]] static Object* integer_text_publish(Context& context, const Descriptor& descriptor,
    std::uint32_t index, Object* text) noexcept {
    if (text == nullptr || !context.instance.owns(text) || text->owner != &context.instance || text->type != &descriptor) {
        context.pending = 6u; return nullptr;
    }
    auto* known = integer_text_find(context, descriptor, index);
    if (context.pending != 0 || known != nullptr) return known;
    auto* input = reinterpret_cast<StringObject*>(text);
    auto* result = allocate_string(context, descriptor, input->units, input->length, nullptr, 0);
    if (result == nullptr) return nullptr;
    auto* entry = reinterpret_cast<StringObject*>(result);
    entry->literal_id = index; entry->literal_next = context.instance.integer_texts;
    context.instance.integer_texts = entry;
    return result;
}
template<class T> static Object* allocate(Context& context, const Descriptor& descriptor) noexcept {
    if (!allocation_safepoint(context)) return nullptr;
    Instance& heap = context.instance;
    bool attempted = false;
    for (;;) {
        if (!allocation_room(context, sizeof(T), attempted)) return nullptr;
        T* value = new (std::nothrow) T{};
        if (value == nullptr) { if (collect_for_allocation(context, attempted)) continue; return nullptr; }
        value->header.type = &descriptor; value->header.owner = &heap; value->header.next = heap.head;
        value->header.allocation_bytes = sizeof(T);
        heap.head = &value->header; heap.bytes += sizeof(T); ++heap.count;
        return &value->header;
    }
}
// Canonical Type objects are permanent instance-local GC roots. Identity slots
// are generated from closed metadata identities, never host reflection or pointers.
template<class T> static Object* runtime_type(Context& context, const Descriptor& descriptor, std::size_t identity) noexcept {
    if (identity >= runtime_type_count || !runtime_type_storage(&descriptor)) { context.pending = 6u; return nullptr; }
    if (context.instance.runtime_types == nullptr) {
        bool attempted = false;
        const auto bytes = runtime_type_count * sizeof(Object*);
        for (;;) {
            if (!allocation_room(context, bytes, attempted)) return nullptr;
            auto** slots = new (std::nothrow) Object*[runtime_type_count]{};
            if (slots == nullptr) { if (collect_for_allocation(context, attempted)) continue; return nullptr; }
            context.instance.runtime_types = slots; context.instance.bytes += bytes; break;
        }
    }
    auto*& slot = context.instance.runtime_types[identity];
    if (slot != nullptr) {
        if (!context.instance.owns(slot) || slot->owner != &context.instance || slot->type != &descriptor) {
            context.pending = 6u; return nullptr;
        }
        return slot;
    }
    auto* value = allocate<T>(context, descriptor);
    if (value != nullptr) slot = value;
    return value;
}
[[maybe_unused]] static std::uint32_t runtime_type_hash(Context& context, Object* value) noexcept {
    if (context.instance.runtime_types != nullptr) for (std::size_t i = 0; i < runtime_type_count; ++i)
        if (context.instance.runtime_types[i] == value) return static_cast<std::uint32_t>(i + 1);
    context.pending = 6u; return 0;
}
// One lazily allocated provider per instance; its cache is a permanent traced root.
template<class T> static Object* invariant_culture(Context& context, const Descriptor& descriptor) noexcept {
    Instance& heap = context.instance;
    if (heap.invariant_culture != nullptr) {
        auto* value = heap.invariant_culture;
        if (!heap.owns(value) || value->owner != &heap || value->type != &descriptor) { context.pending = 6u; return nullptr; }
        return value;
    }
    auto* value = allocate<T>(context, descriptor);
    // No allocation or observation occurs between allocation and cache publication.
    heap.invariant_culture = value;
    return value;
}
[[maybe_unused, gnu::noinline]] static Object* allocate_array(Context& context, const Descriptor& descriptor, std::uint32_t length) noexcept {
    if (!known_type(&descriptor) || descriptor.array_kind == 0) { context.pending = 6u; return nullptr; }
    if (length > 0x7fffffffu) { context.pending = 2u; return nullptr; }
    if (!allocation_safepoint(context)) return nullptr;
    Instance& heap = context.instance;
    const std::size_t stride = array_stride(descriptor);
    if (std::size_t{length} > (std::numeric_limits<std::size_t>::max() - sizeof(ArrayObject)) / stride) return nullptr;
    const std::size_t charged = sizeof(ArrayObject) + std::size_t{length} * stride;
    bool attempted = false;
    for (;;) {
        if (!allocation_room(context, charged, attempted)) return nullptr;
        auto* value = new (std::nothrow) ArrayObject;
        if (value == nullptr) { if (collect_for_allocation(context, attempted)) continue; return nullptr; }
        if (length != 0) {
            const auto integers = std::size_t{length} * array_int32_count(descriptor);
            const auto wide = std::size_t{length} * descriptor.array_int64_count;
            const auto references = std::size_t{length} * array_reference_slots(descriptor);
            if (integers != 0) value->integers = new (std::nothrow) std::uint32_t[integers]{};
            if (wide != 0) value->wide_integers = new (std::nothrow) std::uint64_t[wide]{};
            if (references != 0) value->references = new (std::nothrow) Object*[references]{};
            if ((integers != 0 && value->integers == nullptr) || (wide != 0 && value->wide_integers == nullptr)
                || (references != 0 && value->references == nullptr)) {
                destroy_array(&value->header);
                if (collect_for_allocation(context, attempted)) continue;
                return nullptr;
            }
        }
        value->length = length;
        value->header.type = &descriptor; value->header.owner = &heap; value->header.next = heap.head;
        value->header.allocation_bytes = charged;
        heap.head = &value->header; heap.bytes += charged; ++heap.count;
        return &value->header;
    }
}
[[maybe_unused]] static bool traceable_reference(const Instance&, const Object*) noexcept;
[[maybe_unused, gnu::noinline]] static bool ensure_static_storage(Context& context) noexcept {
    if (context.instance.marking) { context.pending = 6; return false; }
    if (context.instance.statics != nullptr) return true;
    if (!allocation_safepoint(context)) return false;
    constexpr auto charged = static_field_count * sizeof(StaticSlot);
    bool attempted = false;
    for (;;) {
        if (!allocation_room(context, charged, attempted)) return false;
        auto* slots = new (std::nothrow) StaticSlot[static_field_count]{};
        if (slots == nullptr) { if (collect_for_allocation(context, attempted)) continue; return false; }
        context.instance.statics = slots; context.instance.bytes += charged;
        return true;
    }
}
[[maybe_unused, gnu::noinline]] static bool ensure_initialization_storage(Context& context) noexcept {
    if (context.instance.marking) { context.pending = 6; return false; }
    if (context.instance.initializers != nullptr) return true;
    if (!allocation_safepoint(context)) return false;
    constexpr auto charged = type_initializer_count * sizeof(InitializationSlot);
    bool attempted = false;
    for (;;) {
        if (!allocation_room(context, charged, attempted)) return false;
        auto* slots = new (std::nothrow) InitializationSlot[type_initializer_count]{};
        if (slots == nullptr) { if (collect_for_allocation(context, attempted)) continue; return false; }
        context.instance.initializers = slots; context.instance.bytes += charged;
        return true;
    }
}
// Exact descriptors preserve different closed generic cache identities even
// when reference arrays admit covariant assignments. Cache publication cannot GC.
[[maybe_unused, gnu::noinline]] static Object* empty_array(Context& context, const Descriptor& descriptor) noexcept {
    if (context.instance.marking) { context.pending = 6; return nullptr; }
    std::size_t count = 0;
    for (auto* entry = context.instance.empty_arrays; entry != nullptr; entry = entry->empty_next) {
        auto* value = reinterpret_cast<Object*>(entry);
        if (++count > context.instance.count || !traceable_reference(context.instance, value)
            || value->type->array_kind == 0 || entry->length != 0) { context.pending = 6; return nullptr; }
        if (value->type == &descriptor) return value;
    }
    auto* value = allocate_array(context, descriptor, 0);
    if (value == nullptr) return nullptr;
    auto* entry = reinterpret_cast<ArrayObject*>(value);
    entry->empty_next = context.instance.empty_arrays; context.instance.empty_arrays = entry;
    return value;
}
// Receivers have passed exact descriptor membership before these helpers run.
// Keep pointer arithmetic on actual new[] elements, never a fabricated flexible member.
[[maybe_unused, gnu::noinline]] static std::uint32_t array_length(Object* value) noexcept {
    return reinterpret_cast<ArrayObject*>(value)->length;
}
[[maybe_unused, gnu::noinline]] static std::uint32_t load_integer_element(Object* value, std::uint32_t index) noexcept {
    return reinterpret_cast<ArrayObject*>(value)->integers[index];
}
[[maybe_unused, gnu::noinline]] static std::uint64_t load_wide_element(Object* value, std::uint32_t index) noexcept {
    return reinterpret_cast<ArrayObject*>(value)->wide_integers[index];
}
[[maybe_unused, gnu::noinline]] static void store_wide_element(Object* value, std::uint32_t index, std::uint64_t element) noexcept {
    reinterpret_cast<ArrayObject*>(value)->wide_integers[index] = element;
}
[[maybe_unused, gnu::noinline]] static Object* load_reference_element(Object* value, std::uint32_t index) noexcept {
    return reinterpret_cast<ArrayObject*>(value)->references[index];
}
[[maybe_unused, gnu::noinline]] static void store_integer_element(Object* value, std::uint32_t index, std::uint32_t element) noexcept {
    reinterpret_cast<ArrayObject*>(value)->integers[index] = element;
}
[[maybe_unused, gnu::noinline]] static bool store_reference_element(Context& context, Object* value, std::uint32_t index, Object* element) noexcept {
    if (!internal_reference_owned(context.instance, element)) { context.pending = 6u; return false; }
    if (element != nullptr && !known_type(element->type)) { context.pending = 6u; return false; }
    if (element != nullptr && !value->type->accepts_element(element->type)) { context.pending = 11u; return false; }
    reinterpret_cast<ArrayObject*>(value)->references[index] = element;
    return true;
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
