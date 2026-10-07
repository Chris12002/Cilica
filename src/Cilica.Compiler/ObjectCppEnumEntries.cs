using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Cilica.Compiler;
internal static class ObjectCppEnumEntries
{
    internal static string Symbol(ObjectTypeId type) => "enum_entries_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    internal static void Append(StringBuilder text, ImmutableArray<ValueTypeLayout> types)
    {
        text.Append("struct EnumConstantEntry { std::uint32_t value; const std::uint16_t* name; std::uint32_t length; };\n");
        foreach (var type in types.Where(t => t.ScalarEnum && !t.EnumConstants.IsDefault))
        {
            string symbol = Symbol(ObjectTypeId.From(type.Source));
            var constants = type.EnumConstants.OrderBy(c => unchecked((uint)c.Value)).ToArray();
            for (int i = 0; i < constants.Length; i++)
                text.Append("static constexpr std::array<std::uint16_t, ").Append(N(constants[i].Name.Length)).Append("> ").Append(symbol).Append("_name_").Append(N(i)).Append(" = {")
                    .AppendJoin(", ", constants[i].Name.Select(ch => N(ch))).Append("};\n");
            text.Append("static constexpr std::array<EnumConstantEntry, ").Append(N(constants.Length)).Append("> ").Append(symbol).Append(" = {{")
                .AppendJoin(", ", constants.Select((c, i) => "{" + unchecked((uint)c.Value).ToString(CultureInfo.InvariantCulture) + "u, " + symbol + "_name_" + N(i) + ".data(), " + N(c.Name.Length) + "u}"))
                .Append("}};\n");
        }
        text.Append(Helpers).Append('\n');
    }
    private const string Helpers = """
[[maybe_unused, gnu::noinline]] static std::uint32_t enum_entry_value(Context& c, const EnumConstantEntry* entries, std::size_t count, std::uint32_t index) noexcept {
    if (index >= count) { c.pending = 6u; return 0; }
    return entries[index].value;
}
[[maybe_unused, gnu::noinline]] static Object* enum_entry_name(Context& c, const Descriptor& text_type, const EnumConstantEntry* entries, std::size_t count, std::uint32_t index) noexcept {
    if (index >= count || !string_type(&text_type)) { c.pending = 6u; return nullptr; }
    // The immutable compiler-owned UTF-16 table survives allocation and collection.
    const auto& entry = entries[index];
    return allocate_string(c, text_type, entry.name, entry.length, nullptr, 0);
}
""";
}
