namespace Cilica.Compiler;

internal static class ObjectCppInitialization
{
    internal const string Text = """
// Reserve the failure wrapper before executing user IL. If preparation fails,
// no initializer has run and a later trigger may retry. Once user IL starts,
// every outcome is permanent within this instance.
[[gnu::noinline]] static ExceptionResult prepare_initialization(Context& c, const Descriptor* type, const std::uint16_t* units, std::size_t length) noexcept {
    if (c.depth >= 63) return {8};
    auto* descriptor = exception_string_type();
    if (descriptor == nullptr) return {6};
    auto* name = allocate_string(c, *descriptor, units, length, nullptr, 0);
    if (name == nullptr) return {c.pending == 0 ? 5u : c.pending};
    Frame frame; RootScope scope(c, frame, &name, 1);
    return make_initialization_exception(c, type, name, nullptr);
}
[[gnu::noinline]] static std::uint32_t initialization_outcome(Context& c, InitializationSlot& slot) noexcept {
    if (c.pending == 0) { slot.state = 2; slot.failure = nullptr; return 0; }
    (void)materialize_exception(c);
    if ((c.pending == 13 || runtime_exception(c.pending) != nullptr) && valid_exception(c, c.exception)) {
        if (!valid_exception(c, slot.failure) || !type_initialization_exception(slot.failure->type)) { slot.state = 4; slot.fatal = 6; slot.failure = nullptr; c.exception = nullptr; return c.pending = 6; }
        reinterpret_cast<ExceptionObject*>(slot.failure)->inner = c.exception;
        slot.state = 3; c.exception = slot.failure; return c.pending = 13;
    }
    slot.state = 4; slot.fatal = c.pending == 8 || c.pending == 9 ? c.pending : 6;
    slot.failure = nullptr; c.exception = nullptr; return c.pending = slot.fatal;
}
[[maybe_unused, gnu::noinline]] static std::uint32_t initialize_type(Context& c, std::size_t index, const Descriptor* type,
    const std::uint16_t* name, std::size_t length, void (*body)(Context&, const $ARGUMENT*) noexcept) noexcept {
    if (c.instance.marking || c.pending != 0 || c.exception != nullptr || index >= type_initializer_count || body == nullptr
        || !type_initialization_exception(type)) return c.pending = 6;
    if (!ensure_initialization_storage(c)) return c.pending = c.pending == 0 ? 5u : c.pending;
    auto& slot = c.instance.initializers[index];
    if (!initialization_shape(slot)) return c.pending = 6;
    if (slot.state == 1 || slot.state == 3) {
        if (!valid_exception(c, slot.failure) || slot.failure->type != type) return c.pending = 6;
        if (slot.state == 1) return 0;
        c.exception = slot.failure; return c.pending = 13;
    }
    if (slot.failure != nullptr) return c.pending = 6;
    if (slot.state == 2) return 0;
    if (slot.state == 4) return c.pending = slot.fatal == 8 || slot.fatal == 9 ? slot.fatal : 6;
    if (slot.state != 0) return c.pending = 6;
    auto prepared = prepare_initialization(c, type, name, length);
    if (prepared.status != 0) return c.pending = prepared.status;
    slot.failure = prepared.value; slot.state = 1;
    body(c, nullptr);
    return initialization_outcome(c, slot);
}
""";
}
