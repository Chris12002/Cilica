# Proven field access: v105 to v106

2026-10-20. Base `8f21410`; Apple M4 Pro, macOS ARM64, Apple Clang 17.
Implementation: [ADR 0213](../decisions/0213-proven-native-field-access.md).

Paired release medians: **39.312 us/diff -> 36.831 us/diff**, **6.31% less time**.
Each variant has three alternating processes and seven samples of 256 diffs per
process, checksum 6146/diff. C++20 O2/NDEBUG, no C++ exceptions/RTTI. Baseline is
the previous slice's accepted generated binary. Candidate C++ was freshly emitted
with unchanged compiler-source hashes across emission. Timing ran without builds,
profiling or sanitizer work. .NET and Unity were not retimed for this slice.

All 46 focused field, inheritance, typed-borrow, getter and shared-module checks
pass. All 523 DiffPlex observations match release and O1 ASan/UBSan forced-GC
runs with internal/borrow diagnostics both disabled and enabled. Each acceptance
run requests a 512 KiB pthread stack and verifies 512–576 KiB actual capacity.
Commands, source hashes, stack sizes and raw samples are in the adjacent evidence
directory. No local-output borrow optimization is included in this slice.
