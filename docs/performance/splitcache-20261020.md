# Bounded separator cache: DiffPlex profiling and accepted improvement

2026-10-20. Started from committed v115 (`813450f`, compiler `66287c4`) in
`codex/profile-v107`. Fresh emission is byte-identical to the accepted v115 C++.
This slice changes the owned String.Split(String[],StringSplitOptions) algorithm;
compiler sources, native v115, runtime profile, grants and public capabilities
are unchanged. It benefits callers of that overload without recognizing DiffPlex
methods or altering the benchmark input.

## Outcome

The final interleaved batch reduces line-diff execution time **15.86%**, from
**65.974 to 55.509 µs/diff**. An earlier paired batch measured **19.80%**, from
67.314 to 53.986 µs. Both retained batches show a substantial improvement; do not
replace the final comparison with the best observed time.

| Line workload | Median µs/diff | Ratio to .NET |
|---|---:|---:|
| Committed v115 before | 65.974 | 9.43× |
| v115 plus separator cache | 55.509 | 7.94× |
| Ordinary .NET | 6.993 | 1.00× |

Character-diff time is effectively unchanged: **23.674
→ 23.543 µs** in the final batch, versus
**26.441 µs Unity** and **5.465 µs .NET**.
This path does not use the changed Split overload; small differences do not
establish a character-workload benefit. Its .NET gap remains 4.31×.
The current Unity harness has no line workload; no Unity line result is claimed.

## What the fresh profile showed

Two three-second samples per workload at a one-millisecond requested interval,
with checked complete benchmark checksums. Exclusive shares below use all sampled
stacks, including allocator/system work; inlined callees remain attributed to their
native caller. They are estimates, not additive predictions of speedup.

| Committed v115 hotspot | Character samples / 3,610 | Line samples / 5,140 |
|---|---:|---:|
| Dictionary.TryGetValue | 505 / 13.99% | 175 / 3.40% |
| Dictionary.Insert | 211 / 5.84% | 116 / 2.26% |
| Char.ToString | 237 / 6.57% | — |
| Collector membership preparation | 191 / 5.29% | 159 / 3.09% |
| MatchStringSeparator | — | 1,611 / 31.34% |
| CopySubstring | — | 388 / 7.55% |
| String.Split | — | 268 / 5.21% |

The largest isolated line cost was repeated separator matching. The old algorithm
searched the entire string once to count entries, then searched it again while
constructing them. Caching the scan results was a more direct target than another
small helper-inlining change. Dictionary lookup/insertion remains the leading
character-workload investigation; this slice does not claim to remove that cost.

## Roslyn, .NET and Unity comparison

The approved DiffPlex DLL/PDB is unchanged: DLL
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`, PDB
`4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8`.
Its recorded Roslyn build is C# 14 Release/AnyCPU,
5.9.0-1.26379.115+14fbf8d5271c98133561eb55185fdb05b286f578.
LineChunker.Chunk IL_0008 calls String.Split; Roslyn does not prescribe a second
separator scan. The retained Roslyn source checkout is a reference revision,
not an exact source match to that build.

Pinned .NET String.Manipulation.cs uses MakeSeparatorListAny and two
ValueListBuilder buffers for separator positions and lengths. Matching Unity
6000.3.2f1 generated String_MakeSeparatorList likewise records them while retaining
first-matching-separator precedence and nonoverlapping consumption. The selected
change adopts that algorithmic idea using Cilica's ordinary rooted managed storage.
It adds no stackalloc/span/pool dependency, native intrinsic or metadata grant.

Baseline owned core SHA is
`655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`;
new core SHA is `99981b6fa6a9076564586ac4d8105e44de387e998263feb0dc716fcafda87eb4`.
Fresh REA 6.7.0 static member inspection and System.Reflection.Metadata agree on
method tokens, RVAs and IL sizes. ILSpy 9.1.0 supplies fresh disassembly. String-array
Split remains token 060002e3, MatchStringSeparator 060002e4 and CopySubstring
060002d8. The owned core is Release optimized C# 9 with debug output disabled;
its build arguments/inputs are retained separately from the benchmark's Roslyn
options. Inspections read assemblies as data; target managed code is not loaded.

[Baseline references](v115-profile-next-20261020/reference-excerpts.json) include
line-addressed input IL, .NET, Unity and Roslyn excerpts with source hashes.
[New IL](splitcache-20261020/selected-core.il),
[REA records](splitcache-20261020/rea-selected.json) and
[IL-to-C++ anchors](splitcache-20261020/core-trace.json) describe the changed core.
Old REA bodies are only used for the hash-matched baseline.

## Algorithm and memory budget

The first scan saves at most 64 `(position, width)` pairs in one managed Int32
array allocated at the first match. Its length is `2 * min(input.Length, 64)`:
**at most 512 bytes of payload plus one ArrayObject header**. No-match input
allocates no separator cache. Counting and entry filtering retain their existing
semantics. Output construction consumes the cached prefix, then rescans only the
uncached suffix if more than 64 matches occurred. There is no new maximum input
length or result count, and no buffer growth or extra per-match allocation.

The cache is traced through ordinary frame roots. It is charged to the existing
managed heap quota, so sufficiently tight quotas can fail earlier than with the
old rescanning implementation. Tests verify managed allocation faults, complete
unwinding, temporary reclamation and recovery; limits are not increased. This
explicit bounded memory tradeoff buys execution speed. The char-separator overload,
public result identities, no-match shortcuts, trimming, ignored null/empty
separators and whitespace fallback remain unchanged. Concurrent separator mutation
remains outside the existing single-threaded profile.

## Acceptance and review

All **37 focused tests pass**: existing separator, char-split and substring
checks plus five new cache checks. The new differential probe covers 0, 1, 31,
32, 33, 63, 64, 65, 80 and 129 segments, four separator scenarios and all four
supported option combinations, producing **960 repeated O0/O2 native/.NET
comparisons**. It includes overlapping separator precedence, unpaired UTF-16,
all-empty trimming, null whitespace separators, empty-array/string identities,
and a later allocation while results remain live.

Two native quota probes fail and recover through cache, result-array, temporary
substring-buffer and string allocations with forced collection and ASan/UBSan.
A separately compiled shared-module probe executes the cached prefix and fallback
with forced collection; no LTO is used. Existing checks cover all UTF-16 whitespace
units, argument evaluation/fault order, mutated separator arrays between calls
and unsupported overload admission.

All 523 character and 2,061 line DiffPlex observations match the .NET oracle in
release and both O1 ASan/UBSan forced-collection configurations (internal/borrow
diagnostics off/on). Both run on a checked 512–576 KiB thread stack. Final maximum
frames are 432 bytes in the timing binary and 544/448/480 bytes
in release/diagnostic-off/diagnostic-on acceptance harnesses. The timing binary is
599,432 bytes. The extra cache uses managed heap storage, not native stack.

Review checked positive/even cache capacity, bounded index arithmetic, complete
pairs, exact cached-to-rescan transition, unchanged fault/identity rules, roots
across all allocations, quota accounting, callback-free separator stability and
shared-module execution. No unresolved review finding. No compiler eligibility,
public ABI, grant or compatibility admission rule was changed.

## Measurement and follow-up

Apple M4 Pro ARM64, Apple Clang 17, C++20 O2/NDEBUG, no C++ exceptions/RTTI.
Three processes per variant and seven samples each, warmed for at least 300 ms;
rotated/reversed order. Native uses 1,024 diffs/sample, .NET 16,384 for characters
and 8,192 for lines; Unity uses 4,096. .NET 10.0.11 has tiering and ReadyToRun
disabled and is ordinary .NET, not NativeAOT. Unity is the existing retained
Release/OptimizeSpeed player, retimed without rebuilding. Release timing runs
separately from builds, tests, sanitizers and sampling. These are local process
batches, not independent-machine confidence intervals or general library claims.

A post-change line profile contains 4,648 exclusive samples: separator matching
falls to **851 / 18.31%**, while CopySubstring is **411 / 8.84%**. These separate
profiles support the intended mechanism but do not measure an exact dynamic call
count. Remaining matching, substring construction and allocator traffic merit
investigation; CopySubstring still creates an intermediate Int32 array before
UTF-16 construction. The next character target remains dictionary lookup and
insertion. No additional optimization is included in this slice.

[Paired samples](splitcache-20261020/summary.json),
[final line comparison](splitcache-20261020/final-line-summary.json),
[final character/Unity/.NET comparison](splitcache-20261020/final-three-backend-summary.json),
[compiler/input provenance](splitcache-20261020/provenance.json),
[library provenance](splitcache-20261020/library-provenance.json),
[focused tests](splitcache-20261020/tests.log), and
[acceptance](splitcache-20261020/acceptance-results.json) are retained.
Full generated C++, executables, disassembly and raw sample stacks remain in
`artifacts/v115-profile-next-20261020` and `artifacts/splitcache-20261020`; compact
records and reproduction scripts are committed alongside this report.

The primary checkout's unrelated emitter rollback is untouched. Local main
`48ecf14` remains an ancestor; no hosted CI, push or additional agents ran.
See [ADR 0223](../decisions/0223-bounded-separator-cache.md).
