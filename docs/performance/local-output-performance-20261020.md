# Proven local output borrows: v106 to v107

2026-10-20. Base `88bd156`; Apple M4 Pro, macOS ARM64, Apple Clang 17.
Implementation: [ADR 0214](../decisions/0214-proven-local-output-borrows.md).

Paired release medians: **37.912 us/diff -> 35.035 us/diff**, **7.59% less time**.
Each variant has three alternating processes and seven samples of 256 diffs per
process, checksum 6146/diff. C++20 O2/NDEBUG, no C++ exceptions/RTTI. The baseline
is the preceding accepted field-access binary. Candidate C++ was freshly emitted;
compiler-source hashes were stable through emission and final review. No build,
profiling or sanitizer work overlapped timing. Exact generated calls show the
Dictionary TryGetValue output receiving the caller's local integer slot directly.

All 37 selected local-output, typed-borrow, character-helper and module checks pass
(36 in the full batch, plus the corrected depth-status assertion in a separate
one-test run). New probes cover success, null/bounds faults after writing an output,
wide outputs, interface targets, alias fallback, diagnostic modes and separate
translation units at depth 64. The first parallel build failed because MSBuild
workers exited; the serial build succeeded. No compiler change was needed for it.

All 523 DiffPlex observations match release and both O1 ASan/UBSan forced-GC
builds, with internal/borrow diagnostics disabled and enabled. Each uses a 512 KiB
requested pthread stack, checks actual 512–576 KiB, and compiles with warnings as
errors. Maximum reported native frame sizes are 432 bytes release and 320 bytes
in both O1 sanitizer builds; these are separate from the managed frame arena.

## Combined result and fresh .NET comparison

A final interleaved batch compares the original v104 binary with freshly generated
v107 and ordinary .NET 10.0.11, with the same DLL and checksum throughout:

| Backend | Median us/diff | Ratio to .NET |
| --- | ---: | ---: |
| Cilica v104 | 45.648 | 8.26x |
| Cilica v107, all three slices | 34.653 | 6.27x |
| .NET, tiering/ReadyToRun disabled | 5.528 | 1.00x |

All three changes together reduce time **24.09%** in this batch. The measured gap
is now about **6.3x**, compared with the preceding roughly 8.4x result. Each backend
has three processes and 21 samples; .NET uses 16,384 diffs/sample after warmup.
These are process batches, not independent machines or confidence intervals.
Unity was not rebuilt or retimed, and these results do not establish other workload
or platform performance. Managed allocation counts and live-byte quotas remain.
String header reserve trades bounded additional native memory for fewer allocator
calls; see the allocator slice for its 384.125 KiB cap on this target.

The input DLL/PDB, host, native binary and compiler hashes, exact commands, all raw
samples and sanitizer results are retained in the adjacent evidence directory.
The owned core hash remains `655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
