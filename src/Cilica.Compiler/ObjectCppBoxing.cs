using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Private typed boxed payloads, using the existing allocator, tracer and scalar carriers.</summary>
internal static class ObjectCppBoxing
{
    internal static string Symbol(ObjectTypeId type) => "box_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static void Append(StringBuilder text, ImmutableArray<ValueTypePlan> plans, string objectSymbol, Func<ObjectTypeId, string> predicate)
    {
        text.Append("// Cilica boxed-payload-runtime-v1: private helper checkpoint, not boxed IR emission.\n");
        foreach (var plan in plans)
        {
            string type = Symbol(plan.Type);
            var indices = Enumerable.Range(0, plan.Slots.Length).ToArray();
            string Offset(int i) => "offsetof(" + type + ", s" + N(i) + ")";
            text.Append("struct ").Append(type).Append(" { Object header;\n");
            foreach (int i in indices)
                text.Append("    ").Append(plan.Slots[i].Storage is ObjectStorage.Int32 ? "std::uint32_t" : "Object*")
                    .Append(" s").Append(N(i)).Append(";\n");
            text.Append("};\nstatic_assert(std::is_standard_layout_v<").Append(type).Append("> && std::is_trivially_destructible_v<")
                .Append(type).Append("> && offsetof(").Append(type).Append(", header) == 0);\n");
            foreach (int i in indices)
                text.Append("[[gnu::noinline]] static void* ").Append(type).Append("_slot_").Append(N(i))
                    .Append("(Object* p) noexcept { return &reinterpret_cast<").Append(type).Append("*>(p)->s").Append(N(i)).Append("; }\n");
            text.Append("static constexpr std::array<FieldAccessor, ").Append(N(indices.Length)).Append("> ").Append(type).Append("_accessors = {")
                .Append(string.Join(", ", indices.Select(i => type + "_slot_" + N(i)))).Append("};\n")
                .Append("static constexpr std::array<std::size_t, ").Append(N(indices.Length)).Append("> ").Append(type).Append("_fields = {")
                .Append(string.Join(", ", indices.Select(Offset))).Append("};\n");
            var references = indices.Where(i => plan.Slots[i].Storage is ObjectStorage.Reference).ToArray();
            text.Append("static constexpr std::array<std::size_t, ").Append(N(references.Length)).Append("> ").Append(type).Append("_references = {")
                .Append(string.Join(", ", references.Select(Offset))).Append("};\n")
                // Whole-value replacement may write readonly fields. Projected field
                // borrows carry readonly capability, just like inline frame storage.
                .Append("static constexpr std::array<FieldStorage, ").Append(N(indices.Length)).Append("> ").Append(type).Append("_storage = {{")
                .Append(string.Join(", ", plan.Slots.Select(s => s.Storage is ObjectStorage.Reference r
                    ? "{StorageKind::reference, false, " + predicate(r.Type) + "}" : "{StorageKind::integer, false, nullptr}"))).Append("}};\n")
                .Append("static constexpr Descriptor ").Append(type).Append("_descriptor = {sizeof(").Append(type).Append("), alignof(").Append(type)
                .Append("), ").Append(type).Append("_references.data(), ").Append(N(references.Length))
                .Append(", [](Object* p) noexcept { delete reinterpret_cast<").Append(type).Append("*>(p); }, &").Append(objectSymbol)
                .Append("_descriptor, ").Append(type).Append("_fields.data(), ").Append(N(indices.Length)).Append(", ").Append(type)
                .Append("_accessors.data(), field_by_offset, 0, nullptr, nullptr, nullptr, ").Append(type).Append("_storage.data()};\n");
        }
        text.Append("[[gnu::noinline]] static bool known_box(const Descriptor* type) noexcept {\n");
        foreach (var plan in plans) text.Append("    if (type == &").Append(Symbol(plan.Type)).Append("_descriptor) return true;\n");
        text.Append("    return false;\n}\n")
            .Append("[[gnu::noinline]] static Object* allocate_box_storage(Context& c, const Descriptor* type) noexcept {\n");
        foreach (var plan in plans) text.Append("    if (type == &").Append(Symbol(plan.Type)).Append("_descriptor) return allocate<")
            .Append(Symbol(plan.Type)).Append(">(c, *type);\n");
        text.Append("    return nullptr;\n}\n").Append(Helpers).Append('\n');
    }

    private const string Helpers = """
enum class BoxStatus { complete, null_reference, wrong_type, invalid, allocation, busy, read_only };
struct BoxResult { BoxStatus status; Object* value = nullptr; };
// Descriptor and packet extents are compiler-owned. Only exact emitted descriptor
// addresses are trusted; managed pointers are authenticated before dereference.
[[maybe_unused, gnu::noinline]] static BoxResult unbox(Context& c, Object* value, const Descriptor* expected) noexcept {
    if (c.instance.marking) return {BoxStatus::busy};
    if (value == nullptr) return {BoxStatus::null_reference};
    if (!known_box(expected) || !traceable_reference(c.instance, value)) return {BoxStatus::invalid};
    if (value->type != expected) return {BoxStatus::wrong_type};
    return {BoxStatus::complete, value};
}
[[gnu::noinline]] static bool valid_box_packet(Context& c, const Descriptor* type, const Argument* values, std::size_t count) noexcept {
    if (!known_box(type) || count != type->field_count || (count != 0 && values == nullptr)) return false;
    for (std::size_t i = 0; i < count; ++i)
        if (type->storage[i].kind == StorageKind::reference && !location_reference(c.instance, type->storage[i].accepts, values[i].reference)) return false;
    return true;
}
// No allocation or callbacks between full validation and the writes. Input/output
// packets are disjoint from payload slots and each other, like existing call packets.
[[gnu::noinline]] static void write_box_packet(Object* value, const Argument* source, std::size_t count) noexcept {
    for (std::size_t i = 0; i < count; ++i)
        if (value->type->storage[i].kind == StorageKind::integer)
            *static_cast<std::uint32_t*>(field_address(value, i)) = source[i].bits;
        else *static_cast<Object**>(field_address(value, i)) = source[i].reference;
}
[[maybe_unused, gnu::noinline]] static BoxResult box_value(Context& c, const Descriptor* type, const Argument* source, std::size_t count) noexcept {
    if (c.instance.marking) return {BoxStatus::busy};
    if (!valid_box_packet(c, type, source, count)) return {BoxStatus::invalid};
    // The caller publishes all source references across this allocation. Empty
    // payloads still allocate an owned header and receive fresh identity.
    Object* value = allocate_box_storage(c, type);
    if (value == nullptr) return {c.pending == 6u ? BoxStatus::invalid : BoxStatus::allocation};
    write_box_packet(value, source, count);
    return {BoxStatus::complete, value};
}
[[maybe_unused, gnu::noinline]] static BoxStatus copy_box(Context& c, Object* value, const Descriptor* type, Argument* output, std::size_t count) noexcept {
    const auto checked = unbox(c, value, type);
    if (checked.status != BoxStatus::complete) return checked.status;
    if (count != type->field_count || (count != 0 && output == nullptr)) return BoxStatus::invalid;
    // Validate the final reference too, before exposing even the first scalar.
    for (std::size_t i = 0; i < count; ++i)
        if (type->storage[i].kind == StorageKind::reference && !location_reference(c.instance, type->storage[i].accepts,
            *static_cast<Object**>(field_address(value, i)))) return BoxStatus::invalid;
    for (std::size_t i = 0; i < count; ++i)
        if (type->storage[i].kind == StorageKind::integer) output[i].bits = *static_cast<std::uint32_t*>(field_address(value, i));
        else output[i].reference = *static_cast<Object**>(field_address(value, i));
    return BoxStatus::complete;
}
[[maybe_unused, gnu::noinline]] static BoxStatus replace_box(Context& c, Object* value, const Descriptor* type, const Argument* source, std::size_t count,
    bool read_only = false) noexcept {
    const auto checked = unbox(c, value, type);
    if (checked.status != BoxStatus::complete) return checked.status;
    if (read_only) return BoxStatus::read_only;
    if (!valid_box_packet(c, type, source, count)) return BoxStatus::invalid;
    write_box_packet(value, source, count); return BoxStatus::complete;
}
""";
}
