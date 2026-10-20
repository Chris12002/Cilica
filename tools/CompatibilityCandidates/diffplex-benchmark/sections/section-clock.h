#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
namespace section_clock {
inline bool enabled = false;
inline int current = -1;
inline std::int64_t last = 0, ticks[4]{}, calls[4]{};
inline std::int64_t now() {
    return std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}
inline void reset() {
    if (current != -1) std::abort();
    for (int i = 0; i < 4; ++i) ticks[i] = calls[i] = 0;
}
struct Scope {
    bool active;
    int previous;
    explicit Scope(int section) : active(enabled), previous(current) {
        if (!active) return;
        auto time = now();
        if (previous >= 0) ticks[previous] += time - last;
        current = section; last = time; ++calls[section];
    }
    ~Scope() {
        if (!active) return;
        auto time = now();
        ticks[current] += time - last; current = previous; last = time;
    }
};
}
