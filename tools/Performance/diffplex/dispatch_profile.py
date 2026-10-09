"""Instrument one frozen native closure to quantify descriptor/dispatch cost."""
from pathlib import Path
import argparse
import re

from lazy_prefix import transform
from residual import scopes

HEADER = r'''
#include <chrono>
struct DispatchCounters {
    unsigned long long dispatch_calls = 0, dispatch_ns = 0;
    unsigned long long accepts_calls = 0, accepts_ns = 0, known_calls = 0;
};
static DispatchCounters dispatch_counters;
struct DispatchTimer {
    unsigned long long& total;
    std::chrono::steady_clock::time_point start;
    explicit DispatchTimer(unsigned long long& value) noexcept
        : total(value), start(std::chrono::steady_clock::now()) {}
    ~DispatchTimer() noexcept {
        total += std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::steady_clock::now() - start).count();
    }
};
'''


def variant(source: str, prefix: bool) -> str:
    text = transform(source) if prefix else source
    if prefix:
        # residual.scopes is shared with the baseline profiler and recognizes
        # the original root spelling; restore it only while adding diagnostics.
        text = text.replace(
            "if (!batch.contains(f->slots[i])) return false;",
            "if (!context.instance.owns(f->slots[i])) return false;")
    text = scopes(text)
    if prefix:
        text = text.replace(
            "if (!context.instance.owns(f->slots[i])) return false;",
            "if (!batch.contains(f->slots[i])) return false;")
    text = HEADER + text
    text = re.sub(
        r"(\[\[maybe_unused, gnu::noinline\]\] static bool [^\n]+_accepts\(const Descriptor\* type\) noexcept \{)",
        r"\1 DispatchTimer dispatch_timer(dispatch_counters.accepts_ns); ++dispatch_counters.accepts_calls;",
        text)
    text = re.sub(
        r"(\[\[maybe_unused, gnu::noinline\]\] static [^\n]+_dispatch\(const Descriptor\* type\) noexcept \{)",
        r"\1 DispatchTimer dispatch_timer(dispatch_counters.dispatch_ns); ++dispatch_counters.dispatch_calls;",
        text)
    text = text.replace(
        "static bool known_type(const Descriptor* type) noexcept {",
        "static bool known_type(const Descriptor* type) noexcept { ++dispatch_counters.known_calls;")
    text = text.replace(
        "residual={}; auto start=clock::now();",
        "residual={}; dispatch_counters={}; auto start=clock::now();")
    old = r'\"steps\":%llu}\n",'
    new = r'\"steps\":%llu,\"dispatch_calls\":%llu,\"dispatch_ns\":%llu,\"accepts_calls\":%llu,\"accepts_ns\":%llu,\"known_calls\":%llu}\n",'
    if old not in text:
        raise AssertionError("residual output format changed")
    text = text.replace(old, new)
    old_args = "residual.frames,residual.frame_bytes,residual.steps);"
    new_args = ("residual.frames,residual.frame_bytes,residual.steps,"
                "dispatch_counters.dispatch_calls,dispatch_counters.dispatch_ns,"
                "dispatch_counters.accepts_calls,dispatch_counters.accepts_ns,"
                "dispatch_counters.known_calls);")
    if old_args not in text:
        raise AssertionError("residual output arguments changed")
    return text.replace(old_args, new_args)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--prefix", action="store_true")
    args = parser.parse_args()
    args.output.write_text(variant(args.source.read_text(), args.prefix))
