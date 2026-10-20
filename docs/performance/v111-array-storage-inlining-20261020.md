# Array-storage validation: v110 to v111

2026-10-20. Base 0e797b5, branch codex/profile-v107. Apple M4 Pro ARM64,
Apple Clang 17, ordinary .NET 10.0.11. Previous slice:
[v110 primitive reads](v110-primitive-reads-20261020.md).
[Design and self-review](../decisions/0218-array-storage-check-inlining.md).

Paired medians: **28.084 -> 26.906 us/diff**, **4.20% less execution time**.
Three alternating processes per variant, seven samples of 1,024 diffs each;
checksum 6146/diff. Native C++20 O2/NDEBUG, no C++ exceptions/RTTI. No builds,
profiling, tests or sanitizer work overlapped release timing. Fresh generated
C++ matches v110 byte-for-byte after normalizing only the storage-check attribute
macro and native profile. All compiler hashes still match the timing snapshot.

## Final interleaved comparison

Each backend has three processes and 21 retained samples. Native uses 1,024
diffs/sample; ordinary .NET uses 16,384 after warmup, with tiering and ReadyToRun
disabled. These are local process batches, not independent machines or confidence
intervals. All ratios below use this same final batch.

| Backend | Median us/diff | Ratio to .NET |
| --- | ---: | ---: |
| Cilica v109 | 30.463 | 5.57x |
| Cilica v110, primitive reads | 28.030 | 5.12x |
| Cilica v111, both slices | 26.994 | 4.93x |
| Ordinary .NET | 5.473 | 1.00x |

Together the slices reduce time **11.39%** in this batch. The paired slice
measurements above and in the v110 report differ slightly because of run-to-run
variation. The remaining gap is workload/platform-specific. Unity was not retimed;
its retained generated C++ and local runtime informed the preceding investigation.
The .NET comparator is not NativeAOT. No new optimization beyond these two slices
is included.

## Native code and correctness

| Native-code measure | v109 | v110 | v111 |
| --- | ---: | ---: | ---: |
| Static calls to six read helpers | 393 | 0 | 0 |
| Static calls to array-storage validation | 9 | 9 | 0 |
| Executable bytes | 569,336 | 568,536 | 568,360 |

These are disassembly call sites, not dynamic execution counts. Checks remain in
the caller. Code folding can label array_length as string_length, so both are
included in the read-call count.

All 54 distinct focused checks pass: 50 in the first run and four corruption
checks after correcting their empty-array test setup. The original setup changed
charged size without updating heap accounting; forced GC during bounds-exception
creation correctly rejected the heap. The corrected test uses an actual rooted
empty allocation. Initial/follow-up logs are retained; production code did not
change between those runs. Coverage includes O0/O2, typed execution off/on,
diagnostics off/on, UTF-16, 64-bit values, ordered faults, aliases and depth.
Shared-module tests compile independently without LTO, preserve function identity
and include 294 success/fault .NET observations across six lookup configurations.

All 523 DiffPlex observations match release and both O1 ASan/UBSan forced-GC builds,
with internal/borrow diagnostics off/on, on a requested 512 KiB pthread stack
(actual 512–576 KiB checked). Maximum native frames remain 432 bytes release and
384 bytes in both sanitizer modes. Managed builds report the existing fixture
CS0108 warning; native acceptance uses warnings as errors. Self-review found no
remaining issue. Shared-module speed is not measured.

[Evidence directory](v111-array-storage-inlining-20261020/) retains timing samples,
commands, DLL/PDB/host/core/compiler hashes, build/test logs, normalized comparison,
validation and native-code summaries. Full C++/executables/disassembly remain in
the matching artifacts folder in this worktree. The owned-core SHA-256 stays
655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1.
Local main (48ecf14) is an ancestor; no remote/hosted work was triggered. The
primary checkout's uncommitted emitter rollback remains untouched.
