#include "entry.hpp"
#include "cases.hpp"
#include <cstring>
#include <iomanip>
#include <iostream>

// Diagnostic integration uses the private runtime directly. Production callers
// continue to use the packed boundary above; this does not define a handle ABI.
static bool stress_matches(CILICA_DEMO_SCOPE::Context& context, const Case& probe,
    std::uint32_t status, std::int32_t result, std::uint32_t method,
    std::uint32_t il, const char* image) noexcept
{
    using namespace CILICA_DEMO_SCOPE;
    context.pending = 0; context.method = 0; context.il = 0; context.image = nullptr;
    std::array<Argument, 3> arguments{};
    arguments[0].bits = std::bit_cast<std::uint32_t>(probe.turns);
    arguments[1].bits = std::bit_cast<std::uint32_t>(probe.selected);
    arguments[2].bits = std::bit_cast<std::uint32_t>(probe.mode);
    const auto actual = CILICA_DEMO_METHOD(context, arguments.data());
    if (context.pending != status || context.method != method || context.il != il) return false;
    if ((context.image == nullptr) != (image == nullptr)) return false;
    if (image != nullptr && std::strcmp(context.image, image) != 0) return false;
    if (status == 0 && actual != result) return false;
    if (context.top != nullptr || context.depth != 0 || context.used != 0) return false;
    if (collect(context).status != MarkStatus::complete) return false;
    const auto report = mark_reachable(context);
    return report.status == MarkStatus::complete && report.unreachable == 0;
}

int main()
{
    CILICA_DEMO_SCOPE::Instance stress_heap;
    stress_heap.collect_before_allocation = true;
    CILICA_DEMO_SCOPE::Context stress_context(stress_heap);
    for (const Case& probe : cases) {
        std::int32_t result = -123456789;
        std::uint32_t method = 0xffffffffu, il = 0xffffffffu;
        const char* image = nullptr;
        const std::int32_t parameters[] = {probe.turns, probe.selected, probe.mode};
        const std::uint32_t status = CILICA_DEMO_ENTRY(
            &result, &method, &il, &image, parameters, 3);
        if (!stress_matches(stress_context, probe, status, result, method, il, image)) return 4;
        if (status == 0u) {
            if (method != 0u || il != 0u || image != nullptr) return 2;
            std::cout << probe.label << ": success " << result << '\n';
        } else {
            if ((status != 2u && status != 4u && status != 10u) || result != -123456789 || image == nullptr) return 3;
            // Outputs occupy disjoint live storage; image borrows the generated library.
            std::cout << probe.label << ": fault " << status << " method=0x" << std::hex
                << std::setw(8) << std::setfill('0') << method << std::dec
                << " il=" << il << " image=" << image << '\n';
        }
    }
    return 0;
}
