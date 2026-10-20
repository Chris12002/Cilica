# DiffPlex: checked descriptors and typed dictionary lookup

2026-10-20. macOS ARM64, Apple M4 Pro, Apple Clang 17, .NET 10.0.11.

Two independently measured implementation slices reduce the combined release
measurement from **139.892 us/diff to 71.659 us/diff**, 48.78% less time / 1.95x
faster. The fresh non-tiered .NET median is **5.323 us**, leaving **13.46x** on this
workload. The prior 27.5x figure belongs to an earlier series; retain that distinction
when comparing progress. No new Unity IL2CPP timing was collected in this slice.

## Revisions and changes

- v97 baseline implementation: `788760a`; source/profiling documentation through
  `fa2a0f6`. The copied baseline C++ is byte-identical to the previously accepted
  freshly generated v97 artifact.
- v98 checked descriptor lookup: `90fdad8`. Sets with at least eight descriptors
  use fixed address tables with collision checks; smaller sets stay linear.
  Full pointer membership remains checked without dereferencing unknown candidates.
- v99 typed dictionary lookup: `8f951a5`. Native kernels now cover checked field/
  array reads, reference results, void helpers, proven dispatch targets and staged
  scalar frame outputs. Unknown or effectful targets and heap output borrows retain
  framed execution. The dictionary algorithm and core library are unchanged.

The previous [profile and Roslyn/IL2CPP comparison](v97-profile-20261020.md)
identified descriptor scans, frame setup and borrow carriers as the next costs.
Its exact Release IL evidence includes local-address passing at BuildPieceHashes
IL_007d and TryGetValue's stobj/initobj writes. The retained IL2CPP generated code
uses typed locals and a supplied Int32 pointer. This implementation follows those
execution patterns while preserving Cilica's checked boundary and managed faults.
Matching-source limitations for the historical Roslyn/Unity evidence remain as
documented there; neither compiler was substituted for Cilica.

## Comparable release measurements

Each row is a separate paired series. Three fresh processes per variant, at least
300 ms warmup, seven samples of 256 diffs per process, alternating/reversed order:
21 samples per median. No compilation, test or profiler job ran concurrently with
the timing series. Every measured sample has checksum 6146 per diff; all raw
samples, including outliers, are retained. C++20, `-O2 -DNDEBUG`, no C++ exceptions
or RTTI; timing builds have neither sanitizers nor diagnostic reference scans.

| Series | Before us/diff | After us/diff | Less execution time |
| --- | ---: | ---: | ---: |
| v97 → v98 descriptors | 137.554 | 121.062 | 11.99% |
| v98 → v99 typed lookup | 124.646 | 72.536 | 41.81% |
| v97 → v99 combined confirmation | 139.892 | 71.659 | 48.78% |

The fresh ordinary .NET comparator uses the same inputs, three processes, seven
samples of 16,384 diffs each, `DOTNET_TieredCompilation=0` and `DOTNET_ReadyToRun=0`.
This is a non-tiered steady-state comparison, not NativeAOT or a tiered-PGO result.
Its 21 samples range from 5.186 to 5.471 us/diff. The combined v99 samples range
from 70.478 to 72.965 us/diff; v97 ranges from 137.873 to 145.930 us/diff.

## Correctness, review and stack evidence

The descriptor slice passes 46 distinct focused checks across its final runs,
covering exact sets/collisions, null/unknown addresses, casts/interfaces, modules,
public handles, GC and tracing. Its 4,096-entry stress table passes the O0/O2
2 KiB native-frame screen with ASan/UBSan. The initial test driver exceeded the
screen; sharing repeated assertion loops fixed that driver without changing the
production threshold.

The typed slice passes 59 focused checks covering typed helpers/borrows, dictionary
lookup/mutation, custom comparers and execution frames. Five new checks include
1,176 repeated native/.NET comparisons at O0/O2 with typed execution enabled and
disabled, ASan/UBSan and forced collection, plus separate module compilation/linking.
They exercise aliasing, heap-output fallback, mutation before fault replay,
allocating/mutating dispatch targets, reference-result rooting and null/bounds faults.
The common harness also checks exact arena capacity, insufficient capacity and
complete frame/borrow cleanup.

All 523 full DiffPlex observations match in ordinary release, constrained-stack
release and constrained-stack O1 ASan/UBSan builds. The diagnostic build collects
before every allocation and enables internal reference and borrow lifetime checks.
Both stack harnesses request 512 KiB and assert the actual stack is 512–576 KiB.
The maximum measured native frame is 432 bytes in release (400 before typed lookup)
and 320 bytes in the diagnostic workload. Diagnostic/release frame gates remain
16 KiB/4 KiB. These results validate this closure and target, not arbitrary programs
or every platform's stack budget.

Self-review checked exact descriptor membership, static initialization, table
termination, staged write aliasing, heap alias fallback, safe replay, native integer
bits, frozen dispatch proofs, caller roots and module linkage. Scalar outputs commit
only on success; speculative failures cannot publish effects. Reference borrow
writes and allocations remain excluded from kernels. Kernel graph limits are
depth eight and 512 expanded statements, with runtime depth still limited to 64.
See [ADR 0205](../decisions/0205-checked-descriptor-lookup.md) and
[ADR 0206](../decisions/0206-typed-dictionary-lookup.md).

## Reproduction and retained evidence

The input DiffPlex DLL SHA-256 remains
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`.
The freshly captured owned core DLL remains
`655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
Generated benchmark C++ SHA-256 values:

| Artifact | SHA-256 |
| --- | --- |
| v97 | `20c186d15788437cca3848f4d9238e5c7f97e815ec40d016c7957bef90bba0c2` |
| v98 | `848faba090c75c5e1174ec16450c487f23e4d48469524bcd3ee26225df396477` |
| v99 | `e66a1ab85cc5cbf9da55a81f9f29486c80e33ad786d00da6e882265573cc09bd` |

Raw local evidence is in `artifacts/descriptor-typed-20261020`: benchmark.py,
emit.py, acceptance.py, generated C++, captured core files, compiler source hashes,
native frame reports, output observations, all timing samples and regression logs.
The principal summaries are descriptors-summary.json, lookup-summary.json,
combined-summary.json, net-final-summary.json and acceptance-results.json.
These ignored local artifacts are saved on disk; the implementation, ADRs and this
report are committed. No hosted jobs or pushes were performed.

The unexplained emitter overwrite recurred during development, restoring old v96
contents. Its diff was retained, then the committed descriptor implementation and
current typed edits were restored. The final compiler was rebuilt from commit
8f951a5 and regenerated the measured C++ byte-for-byte, with a clean compiler
worktree checked after emission (committed-reproduction.json). The writer's cause
is unresolved; reproducibility of this accepted snapshot does not prove that
external overwrite has stopped.
