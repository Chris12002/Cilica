namespace Cilica.Compiler;

// The caller owns this mutable, budgeted packet. Its receiver root remains live
// throughout adaptation and the call; no allocation or collection occurs here.
internal static class ObjectCppBoxedDispatch
{
    internal const string Text = """
[[maybe_unused, gnu::noinline]] static std::uint32_t adapt_boxed_call(Context& context,
    LocationArgument* arguments, std::size_t count, std::size_t capacity,
    const Descriptor* type, std::size_t slots) noexcept {
    if (type == nullptr) return 0u;
    if (arguments == nullptr || count == 0 || count > capacity || slots > capacity - (count - 1)) return 6u;
    const auto checked = unbox(context, arguments[0].reference, type, true);
    if (checked.status != BoxStatus::complete) return checked.status == BoxStatus::null_reference ? 4u : 6u;
    if (slots != type->field_count) return 6u;
    if (slots == 0) {
        for (std::size_t i = 1; i < count; ++i) arguments[i - 1] = arguments[i];
    } else {
        for (std::size_t i = count; i > 1; --i) arguments[i + slots - 2] = arguments[i - 1];
        for (std::size_t i = 0; i < slots; ++i) {
            const auto location = heap_location(context, checked.value, type, i);
            if (location.status != LocationStatus::complete) return 6u;
            arguments[i] = {}; arguments[i].location = location.value;
        }
    }
    return 0u;
}

""";
}
