# Primitive reads: v109 to v110

2026-10-20. Base 0717dc0 (v109 implementation 9706495), branch codex/profile-v107.
Apple M4 Pro ARM64, Apple Clang 17, unchanged pinned DiffPlex input and owned core.
[Design and self-review](../decisions/0217-primitive-read-inlining.md).

Paired medians: **30.193 -> 28.071 us/diff**, **7.03% less execution time**.
Three alternating processes per variant, seven samples of 1,024 diffs per process,
checksum 6146/diff. C++20 O2/NDEBUG, no C++ exceptions/RTTI. No build, tests,
sanitizers or profiler overlapped timing. Fresh candidate emission was checked
against v109: only the six read-helper attributes, new macro and profile differ.
This confirms the earlier generated-C++ experiment in the production emitter.

All 54 selected checks pass: NativeArray, NativeString, NativeInteger64,
NativeTypedLookup, NativeTypedBorrow, NativeTypedHelper, NativeLocalOutput and
NativeModule. Shared modules compile independently without LTO and include
294 success/fault observations against .NET in six lookup configurations.
Existing tests exercise UTF-16 including unpaired surrogates, all 64-bit patterns,
null/bounds order, roots, descriptor rejection, alias staging and depth limits.

All 523 DiffPlex observations match release and both O1 ASan/UBSan forced-GC
builds, diagnostics off/on, on a requested 512 KiB stack (actual 512–576 KiB
checked). Maximum native frames: 432 bytes release, 384 bytes both sanitizer
modes. The managed build has one pre-existing fixture CS0108 warning; native
acceptance treats warnings as errors. Review found no remaining correctness issue.

[Evidence directory](v110-primitive-reads-20261020/) retains raw timings, commands,
input/compiler/core hashes, build/test logs, normalized comparison and validation
results. Generated C++ and executables remain under the matching artifacts folder
in this worktree. This is local single-file DiffPlex timing; shared-module speed
and Unity speed were not measured. Ordinary .NET will be retimed with the second
slice. The primary checkout's unrelated emitter rollback is untouched.
