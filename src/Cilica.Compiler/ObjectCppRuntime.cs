namespace Cilica.Compiler;

/// <summary>Private single-thread experimental runtime, emitted inside each program namespace.</summary>
internal static class ObjectCppRuntime
{
    internal const string Text = """
struct Object;
struct Descriptor {
    std::size_t size, alignment;
    const std::size_t* references;
    std::size_t reference_count;
    void (*destroy)(Object*) noexcept;
};
struct Instance;
struct Object {
    const Descriptor* type = nullptr;
    Instance* owner = nullptr;
    Object* next = nullptr;
    std::uintptr_t synchronization = 0;
};
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
struct Frame;
struct Context {
    Instance& instance;
    Frame* top = nullptr;
    std::size_t depth = 0;
    std::uint32_t pending = 0;
    const char* image = nullptr;
    std::uint32_t method = 0, il = 0;
    // Private diagnostic observer, called before each allocation. It cannot collect yet.
    void (*observe)(Context&) noexcept = nullptr;
    explicit Context(Instance& heap) noexcept : instance(heap) {}
};
struct Frame {
    Context& context;
    Frame* previous;
    Object** slots;
    std::size_t count;
    Frame(Context& c, Object** roots, std::size_t n) noexcept
        : context(c), previous(c.top), slots(roots), count(n) { c.top = this; ++c.depth; }
    Frame(const Frame&) = delete;
    Frame& operator=(const Frame&) = delete;
    ~Frame() noexcept { context.top = previous; --context.depth; }
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
