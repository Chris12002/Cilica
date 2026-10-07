using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
namespace Cilica.Compiler;
internal static class ObjectCppEnums
{
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    internal static void Append(StringBuilder text, ImmutableArray<ValueTypeLayout> values,
        IReadOnlyDictionary<ObjectTypeId, int> identities)
    {
        text.Append("struct EnumText { const std::uint16_t* units; std::uint32_t length; };\n")
            .Append("struct EnumDefinition { std::size_t identity; const std::uint32_t* values; const EnumText* names; std::size_t count; EnumText ending; };\n")
            .Append("struct EnumValueInfo { const Descriptor* type; std::uint32_t kind; std::size_t identity; EnumText name; };\n");
        int next = 0;
        string Literal(string value)
        {
            string name = "enum_text_" + N(next++);
            text.Append("static constexpr std::array<std::uint16_t, ").Append(N(value.Length)).Append("> ").Append(name).Append(" = {")
                .AppendJoin(", ", value.Select(ch => N(ch))).Append("};\n");
            return "{" + name + ".data(), " + N(value.Length) + "u}";
        }
        string enumPrefix = Literal("Object must be the same type as the enum. The type passed in was '");
        string enumMiddle = Literal("'; the enum type was '");
        string integerPrefix = Literal("Enum underlying type and the object must be same type or object must be a String. Type passed in was '");
        string integerMiddle = Literal("'; the enum underlying type was '");
        string integerEnding = Literal("System.Int32'.");
        var definitions = new List<string>(); var payloads = new List<string>();
        foreach (var type in values.Where(t => t.ScalarBox is not null))
        {
            var id = ObjectTypeId.From(type.Source); string name = Literal(EnumQueryMetadata.Name(type.Source));
            int kind = type.ScalarEnum ? 3 : type.ScalarBox == PrimitiveTypeCode.Int32 ? 1
                : type.ScalarBox is PrimitiveTypeCode.SByte or PrimitiveTypeCode.Byte or PrimitiveTypeCode.Int16 or PrimitiveTypeCode.UInt16
                    or PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 or PrimitiveTypeCode.Char ? 2 : 0;
            payloads.Add("{&" + ObjectCppBoxing.Symbol(id) + "_descriptor, " + N(kind) + "u, " + N(identities[id]) + "u, " + name + "}");
            if (!type.ScalarEnum) continue;
            var names = type.EnumConstants.Select(c => Literal(c.Name)).ToArray();
            string symbol = "enum_definition_" + N(definitions.Count);
            string ending = Literal(EnumQueryMetadata.Name(type.Source) + "'.");
            text.Append("static constexpr std::array<std::uint32_t, ").Append(N(names.Length)).Append("> ").Append(symbol).Append("_values = {")
                .AppendJoin(", ", type.EnumConstants.Select(c => unchecked((uint)c.Value).ToString(CultureInfo.InvariantCulture) + "u")).Append("};\n")
                .Append("static constexpr std::array<EnumText, ").Append(N(names.Length)).Append("> ").Append(symbol).Append("_names = {{")
                .AppendJoin(", ", names).Append("}};\n");
            definitions.Add("{" + N(identities[id]) + "u, " + symbol + "_values.data(), " + symbol + "_names.data(), " + N(names.Length) + "u, " + ending + "}");
        }
        text.Append("static constexpr std::array<EnumDefinition, ").Append(N(definitions.Count)).Append("> enum_definitions = {{")
            .AppendJoin(", ", definitions).Append("}};\n")
            .Append("static constexpr std::array<EnumValueInfo, ").Append(N(payloads.Count)).Append("> enum_payloads = {{")
            .AppendJoin(", ", payloads).Append("}};\n")
            .Append("static constexpr EnumText enum_error_prefix = ").Append(enumPrefix).Append(", enum_error_middle = ").Append(enumMiddle)
            .Append(", enum_integer_prefix = ").Append(integerPrefix).Append(", enum_integer_middle = ").Append(integerMiddle)
            .Append(", enum_integer_ending = ").Append(integerEnding).Append(";\n").Append(Helpers).Append('\n');
    }
    private const string Helpers = """
[[maybe_unused, gnu::noinline]] static const EnumDefinition* enum_definition(std::size_t identity) noexcept {
    for (const auto& entry : enum_definitions) if (entry.identity == identity) return &entry;
    return nullptr;
}
[[maybe_unused, gnu::noinline]] static const EnumValueInfo* enum_payload(const Descriptor* type) noexcept {
    for (const auto& entry : enum_payloads) if (entry.type == type) return &entry;
    return nullptr;
}
// 0/1 membership, 2 non-enum Type, 3 enum mismatch, 4 integer mismatch, 5 unknown value kind.
[[maybe_unused, gnu::noinline]] static std::uint32_t enum_query(Context& c, Object* type, Object* value) noexcept {
    if (type == nullptr || value == nullptr) { c.pending = 4u; return 0; }
    if (!traceable_reference(c.instance, type) || !traceable_reference(c.instance, value)
        || !runtime_type_storage(type->type)) { c.pending = 6u; return 0; }
    auto identity = runtime_type_hash(c, type);
    if (c.pending != 0u || identity == 0u) return 0;
    const auto* definition = enum_definition(identity - 1u);
    if (definition == nullptr) return 2;
    const auto* payload = enum_payload(value->type);
    if (payload != nullptr && payload->kind == 3u && payload->identity != definition->identity) return 3;
    if (string_type(value->type)) {
        const auto* candidate = reinterpret_cast<const StringObject*>(value);
        for (std::size_t i = 0; i < definition->count; ++i) {
            const auto name = definition->names[i];
            if (name.length != candidate->length) continue;
            bool same = true;
            for (std::uint32_t j = 0; j < name.length; ++j) if (name.units[j] != candidate->units[j]) { same = false; break; }
            if (same) return 1;
        }
        return 0;
    }
    if (payload == nullptr || payload->kind == 0u) return 5;
    if (payload->kind == 2u) return 4;
    // Exact generated Int32/enum descriptor proves the single aligned UInt32 slot.
    const auto bits = *static_cast<const std::uint32_t*>(value->type->accessors[0](value));
    for (std::size_t i = 0; i < definition->count; ++i) if (definition->values[i] == bits) return 1;
    return 0;
}
[[maybe_unused, gnu::noinline]] static Object* enum_error_message(Context& c, const Descriptor& text_type, Object* type, Object* value) noexcept {
    const auto status = enum_query(c, type, value);
    if (c.pending != 0u) return nullptr;
    if (status != 3u && status != 4u) { c.pending = 6u; return nullptr; }
    const auto* payload = enum_payload(value->type);
    const auto* definition = enum_definition(runtime_type_hash(c, type) - 1u);
    if (payload == nullptr || definition == nullptr || c.pending != 0u || !string_type(&text_type)) { c.pending = 6u; return nullptr; }
    const auto prefix = status == 3u ? enum_error_prefix : enum_integer_prefix;
    const auto middle = status == 3u ? enum_error_middle : enum_integer_middle;
    const auto ending = status == 3u ? definition->ending : enum_integer_ending;
    // All four spans point into immutable compiler-owned tables across allocation/GC.
    return allocate_string(c, text_type, prefix.units, prefix.length, payload->name.units, payload->name.length,
        middle.units, middle.length, ending.units, ending.length);
}
""";
}
