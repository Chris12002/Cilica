# Bounded typed helper inlining: v108 to v109

2026-10-20. Base `1b578c4`, isolated `codex/profile-v107` checkout, following
committed v107 `84a3839` and investigation `1e1e9a6`. Apple M4 Pro ARM64,
Apple Clang 17, ordinary .NET 10.0.11. Design and review:
[ADR 0216](../decisions/0216-bounded-typed-helper-inlining.md).

Paired release medians: **31.452 us/diff -> 30.205 us/diff**, **3.97% less time**.
Three alternating processes per variant, seven samples of 1,024 diffs each;
checksum 6146/diff throughout. C++20 O2/NDEBUG, no C++ exceptions/RTTI. Fresh
candidate emission uses this compiler; final compiler-source hashes match the
measurement snapshot. No build, profiler or sanitizer work overlapped timing.

All 39 proven kernels request optimized inlining. Four previously materialized
kernels (Dictionary equality/hash/find and string-key hashing) disappear from
the native symbol table. The executable shrinks from 570,600 to 569,336 bytes.
Generated benchmark bodies match v108 byte-for-byte after normalizing the profile,
new macro and kernel attributes. No owned-core source or IL lowering changes.

Forty-six selected checks pass, including six shared-module configurations
(294 exact .NET observations) with independent object compilation and no LTO.
They cover O0/O2, typed execution off/on, diagnostics off/on, shared function
identity, reference results, staged/aliased output writes and fault replay.
Additional probes retain the depth-eight and 512-expanded-statement bounds and
exclude recursion. Shared-module timing is not measured; its acceptance is
correctness and linkage across translation units.

All 523 DiffPlex observations match release and both O1 ASan/UBSan forced-GC
acceptance builds. Internal/borrow diagnostics are disabled in one sanitizer
build and enabled in the other. Each run requests a 512 KiB pthread stack and
checks the actual 512–576 KiB size. Maximum reported native frame sizes remain
432 bytes release and 384 bytes for both sanitizer builds. Self-review found no
remaining correctness issue. The build reports one existing unrelated fixture
member-hiding warning; native acceptance uses warnings as errors.

## Fresh combined comparison

A final interleaved batch retains all samples, including one unusually slow v107
sample (65.335 us). Each backend has three processes and 21 samples. Native runs
use 1,024 diffs/sample; .NET uses 16,384 after warmup, with tiering and ReadyToRun
disabled. Ratios below use medians from this same batch.

| Backend | Median us/diff | Ratio to .NET |
| --- | ---: | ---: |
| Cilica v107 | 35.052 | 6.46x |
| Cilica v108, small predicates | 31.454 | 5.80x |
| Cilica v109, both slices | 30.159 | 5.56x |
| Ordinary .NET | 5.426 | 1.00x |

Together the changes reduce execution time **13.96%**. These are local process
batches, not independent machines or confidence intervals. Unity was not retimed;
its retained generated output and matching local runtime informed the preceding
investigation. The .NET comparator is not NativeAOT.

Input DLL/PDB, host, native and compiler hashes, exact commands, raw timings and
validation results are retained in the adjacent evidence directory. Full generated
C++ and executables remain in `artifacts/v109-typed-inlining-20261020` in this
worktree. The owned-core SHA-256 remains
`655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
The primary checkout's uncommitted emitter rollback was left untouched.
