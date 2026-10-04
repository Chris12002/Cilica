using System;
using System.Globalization;
using System.Text;

namespace Cilica.Compiler;

internal static class ObjectCppExceptions
{
    internal static void AppendStorage(StringBuilder text) => text.Append("""
struct ExceptionObject { Object header; Object* message = nullptr; };
static_assert(std::is_standard_layout_v<ExceptionObject> && std::is_trivially_destructible_v<ExceptionObject> && offsetof(ExceptionObject, header) == 0);
static constexpr std::size_t exception_references[] = {offsetof(ExceptionObject, message)};
[[gnu::noinline]] static void* exception_field(Object* value, [[maybe_unused]] std::size_t offset) noexcept {
    return &reinterpret_cast<ExceptionObject*>(value)->message;
}
struct ExceptionResult { std::uint32_t status = 0; Object* value = nullptr; };
struct ExceptionOrigin { std::uint32_t status = 0; const char* image = nullptr; std::uint32_t method = 0, il = 0; };
[[maybe_unused]] static ExceptionOrigin exception_origin(const Context& c) noexcept { return {c.pending, c.image, c.method, c.il}; }
[[maybe_unused]] static void set_exception_origin(Context& c, ExceptionOrigin origin) noexcept {
    c.pending = origin.status; c.image = origin.image; c.method = origin.method; c.il = origin.il;
}

""");

    internal static void AppendType(StringBuilder text, string symbol, string parent) => text
        .Append("using ").Append(symbol).Append(" = ExceptionObject;\nstatic constexpr Descriptor ").Append(symbol)
        .Append("_descriptor = {sizeof(ExceptionObject), alignof(ExceptionObject), exception_references, 1, ")
        .Append("[](Object* p) noexcept { delete reinterpret_cast<ExceptionObject*>(p); }, &").Append(parent)
        .Append("_descriptor, nullptr, 0, nullptr, exception_field};\n");

    internal static void AppendHelpers(StringBuilder text, ManagedExceptionTypes types, Func<ObjectTypeId, string> symbol)
    {
        text.Append("[[gnu::noinline]] static bool known_exception(const Descriptor* type) noexcept {\n");
        foreach (var type in types.Types) text.Append("    if (type == &").Append(symbol(type)).Append("_descriptor) return true;\n");
        text.Append("    return false;\n}\n[[gnu::noinline]] static const Descriptor* runtime_exception(std::uint32_t status) noexcept {\n    switch (status) {\n");
        foreach (var binding in types.RuntimeFaults)
            text.Append("    case ").Append(binding.Status.ToString(CultureInfo.InvariantCulture)).Append(": return &").Append(symbol(binding.Type)).Append("_descriptor;\n");
        text.Append("    default: return nullptr;\n    }\n}\n").Append(Helpers).Append('\n');
    }

    private const string Helpers = """
[[maybe_unused, gnu::noinline]] static bool valid_exception(const Context& c, Object* value) noexcept {
    return value != nullptr && traceable_reference(c.instance, value) && known_exception(value->type);
}
// Failure leaves the previous pending exception and its provenance unchanged.
// The caller publishes the message reference before a potentially collecting allocation.
[[maybe_unused, gnu::noinline]] static ExceptionResult make_exception(Context& c, const Descriptor* type, Object* message) noexcept {
    if (c.instance.marking || !known_exception(type) || !traceable_reference(c.instance, message)
        || (message != nullptr && !string_type(message->type))) return {6};
    const auto origin = exception_origin(c); c.pending = 0;
    Object* value = allocate<ExceptionObject>(c, *type);
    const auto failure = c.pending; set_exception_origin(c, origin);
    if (value == nullptr) return {failure == 0 ? 5u : failure};
    reinterpret_cast<ExceptionObject*>(value)->message = message;
    return {0, value};
}
[[maybe_unused, gnu::noinline]] static std::uint32_t prepare_exceptions(Context& c) noexcept {
    if (c.instance.marking) return 6;
    if (c.instance.emergency_exception != nullptr)
        return valid_exception(c, c.instance.emergency_exception) && c.instance.emergency_exception->type == runtime_exception(5) ? 0u : 6u;
    auto result = make_exception(c, runtime_exception(5), nullptr);
    if (result.status == 0) c.instance.emergency_exception = result.value;
    return result.status;
}
[[maybe_unused, gnu::noinline]] static ExceptionResult exception_message(Context& c, Object* value) noexcept {
    if (c.instance.marking) return {6};
    if (value == nullptr) return {4};
    if (!valid_exception(c, value)) return {6};
    Object* message = reinterpret_cast<ExceptionObject*>(value)->message;
    if (!traceable_reference(c.instance, message) || (message != nullptr && !string_type(message->type))) return {6};
    return {0, message};
}
// Turn a numeric runtime fault into a rooted managed identity before handler search.
// Status 13 denotes an explicitly thrown managed exception; 6/8/9 stay fatal.
[[maybe_unused, gnu::noinline]] static std::uint32_t materialize_exception(Context& c) noexcept {
    if (c.instance.marking) return 6;
    const Descriptor* type = runtime_exception(c.pending);
    if (c.pending == 0 || (type == nullptr && c.pending != 13)) return c.pending;
    if (c.exception != nullptr) {
        if (valid_exception(c, c.exception) && (c.pending == 13 || c.exception->type == type)) return c.pending;
        c.exception = nullptr; c.pending = 6; return 6;
    }
    if (type == nullptr) { c.pending = 6; return 6; }
    ExceptionResult result = c.pending == 5 ? ExceptionResult{5} : make_exception(c, type, nullptr);
    if (result.status == 0) c.exception = result.value;
    else if (result.status == 5 && valid_exception(c, c.instance.emergency_exception)
        && c.instance.emergency_exception->type == runtime_exception(5)) {
        c.exception = c.instance.emergency_exception; c.pending = 5;
    }
    else { c.exception = nullptr; c.pending = result.status == 5 ? 5u : 6u; }
    return c.pending;
}
[[maybe_unused, gnu::noinline]] static std::uint32_t raise_exception(Context& c, Object* value, const char* image, std::uint32_t method, std::uint32_t il) noexcept {
    if (c.instance.marking || c.pending != 0 || c.exception != nullptr || (value != nullptr && !valid_exception(c, value))) return 6;
    c.exception = value; set_exception_origin(c, {value == nullptr ? 4u : 13u, image, method, il});
    return materialize_exception(c);
}
// Caller-owned saved references belong to the published exception root slots.
[[maybe_unused, gnu::noinline]] static bool suspend_exception(Context& c, Object*& saved, ExceptionOrigin& origin) noexcept {
    if (c.instance.marking || c.pending == 0 || !valid_exception(c, c.exception)
        || (c.pending != 13 && c.exception->type != runtime_exception(c.pending))) return false;
    saved = c.exception; origin = exception_origin(c);
    c.exception = nullptr; set_exception_origin(c, {}); return true;
}
[[maybe_unused, gnu::noinline]] static bool restore_exception(Context& c, Object* saved, ExceptionOrigin origin) noexcept {
    if (c.instance.marking || c.pending != 0 || c.exception != nullptr || !valid_exception(c, saved)
        || (origin.status != 13 && saved->type != runtime_exception(origin.status))) return false;
    c.exception = saved; set_exception_origin(c, origin); return true;
}
""";
}
