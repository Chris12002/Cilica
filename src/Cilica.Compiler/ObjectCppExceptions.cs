using System;
using System.Globalization;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

internal static class ObjectCppExceptions
{
    internal static void AppendStorage(StringBuilder text) => text.Append("""
struct ExceptionObject { Object header; Object* message = nullptr; Object* parameter_name = nullptr; Object* inner = nullptr; Object* type_name = nullptr; Object* actual_value = nullptr; std::uint32_t message_kind = 0; const Descriptor* cast_from = nullptr; const Descriptor* cast_to = nullptr; bool cast_nullable = false; };
static_assert(std::is_standard_layout_v<ExceptionObject> && std::is_trivially_destructible_v<ExceptionObject> && offsetof(ExceptionObject, header) == 0);
static constexpr std::size_t exception_references[] = {offsetof(ExceptionObject, message), offsetof(ExceptionObject, parameter_name), offsetof(ExceptionObject, inner), offsetof(ExceptionObject, type_name), offsetof(ExceptionObject, actual_value)};
[[gnu::noinline]] static void* exception_field(Object* value, [[maybe_unused]] std::size_t offset) noexcept {
    auto* payload = reinterpret_cast<ExceptionObject*>(value);
    return offset == offsetof(ExceptionObject, message) ? &payload->message
        : offset == offsetof(ExceptionObject, parameter_name) ? &payload->parameter_name
        : offset == offsetof(ExceptionObject, inner) ? &payload->inner
        : offset == offsetof(ExceptionObject, type_name) ? &payload->type_name : &payload->actual_value;
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
        .Append("_descriptor = {sizeof(ExceptionObject), alignof(ExceptionObject), exception_references, 5, ")
        .Append("[](Object* p) noexcept { delete reinterpret_cast<ExceptionObject*>(p); }, &").Append(parent)
        .Append("_descriptor, nullptr, 0, nullptr, exception_field};\n");

    internal static void AppendHelpers(StringBuilder text, ManagedExceptionTypes types, ObjectTypeCatalog catalog, Func<ObjectTypeId, string> symbol, ImmutableArray<ExceptionCastText> casts, ImmutableArray<ValueTypeLayout> values)
    {
        text.Append("[[gnu::noinline]] static bool known_exception(const Descriptor* type) noexcept {\n");
        foreach (var type in types.Types) text.Append("    if (type == &").Append(symbol(type)).Append("_descriptor) return true;\n");
        text.Append("    return false;\n}\n[[gnu::noinline]] static const Descriptor* runtime_exception(std::uint32_t status) noexcept {\n    switch (status) {\n");
        foreach (var binding in types.RuntimeFaults)
            text.Append("    case ").Append(binding.Status.ToString(CultureInfo.InvariantCulture)).Append(": return &").Append(symbol(binding.Type)).Append("_descriptor;\n");
        text.Append("    default: return nullptr;\n    }\n}\n");
        text.Append("[[maybe_unused]] static bool type_initialization_exception([[maybe_unused]] const Descriptor* type) noexcept { return ")
            .Append(types.TypeInitialization is { } initialization ? "type == &" + symbol(initialization) + "_descriptor" : "false").Append("; }\n");
        text.Append("[[maybe_unused]] static const Descriptor* exception_string_type() noexcept { return ")
            .Append(catalog.String is { } selectedString ? "&" + symbol(selectedString) + "_descriptor" : "nullptr").Append("; }\n");
        var messages = ManagedExceptionMessages.Build(types, catalog);
        var resources = messages.Values.Distinct(StringComparer.Ordinal).Select((message, index) => (message, index)).ToDictionary(p => p.message, p => p.index, StringComparer.Ordinal);
        foreach (var resource in resources)
            text.Append("static constexpr std::uint16_t exception_text_").Append(resource.Value.ToString(CultureInfo.InvariantCulture)).Append("[] = {")
                .AppendJoin(", ", resource.Key.Select(ch => ((ushort)ch).ToString(CultureInfo.InvariantCulture))).Append("};\n");
        text.Append("struct ExceptionText { const std::uint16_t* units = nullptr; std::uint32_t length = 0; std::size_t cache_id = 0; };\n")
            .Append("struct ExceptionTextEntry { const Descriptor* type; std::uint32_t kind; ExceptionText text; };\nstatic constexpr ExceptionTextEntry exception_texts[] = {\n");
        foreach (var pair in messages)
            text.Append("    {&").Append(symbol(pair.Key.Type)).Append("_descriptor, ").Append(pair.Key.Kind.ToString(CultureInfo.InvariantCulture))
                .Append("u, {exception_text_").Append(resources[pair.Value].ToString(CultureInfo.InvariantCulture)).Append(", ").Append(pair.Value.Length.ToString(CultureInfo.InvariantCulture)).Append("u, ")
                .Append((pair.Value.StartsWith("Exception of type '", StringComparison.Ordinal) ? 0 : resources[pair.Value] + 1).ToString(CultureInfo.InvariantCulture)).Append("u}},\n");
        text.Append("};\n[[maybe_unused, gnu::noinline]] static ExceptionText exception_text(const Descriptor* type, std::uint32_t kind) noexcept {\n")
            .Append("    for (const auto& entry : exception_texts) if (entry.type == type && entry.kind == kind) return entry.text;\n    return {};\n}\n");
        text.Append("struct ExceptionCastTextEntry { const Descriptor* from; const Descriptor* to; bool nullable_target; ExceptionText text; };\n");
        for (int i = 0; i < casts.Length; ++i)
            text.Append("static constexpr std::uint16_t cast_text_").Append(i.ToString(CultureInfo.InvariantCulture)).Append("[] = {")
                .AppendJoin(", ", casts[i].Text.Select(ch => ((ushort)ch).ToString(CultureInfo.InvariantCulture))).Append("};\n");
        text.Append("static constexpr std::array<ExceptionCastTextEntry, ").Append(casts.Length.ToString(CultureInfo.InvariantCulture)).Append("> exception_cast_texts = {{\n");
        for (int i = 0; i < casts.Length; ++i)
        {
            var cast = casts[i];
            text.Append("{&").Append(cast.FromBox ? ObjectCppBoxing.Symbol(cast.From) : symbol(cast.From)).Append("_descriptor, &")
                .Append(cast.ToBox ? ObjectCppBoxing.Symbol(cast.To) : symbol(cast.To)).Append("_descriptor, ").Append(cast.NullableTarget ? "true" : "false").Append(", {cast_text_")
                .Append(i.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(cast.Text.Length.ToString(CultureInfo.InvariantCulture)).Append("u}},\n");
        }
        text.Append("}};\n[[maybe_unused, gnu::noinline]] static ExceptionText exception_cast_text(const Descriptor* from, const Descriptor* to, bool nullable_target) noexcept {\n")
            .Append("    for (const auto& entry : exception_cast_texts) if (entry.from == from && entry.to == to && entry.nullable_target == nullable_target) return entry.text;\n    return {};\n}\n");
        text.Append("[[maybe_unused, gnu::noinline]] static bool argument_exception(const Descriptor* type) noexcept {\n");
        if (types.ArgumentBase is { } argumentBase) text.Append("    return derives_from(type, &").Append(symbol(argumentBase)).Append("_descriptor);\n");
        else text.Append("    (void)type; return false;\n");
        text.Append("}\n");
        var actualInteger = values.IsDefault ? null : values.SingleOrDefault(t => t.Source.ContentHash == catalog.ObjectBase.ContentHash
            && t.Source.Name == "System.Int32" && t.ScalarBox == System.Reflection.Metadata.PrimitiveTypeCode.Int32);
        text.Append("[[maybe_unused, gnu::noinline]] static bool range_exception([[maybe_unused]] const Descriptor* type) noexcept { return ")
            .Append(types.Resources.SingleOrDefault(r => r.Kind == ExceptionResourceKind.ArgumentOutOfRange) is { } range
                ? "type == &" + symbol(range.Type) + "_descriptor" : "false").Append("; }\n");
        text.Append("[[maybe_unused, gnu::noinline]] static bool actual_integer([[maybe_unused]] Object* value, [[maybe_unused]] std::uint32_t& bits) noexcept {\n");
        if (actualInteger is not null)
        {
            string box = ObjectCppBoxing.Symbol(ObjectTypeId.From(actualInteger.Source));
            text.Append("    if (value != nullptr && value->type == &").Append(box).Append("_descriptor) { bits = reinterpret_cast<")
                .Append(box).Append("*>(value)->s0; return true; }\n");
        }
        text.Append("    return false;\n}\n");
        text.Append(ResourceHelper);
        text.Append("[[maybe_unused, gnu::noinline]] static Object* exception_message_string(Context& c, ExceptionText text) noexcept {\n");
        if (catalog.String is { } stringType) text.Append("    return exception_resource(c, ").Append(symbol(stringType)).Append("_descriptor, text);\n");
        else text.Append("    (void)c; (void)text; return nullptr;\n");
        text.Append("}\n").Append(Helpers).Append('\n');
    }

    private const string ResourceHelper = """
[[maybe_unused, gnu::noinline]] static Object* exception_resource(Context& c, const Descriptor& type, ExceptionText text) noexcept {
    if (text.cache_id == 0) return allocate_string(c, type, text.units, text.length, nullptr, 0);
    std::size_t count = 0;
    for (auto* value = c.instance.exception_resources; value != nullptr; value = value->literal_next) {
        if (++count > exception_resource_capacity || !traceable_reference(c.instance, reinterpret_cast<Object*>(value)) || value->header.type != &type) { c.pending = 6; return nullptr; }
        if (value->literal_id == text.cache_id) return &value->header;
    }
    if (count == exception_resource_capacity) { c.pending = 6; return nullptr; }
    auto* result = allocate_string(c, type, text.units, text.length, nullptr, 0);
    if (result == nullptr) return nullptr;
    auto* value = reinterpret_cast<StringObject*>(result);
    value->literal_id = text.cache_id; value->literal_next = c.instance.exception_resources; c.instance.exception_resources = value;
    return result;
}

""";

    private const string Helpers = """
[[maybe_unused, gnu::noinline]] static bool valid_exception(const Context& c, Object* value) noexcept {
    return value != nullptr && traceable_reference(c.instance, value) && known_exception(value->type);
}
// Failure leaves the previous pending exception and its provenance unchanged.
// The caller publishes the message reference before a potentially collecting allocation.
[[maybe_unused, gnu::noinline]] static ExceptionResult make_exception(Context& c, const Descriptor* type, Object* message, std::uint32_t message_kind = 0, Object* parameter_name = nullptr, Object* inner = nullptr, Object* type_name = nullptr, Object* actual_value = nullptr) noexcept {
    std::uint32_t actual_bits = 0;
    if (!traceable_reference(c.instance, actual_value) || (actual_value != nullptr && (!range_exception(type) || !actual_integer(actual_value, actual_bits)))
        || c.instance.marking || message_kind > 3 || (message_kind == 3 && (!type_initialization_exception(type) || message != nullptr || parameter_name != nullptr))
        || (message_kind != 3 && (type_name != nullptr || inner != nullptr)) || (inner != nullptr && !valid_exception(c, inner))
        || !traceable_reference(c.instance, type_name) || (type_name != nullptr && !string_type(type_name->type)) || !known_exception(type) || !traceable_reference(c.instance, message)
        || (message != nullptr && !string_type(message->type)) || !traceable_reference(c.instance, parameter_name)
        || (parameter_name != nullptr && (!argument_exception(type) || !string_type(parameter_name->type)))) return {6};
    const auto origin = exception_origin(c); c.pending = 0;
    Object* value = allocate<ExceptionObject>(c, *type);
    const auto failure = c.pending; set_exception_origin(c, origin);
    if (value == nullptr) return {failure == 0 ? 5u : failure};
    reinterpret_cast<ExceptionObject*>(value)->message = message;
    reinterpret_cast<ExceptionObject*>(value)->message_kind = message_kind;
    reinterpret_cast<ExceptionObject*>(value)->parameter_name = parameter_name;
    reinterpret_cast<ExceptionObject*>(value)->inner = inner;
    reinterpret_cast<ExceptionObject*>(value)->type_name = type_name;
    reinterpret_cast<ExceptionObject*>(value)->actual_value = actual_value;
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
[[gnu::noinline]] static ExceptionResult initialization_message(Context& c, ExceptionObject* payload) noexcept {
        Object* name = payload->type_name;
        if (!traceable_reference(c.instance, name) || (name != nullptr && !string_type(name->type))) return {6};
        const auto* descriptor = exception_string_type();
        if (descriptor == nullptr) return {6};
        static constexpr std::uint16_t prefix[] = {84, 104, 101, 32, 116, 121, 112, 101, 32, 105, 110, 105, 116, 105, 97, 108, 105, 122, 101, 114, 32, 102, 111, 114, 32, 39};
        static constexpr std::uint16_t suffix[] = {39, 32, 116, 104, 114, 101, 119, 32, 97, 110, 32, 101, 120, 99, 101, 112, 116, 105, 111, 110, 46};
        const auto* text = reinterpret_cast<StringObject*>(name);
        const auto origin = exception_origin(c); c.pending = 0;
        auto* message = allocate_string(c, *descriptor, prefix, 26, text == nullptr ? nullptr : text->units, text == nullptr ? 0 : text->length, suffix, 21);
        const auto failure = c.pending; set_exception_origin(c, origin);
        if (message == nullptr) return {failure == 0 ? 5u : failure};
        payload->message = message;
        return {0, message};
}
// Public construction eagerly formats the message; the partially built object
// remains rooted across that allocation and is never returned on failure.
[[maybe_unused, gnu::noinline]] static ExceptionResult make_initialization_exception(Context& c, const Descriptor* type, Object* name, Object* inner) noexcept {
    if (c.depth >= 64) return {8};
    auto result = make_exception(c, type, nullptr, 3, nullptr, inner, name);
    if (result.status != 0) return result;
    Frame frame; Object* root = result.value; RootScope scope(c, frame, &root, 1);
    auto formatted = initialization_message(c, reinterpret_cast<ExceptionObject*>(root));
    return formatted.status == 0 ? result : ExceptionResult{formatted.status};
}
[[gnu::noinline]] static ExceptionResult parameter_message(Context& c, Object* message, Object* parameter) noexcept {
        // Both inputs are now retained by the caller-rooted exception. Format in
        // one bounded allocation; no intermediate managed pointer crosses GC.
        static constexpr std::uint16_t prefix[] = {32, 40, 80, 97, 114, 97, 109, 101, 116, 101, 114, 32, 39};
        static constexpr std::uint16_t suffix[] = {39, 41};
        const auto* first = reinterpret_cast<StringObject*>(message);
        const auto* name = reinterpret_cast<StringObject*>(parameter);
        const auto origin = exception_origin(c); c.pending = 0;
        message = allocate_string(c, *message->type, first->units, first->length, prefix, 13, name->units, name->length, suffix, 2);
        const auto failure = c.pending; set_exception_origin(c, origin);
        if (message == nullptr) return {failure == 0 ? 5u : failure};
        return {0, message};
}
// Retain the intermediate base/parameter message across the final allocation.
// Int32 formatting uses unsigned arithmetic, including Int32.MinValue.
[[gnu::noinline]] static ExceptionResult actual_message(Context& c, Object* value, Object* message) noexcept {
    auto* actual = reinterpret_cast<ExceptionObject*>(value)->actual_value;
    if (actual == nullptr) return {0, message};
    std::uint32_t bits = 0;
    if (!range_exception(value->type) || !traceable_reference(c.instance, actual) || !actual_integer(actual, bits)) return {6};
    if (c.depth >= 64) return {8};
    Frame frame; Object* root = message; RootScope scope(c, frame, &root, 1);
    std::uint16_t number[11]; std::uint32_t start = 11;
    const bool negative = (bits & 0x80000000u) != 0;
    std::uint32_t magnitude = negative ? 0u - bits : bits;
    do { number[--start] = static_cast<std::uint16_t>(48u + magnitude % 10u); magnitude /= 10u; } while (magnitude != 0);
    if (negative) number[--start] = 45;
#ifdef _WIN32
    static constexpr std::uint16_t prefix[] = {13, 10, 65, 99, 116, 117, 97, 108, 32, 118, 97, 108, 117, 101, 32, 119, 97, 115, 32};
#else
    static constexpr std::uint16_t prefix[] = {10, 65, 99, 116, 117, 97, 108, 32, 118, 97, 108, 117, 101, 32, 119, 97, 115, 32};
#endif
    static constexpr std::uint16_t suffix[] = {46};
    const auto* text = reinterpret_cast<StringObject*>(root);
    const auto origin = exception_origin(c); c.pending = 0;
    auto* result = allocate_string(c, *root->type, text->units, text->length,
        prefix, static_cast<std::uint32_t>(sizeof(prefix) / sizeof(prefix[0])), number + start, 11u - start, suffix, 1);
    const auto failure = c.pending; set_exception_origin(c, origin);
    return result == nullptr ? ExceptionResult{failure == 0 ? 5u : failure} : ExceptionResult{0, result};
}
// Keep resource selection outside the allocation/validation frame, including at
// O0 with sanitizers. This does not allocate or dereference managed text storage.
[[gnu::noinline]] static ExceptionText selected_exception_text(Object* value) noexcept {
    const auto* payload = reinterpret_cast<ExceptionObject*>(value);
    if (payload->cast_from == nullptr && payload->cast_to == nullptr) return exception_text(value->type, payload->message_kind);
    if (value->type == runtime_exception(7)) return exception_cast_text(payload->cast_from, payload->cast_to, payload->cast_nullable);
    return {};
}
[[maybe_unused, gnu::noinline]] static ExceptionResult exception_message(Context& c, Object* value) noexcept {
    if (c.instance.marking) return {6};
    if (value == nullptr) return {4};
    if (!valid_exception(c, value)) return {6};
    auto* payload = reinterpret_cast<ExceptionObject*>(value);
    std::uint32_t actual_bits = 0;
    if (!traceable_reference(c.instance, payload->actual_value) || (payload->actual_value != nullptr
        && (!range_exception(value->type) || !actual_integer(payload->actual_value, actual_bits)))) return {6};
    if (payload->message_kind > 3 || (payload->message_kind == 3 && !type_initialization_exception(value->type))) return {6};
    Object* message = payload->message;
    Object* parameter = payload->parameter_name;
    if (!traceable_reference(c.instance, parameter) || (parameter != nullptr && (!argument_exception(value->type) || !string_type(parameter->type)))) return {6};
    if (!traceable_reference(c.instance, message) || (message != nullptr && !string_type(message->type))) return {6};
    if (payload->inner != nullptr && !valid_exception(c, payload->inner)) return {6};
    if (!traceable_reference(c.instance, payload->type_name) || (payload->type_name != nullptr && !string_type(payload->type_name->type))) return {6};
    if (payload->message_kind == 3) return message == nullptr ? initialization_message(c, payload) : ExceptionResult{0, message};
    if (message == nullptr) {
        const auto text = selected_exception_text(value);
        if (text.units == nullptr) return {6};
        const auto origin = exception_origin(c); c.pending = 0;
        message = exception_message_string(c, text);
        const auto failure = c.pending; set_exception_origin(c, origin);
        if (message == nullptr) return {failure == 0 ? 5u : failure};
        if (text.cache_id != 0 || payload->cast_from != nullptr || (parameter != nullptr && string_length(parameter) != 0)) payload->message = message;
    }
    if (parameter != nullptr && string_length(parameter) != 0) {
        auto formatted = parameter_message(c, message, parameter);
        if (formatted.status != 0) return formatted;
        message = formatted.value;
    }
    return actual_message(c, value, message);
}
[[maybe_unused, gnu::noinline]] static ExceptionResult exception_inner(Context& c, Object* value) noexcept {
    if (c.instance.marking) return {6};
    if (value == nullptr) return {4};
    if (!valid_exception(c, value)) return {6};
    auto* inner = reinterpret_cast<ExceptionObject*>(value)->inner;
    if (inner != nullptr && !valid_exception(c, inner)) return {6};
    return {0, inner};
}
[[maybe_unused, gnu::noinline]] static ExceptionResult exception_type_name(Context& c, Object* value) noexcept {
    if (c.instance.marking) return {6};
    if (value == nullptr) return {4};
    if (!valid_exception(c, value) || !type_initialization_exception(value->type)) return {6};
    auto* name = reinterpret_cast<ExceptionObject*>(value)->type_name;
    if (!traceable_reference(c.instance, name) || (name != nullptr && !string_type(name->type))) return {6};
    if (name != nullptr) return {0, name};
    const auto* descriptor = exception_string_type();
    if (descriptor == nullptr) return {6};
    const auto origin = exception_origin(c); c.pending = 0;
    auto* empty = string_literal(c, *descriptor, 0, nullptr, 0);
    const auto failure = c.pending; set_exception_origin(c, origin);
    return empty == nullptr ? ExceptionResult{failure == 0 ? 5u : failure} : ExceptionResult{0, empty};
}
[[maybe_unused, gnu::noinline]] static ExceptionResult exception_parameter_name(Context& c, Object* value) noexcept {
    if (c.instance.marking) return {6};
    if (value == nullptr) return {4};
    if (!valid_exception(c, value) || !argument_exception(value->type)) return {6};
    Object* parameter = reinterpret_cast<ExceptionObject*>(value)->parameter_name;
    if (!traceable_reference(c.instance, parameter) || (parameter != nullptr && !string_type(parameter->type))) return {6};
    return {0, parameter};
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
[[maybe_unused, gnu::noinline]] static void raise_cast_exception(Context& c, Object* value, const Descriptor* target, ExceptionOrigin origin, bool nullable_target = false) noexcept {
    set_exception_origin(c, origin);
    (void)materialize_exception(c);
    if (c.pending == 7 && c.exception != nullptr) {
        auto* payload = reinterpret_cast<ExceptionObject*>(c.exception);
        payload->cast_from = value->type; payload->cast_to = target; payload->cast_nullable = nullable_target;
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
