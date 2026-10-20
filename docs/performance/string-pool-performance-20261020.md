# Bounded string allocator: v104 to v105

2026-10-20. Base `8ef9323`; Apple M4 Pro, macOS ARM64, Apple Clang 17.
Implementation: [ADR 0212](../decisions/0212-bounded-string-header-pool.md).

Paired release medians: **44.919 us/diff -> 39.609 us/diff**, **11.82% less time**.
Each variant has three alternating processes and seven samples of 256 diffs per
process, checksum 6146/diff. C++20 O2/NDEBUG, no C++ exceptions/RTTI.
The baseline is the unchanged v104 compiler output from the preceding fresh
investigation. Candidate C++ was regenerated from this compiler; source hashes
were stable across emission. Build/counter/sanitizer work did not overlap timing.
Raw samples, candidate hashes and commands are in the adjacent evidence directory.
.NET and Unity were not retimed for this individual slice.

The production pool adds external free-list links so ASan can poison recycled
objects. On this target the 16-slab reserve cap is 393,344 bytes (384.125 KiB);
reserve is allocated lazily. Occupied slots overlap managed live-byte charges.
This is not an RSS measurement and is not added to the managed quota. Ordinary
allocation remains available beyond the reserve budget.

All 21 focused checks pass: NativeStringPoolTests, NativeInlineStringTests,
NativeCharacterHelperTests and NativeStringTests. They include O0/O2 sanitizers,
full UInt16 coverage on the ordinary allocation path, pool live identity,
recycling, cross-instance ownership, capacity/failure recovery, poisoned dead
slots, character helper origins and separate module linkage.

All 523 DiffPlex observations match in release and both O1 ASan/UBSan forced-GC
builds: internal/borrow diagnostics off and on. The pthread requests 512 KiB and
checks actual size within 512–576 KiB. Exact frame sizes are retained in
acceptance-results.json. Self-review checked publication, roots, teardown, mixed
pooled/ordinary allocation, budget subtraction and poisoned-slot lifetime.
No field access or local-output optimization is included in this slice.
