# ADR AC0226: Remove a contiguous list range

Recorded 2026-10-09 for AC8.82 after 8f3767d (AC8.81).

Frozen ShieldSourceSelection.Select 0x06000639 IL_00af calls List<T>.RemoveRange.
Implement the pinned managed algorithm in the owned List source. Validate negative
index, negative count, then size-index<count, preserving exact messages and parameter
names. For positive count, reduce size, copy retained elements forward over the gap,
increment version, then clear the vacated tail when T contains references. Forward
copy is overlap-safe because the destination precedes the source. Zero count leaves
size, capacity, storage and enumerator version unchanged after validation.

Reuse existing managed typed array stores and ArraySections<T>.ClearSection; there
are no callbacks or allocations on a successful removal. No compiler/runtime,
capability, profile or production-budget changes (CIL108/IR64/interface20/native80).

## Reference and validation

The captured converted Alchemy code contains List_1_RemoveRange in Generics__19.cpp
around lines 4358-4458. It confirms index/count/range check order, positive-count
guard, size publication, overlapping copy, version update and tail clear. Capture:
`/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/evidence/alchemy-reference-20261008T2312Z/generated/Library/Bee/artifacts/MacStandalonePlayerBuildProgram/il2cppOutput/cpp/Generics__19.cpp`.
SHA256 `5b387ba99a1554b9bbda8095d8d6d24efde8ad29b0bb4147e5dd8a79abefc4b7`.
The pinned dotnet-runtime List.cs lines 1020-1049 governs the owned managed
implementation, including its reference-containing clear condition. No Unity
runtime code is adopted; the captured DLLs differ from the frozen audit inputs.

The isolated snapshot artifacts/alchemy-list-remove-range passes all 19 new and
adjacent ListMutation checks in 53.639s. Eight roots x 121 index/count pairs x
three repeats x O0/O2 supply 5,808 new native/.NET comparisons. Cover empty lists,
all valid ranges of a six-element list, prefix/middle/suffix/all removal, exact
negative/oversized range faults, Int32 extremes, null receivers, zero-count cursor
preservation, successful cursor invalidation, subsequent Add, strings, object
identity and reference-bearing copied structs.

Four additional O0/O2 native checks keep the list rooted, remove one then all
referenced objects and assert exact collection drops for reference and inline-value
elements. Repeating empty removal changes nothing; dropping the list leaves only
the emergency exception. Sanitizers, forced collection, exact root/borrow cleanup,
exact/one-short frames and 2 KiB frame warnings remain enabled. Native hashes are
in native-evidence. Build has only the existing unrelated fixture warning.

Review checked subtraction-based validation without index+count overflow,
overlap-safe forward stores, version timing, reference-tail release and unchanged
capacity. No remaining findings. No full suite or hosted tests ran.

## Diagnostic capacity and frozen scan

Before this change, AC8.81's diagnostic graph exceeded the production relation-work
limit of 1,048,576. A counter-only pass with diagnostic allowance 4,194,304 converged
in two passes at 1,115,387 steps for 3,590 identities, 1,467 interfaces and 235 arrays.
Breakdown: variance pairs 320,396; arguments 320,744; array covariance 72,380; array
contracts 349,210; transitivity 52,657. This is a measured capacity boundary in the
already enlarged diagnostic catalog. Keep the production limit unchanged and use
that larger relation allowance only in subsequent diagnostic copies. Evidence:
artifacts/alchemy-relation-work-audit, including scope.json, command.json, probe.log
and work.json. No production algorithm optimization is claimed.

With RemoveRange, the frozen scan advances to Math.Sign in BarrierOperation.Span
0x06000d47 IL_0045, requested by Apply 0x06000d46 IL_0045. Engine discovery charges
3,714,292 metadata units in 27,146ms. StateLookup validates 15 methods/11,742 units
in 447ms. Both emission fields remain null. No engine C++ was compiled or run.
All six immutable DLL hashes remain unchanged.

Core SHA256: `42f39757a401d46d2ec818d35ee9cb93b0b0384e47e424f75aa2cc7bb55cb7ef`.
Manifest SHA256: `f2f96d1cb0fb904cab52c7aa4d45ad0efbd0509379bebe1d4bc17278f77e0e72`.
Receipts: artifacts/alchemy-list-remove-range-refresh and -engine. Full engine
closure and saved-game replay remain incomplete.
