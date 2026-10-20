namespace Cilica.Compiler;

/// <summary>Exact address membership; never reads a candidate descriptor.</summary>
internal static class ObjectCppDescriptorLookup
{
    internal const string Text = """
template<std::size_t Count, std::size_t Capacity> class DescriptorLookup {
    static_assert(Capacity >= Count * 2 && (Capacity & (Capacity - 1)) == 0);
    std::array<const Descriptor*, Capacity> slots{};
    static std::size_t bucket(const Descriptor* value) noexcept {
        const auto address = reinterpret_cast<std::uintptr_t>(value);
        return ((address >> 3) ^ (address >> 17)) & (Capacity - 1);
    }
public:
    // Construct directly in static storage: no allocation or table-sized stack temporary.
    explicit DescriptorLookup(const std::array<const Descriptor*, Count>& values) noexcept {
        for (const auto* value : values) {
            auto index = bucket(value);
            while (slots[index] != nullptr) index = (index + 1) & (Capacity - 1);
            slots[index] = value;
        }
    }
    bool contains(const Descriptor* value) const noexcept {
        if (value == nullptr) return false;
        auto index = bucket(value);
        while (slots[index] != nullptr) {
            if (slots[index] == value) return true;
            index = (index + 1) & (Capacity - 1);
        }
        return false;
    }
};
""";
}
