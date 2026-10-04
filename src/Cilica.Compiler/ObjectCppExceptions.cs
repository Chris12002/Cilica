using System;
using System.Globalization;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

internal static class ObjectCppExceptions
{
    internal static void AppendStorage(StringBuilder text) => text.Append("""
struct ExceptionObject { Object header; Object* message = nullptr; std::uint32_t message_kind = 0; const Descriptor* cast_from = nullptr; const Descriptor* cast_to = nullptr; };
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

    internal static void AppendHelpers(StringBuilder text, ManagedExceptionTypes types, ObjectTypeCatalog catalog, Func<ObjectTypeId, string> symbol, ImmutableArray<ExceptionCastText> casts)
    {
        text.Append("[[gnu::noinline]] static bool known_exception(const Descriptor* type) noexcept {\n");
        foreach (var type in types.Types) text.Append("    if (type == &").Append(symbol(type)).Append("_descriptor) return true;\n");
        text.Append("    return false;\n}\n[[gnu::noinline]] static const Descriptor* runtime_exception(std::uint32_t status) noexcept {\n    switch (status) {\n");
        foreach (var binding in types.RuntimeFaults)
            text.Append("    case ").Append(binding.Status.ToString(CultureInfo.InvariantCulture)).Append(": return &").Append(symbol(binding.Type)).Append("_descriptor;\n");
        text.Append("    default: return nullptr;\n    }\n}\n");
        var messages = ManagedExceptionMessages.Build(types, catalog);
        var resources = messages.Values.Distinct(StringComparer.Ordinal).Select((message, index) => (message, index)).ToDictionary(p => p.message, p => p.index, StringComparer.Ordinal);
        foreach (var resource in resources)
            text.Append("static constexpr std::uint16_t exception_text_").Append(resource.Value.ToString(CultureInfo.InvariantCulture)).Append("[] = {")
                .AppendJoin(", ", resource.Key.Select(ch => ((ushort)ch).ToString(CultureInfo.InvariantCulture))).Append("};\n");
        text.Append("struct ExceptionText { const std::uint16_t* units = nullptr; std::uint32_t length = 0; };\n")
            .Append("struct ExceptionTextEntry { const Descriptor* type; std::uint32_t kind; ExceptionText text; };\nstatic constexpr ExceptionTextEntry exception_texts[] = {\n");
        foreach (var pair in messages)
            text.Append("    {&").Append(symbol(pair.Key.Type)).Append("_descriptor, ").Append(pair.Key.Kind.ToString(CultureInfo.InvariantCulture))
                .Append("u, {exception_text_").Append(resources[pair.Value].ToString(CultureInfo.InvariantCulture)).Append(", ").Append(pair.Value.Length.ToString(CultureInfo.InvariantCulture)).Append("u}},\n");
        text.Append("};\n[[maybe_unused, gnu::noinline]] static ExceptionText exception_text(const Descriptor* type, std::uint32_t kind) noexcept {\n")
            .Append("    for (const auto& entry : exception_texts) if (entry.type == type && entry.kind == kind) return entry.text;\n    return {};\n}\n");
        text.Append("struct ExceptionCastTextEntry { const Descriptor* from; const Descriptor* to; ExceptionText text; };\n");
        for (int i = 0; i < casts.Length; ++i)
            text.Append("static constexpr std::uint16_t cast_text_").Append(i.ToString(CultureInfo.InvariantCulture)).Append("[] = {")
                .AppendJoin(", ", casts[i].Text.Select(ch => ((ushort)ch).ToString(CultureInfo.InvariantCulture))).Append("};\n");
        text.Append("static constexpr std::array<ExceptionCastTextEntry, ").Append(casts.Length.ToString(CultureInfo.InvariantCulture)).Append("> exception_cast_texts = {{\n");
        for (int i = 0; i < casts.Length; ++i)
        {
            var cast = casts[i];
            text.Append("{&").Append(cast.FromBox ? ObjectCppBoxing.Symbol(cast.From) : symbol(cast.From)).Append("_descriptor, &")
                .Append(cast.ToBox ? ObjectCppBoxing.Symbol(cast.To) : symbol(cast.To)).Append("_descriptor, {cast_text_")
                .Append(i.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(cast.Text.Length.ToString(CultureInfo.InvariantCulture)).Append("u}},\n");
        }
        text.Append("}};\n[[maybe_unused, gnu::noinline]] static ExceptionText exception_cast_text(const Descriptor* from, const Descriptor* to) noexcept {\n")
            .Append("    for (const auto& entry : exception_cast_texts) if (entry.from == from && entry.to == to) return entry.text;\n    return {};\n}\n");
        text.Append("[[maybe_unused, gnu::noinline]] static Object* exception_message_string(Context& c, ExceptionText text) noexcept {\n");
        if (catalog.String is { } stringType) text.Append("    return allocate_string(c, ").Append(symbol(stringType)).Append("_descriptor, text.units, text.length, nullptr, 0);\n");
        else text.Append("    (void)c; (void)text; return nullptr;\n");
        text.Append("}\n").Append(Helpers).Append('\n');
    }

    private const string Helpers = """
[[maybe_unused, gnu::noinline]] static bool valid_exception(const Context& c, Object* value) noexcept {
    return value != nullptr && traceable_reference(c.instance, value) && known_exception(value->type);
}
// Failure leaves the previous pending exception and its provenance unchanged.
// The caller publishes the message reference before a potentially collecting allocation.
[[maybe_unused, gnu::noinline]] static ExceptionResult make_exception(Context& c, const Descriptor* type, Object* message, std::uint32_t message_kind = 0) noexcept {
    if (c.instance.marking || message_kind > 2 || !known_exception(type) || !traceable_reference(c.instance, message)
        || (message != nullptr && !string_type(message->type))) return {6};
    const auto origin = exception_origin(c); c.pending = 0;
    Object* value = allocate<ExceptionObject>(c, *type);
    const auto failure = c.pending; set_exception_origin(c, origin);
    if (value == nullptr) return {failure == 0 ? 5u : failure};
    reinterpret_cast<ExceptionObject*>(value)->message = message;
    reinterpret_cast<ExceptionObject*>(value)->message_kind = message_kind;
    return {0, value};
}
[[maybe_unused, gnu::noinline]] static std::uint32_t prepare_exceptions(Context& c) noexcept {
    if (c.instance.marking) return 6;
    if (c.instance.emergency_exception != nullptr)
        return valid_exception(c, c.instance.emergency_exception) && c.instance.emergency_exception->type == runtime_exception(5) ? 0u : 6u;
    auto result = make_exception(c, runtime_exception(5), nullptr, 2);
    if (result.status == 0) c.instance.emergency_exception = result.value;
    return result.status;
}
[[maybe_unused, gnu::noinline]] static ExceptionResult exception_message(Context& c, Object* value) noexcept {
    if (c.instance.marking) return {6};
    if (value == nullptr) return {4};
    if (!valid_exception(c, value)) return {6};
    auto* payload = reinterpret_cast<ExceptionObject*>(value);
    if (payload->message_kind > 2) return {6};
    Object* message = payload->message;
    if (!traceable_reference(c.instance, message) || (message != nullptr && !string_type(message->type))) return {6};
    if (message == nullptr) {
        const auto text = payload->cast_from == nullptr && payload->cast_to == nullptr ? exception_text(value->type, payload->message_kind)
            : value->type == runtime_exception(7) ? exception_cast_text(payload->cast_from, payload->cast_to) : ExceptionText{};
        if (text.units == nullptr) return {6};
        const auto origin = exception_origin(c); c.pending = 0;
        message = exception_message_string(c, text);
        const auto failure = c.pending; set_exception_origin(c, origin);
        if (message == nullptr) return {failure == 0 ? 5u : failure};
        payload->message = message;
    }
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
    ExceptionResult result = c.pending == 5 ? ExceptionResult{5} : make_exception(c, type, nullptr, 2);
    if (result.status == 0) c.exception = result.value;
    else if (result.status == 5 && valid_exception(c, c.instance.emergency_exception)
        && c.instance.emergency_exception->type == runtime_exception(5)) {
        c.exception = c.instance.emergency_exception; c.pending = 5;
    }
    else { c.exception = nullptr; c.pending = result.status == 5 ? 5u : 6u; }
    return c.pending;
}
[[maybe_unused, gnu::noinline]] static void raise_cast_exception(Context& c, Object* value, const Descriptor* target, ExceptionOrigin origin) noexcept {
    set_exception_origin(c, origin);
    (void)materialize_exception(c);
    if (c.pending == 7 && c.exception != nullptr) {
        auto* payload = reinterpret_cast<ExceptionObject*>(c.exception);
        payload->cast_from = value->type; payload->cast_to = target;
    }
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
