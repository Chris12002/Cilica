namespace Cilica.Compiler;

/// <summary>Unsigned storage and guarded signed operations, without nonstandard wider integer types.</summary>
internal static class Int64CppLowering
{
    internal const string Runtime = """
struct I8Result { std::uint64_t value; std::uint32_t fault; };
[[maybe_unused]] static I8Result cilica_binary_i8(unsigned operation, std::uint64_t a, std::uint64_t b) noexcept {
    constexpr std::uint64_t sign = std::uint64_t{1} << 63;
    constexpr std::uint64_t maximum = ~std::uint64_t{0};
    switch (operation) {
        case 0: return {a + b, 0};
        case 1: return {a - b, 0};
        case 2: return {a * b, 0};
        case 3: return {a & b, 0};
        case 4: return {a | b, 0};
        case 5: return {a ^ b, 0};
        case 6: return {a << (b & 63u), 0};
        case 7: {
            const unsigned count = static_cast<unsigned>(b & 63u);
            const std::uint64_t fill = count == 0 ? 0 : (std::uint64_t{0} - (a >> 63)) << (64u - count);
            return {(a >> count) | fill, 0};
        }
        case 8: return {a >> (b & 63u), 0};
        case 9: case 17: {
            if (b == 0) return {0, 1};
            if (a == sign && b == maximum) return {0, 2};
            const auto left = std::bit_cast<std::int64_t>(a), right = std::bit_cast<std::int64_t>(b);
            return {static_cast<std::uint64_t>(operation == 9 ? left / right : left % right), 0};
        }
        case 10: case 18:
            if (b == 0) return {0, 1};
            return {operation == 10 ? a / b : a % b, 0};
        case 11: {
            const auto result = a + b;
            return {result, ((a ^ result) & (b ^ result) & sign) != 0 ? 2u : 0u};
        }
        case 12: {
            const auto result = a - b;
            return {result, ((a ^ b) & (a ^ result) & sign) != 0 ? 2u : 0u};
        }
        case 13: {
            const bool negative = ((a ^ b) & sign) != 0;
            const auto x = (a & sign) != 0 ? std::uint64_t{0} - a : a;
            const auto y = (b & sign) != 0 ? std::uint64_t{0} - b : b;
            const auto limit = negative ? sign : sign - 1;
            if (y != 0 && x > limit / y) return {0, 2};
            const auto product = x * y;
            return {negative ? std::uint64_t{0} - product : product, 0};
        }
        case 14: { const auto result = a + b; return {result, result < a ? 2u : 0u}; }
        case 15: return {a - b, a < b ? 2u : 0u};
        case 16:
            if (b != 0 && a > maximum / b) return {0, 2};
            return {a * b, 0};
        default: return {0, 2}; // Unreachable after validated IR.
    }
}

""";
}
