#include "entry.hpp"
#include "cases.hpp"
#include <iomanip>
#include <iostream>

int main()
{
    for (const Case& probe : cases) {
        std::int32_t result = -123456789;
        std::uint32_t method = 0xffffffffu, il = 0xffffffffu;
        const char* image = nullptr;
        const std::uint32_t status = CILICA_DEMO_ENTRY(
            &result, &method, &il, &image, probe.initial, probe.step, probe.count);
        if (status == 0u) {
            if (method != 0u || il != 0u || image != nullptr) return 2;
            std::cout << probe.label << ": success " << result << '\n';
        } else {
            if (status != 2u || result != -123456789 || image == nullptr) return 3;
            // Outputs occupy disjoint live storage; image borrows the generated library.
            std::cout << probe.label << ": Overflow method=0x" << std::hex
                << std::setw(8) << std::setfill('0') << method << std::dec
                << " il=" << il << " image=" << image << '\n';
        }
    }
    return 0;
}
