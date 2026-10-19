# ADR AC0250: Bound native shallow-clone field-copy frames

Recorded 2026-10-19 for AC8.106 after `64615aa`.

## Failure and reference

The [resumption investigation](../compatibility/alchemy-native-build-resumption.md)
identifies a 2,672-byte clone helper in frozen EngineStep unit 61, exceeding the
existing 2,048-byte native-stack guard on Apple Clang 17 ARM64, O0/ASan/UBSan.
The helper copies `AlchemyGameEngine.PhysicalDamagePayload`, type `0x020002e3`.
Its managed semantics already translate; unoptimized native field-access
instrumentation creates excessive spills when all copies share one function.

The new owned WideClonePayload fixture has ten references and ten Int32 fields.
Before this emitter change its O0 differential test reproduces the same 2,672-byte
failure; its O2 test passes. Baseline sources/binaries and logs are pinned under
`artifacts/alchemy-clone-field-copies/`. An initial test build needed a cache-only
restore for stale NETStandard.Library assets; it supplied no native verdict.

Retained Unity `Alchemy.Engine__1.cpp:30261–30296` shows PhysicalDamagePayload.Copy
calling Object.MemberwiseClone, then optionally changing Sequence. Version-matched
`libil2cpp/vm/Object.cpp:135–153` allocates a fresh object, copies payload after its
header and applies Unity's barrier. Source hashes and qualified capture identity
are recorded in the investigation. This guides the shallow-copy/header separation;
Cilica keeps typed field access, its own allocation, precise roots and collector.
The captured empty-board scenario does not demonstrate executed physical damage.
This compile-frame investigation makes no runtime-profile or speed claim.

## Decision and review

Clones with more than four scalar storage fields call noinline typed copy helpers,
each assigning at most four fields. Reuse the emitter's existing CopiesPerStep
bound rather than add a separate clone-specific limit. Small/empty clones retain
their direct copies. Shared modules promote the helpers to inline linkage while
retaining noinline code generation, as with their other runtime functions.

Allocation still runs first and returns immediately on failure. Source/target
casts and reads occur afterward, in the original order. The caller's managed
source root remains live across allocation. Copy helpers contain only primitive
slot assignments: no allocation, callback, constructor or GC safepoint. The new
destination needs no additional root during those copies. Native helper calls
do not create managed frames or change managed call-depth charging.

Every flattened/inherited payload field is copied exactly once and in order;
identity, owner, synchronization, collector links and descriptor headers remain
those of the fresh allocation. There is no raw object memcpy or Unity runtime
dependency. Empty clones, null checks, intrinsic grants, sealed-owner admission,
forged-IR rejection, allocation/quota failure and cleanup retain their contracts.
Review found no remaining issues in this slice.

Only the native emission profile changes: object-frames-native-v90. Managed CIL,
IR, layout, boxed runtime, library grants and production capacities are unchanged.

## Validation

All 33 selected checks pass: MemberwiseCloneTests, NativeModuleTests,
NativeBoxedPayloadTests and NativeBoxedIrTests. This includes O0/O2 native/.NET
comparisons with ASan/UBSan, forced collection, exact/one-short execution budgets,
inherited fields, reference-bearing values, generic/empty clones and null faults.

The new wide differential root supplies 294 repeated comparisons (49 input pairs,
three repeats, O0/O2). A separate native-module test puts each managed method in
its own C++ source and supplies 60 more comparisons (five pairs, three repeats,
normal/forced collection, O0/O2). New wide quota/header checks cover byte/object
limits, managed fault recovery, distinct identity, zeroed synchronization and
collector links, exact ownership, collection and frame/borrow cleanup. The module
tests also verify that only the permanent emergency exception remains after GC.
These are 354 additional comparisons, not 354 unique inputs.

Receipts: `artifacts/alchemy-clone-field-copies/tests.log`,
`baseline-native-tests.log`, `baseline-binaries.json`, `native-evidence/` and
`baseline-native-evidence/`. No full suite or hosted work ran.

## Frozen engine boundary

The full-module refresh uses the exact previous diagnostic compiler snapshot with
only ObjectCppEmitter.cs replaced by the reviewed production change. All six
frozen input hashes and the owned-core manifest digest are verified. Previous
diagnostic aggregate allowances remain unchanged; production capacity is not
expanded. The initial local runner had an argument-index error rejected by the
manifest preflight before translation. Its receipt is retained; the corrected
attempt reuses the compiler/harness build and reruns only emission.

The refresh and native compile receipts are under
`artifacts/alchemy-clone-field-module-emission/`. EngineStep validates/emits its
unchanged 15,254 methods in 110,066ms: 67 native sources plus a shared header,
466,935,190 bytes, module manifest SHA256
`b0224f6ad726054acf77339838a220bf229e1d9818623ba922fbba3aaa42346e`.
StateLookup emits 15 methods/two sources plus a header, 1,433,235 bytes, manifest
`4e4e40584670dbc60d800e29212ae922d17422818fca0f5bb6fcc3ab2d05e61b`.
This is a bounded diagnostic refresh, not production-capacity admission or a
competitive timing measurement.

Apple Clang 17 ARM64 compiles the fresh shared header, units 61–66 and the original
entry-fault consumer at O0/ASan/UBSan with the unchanged 2 KiB guard. Unit 61's
previous clone-frame failure is cleared. Commands, object hashes, durations and
empty diagnostic logs are in `native-tail/receipt.json` and per-command logs.
Units 0–60 were not rebuilt against this new header; historical Linux objects and
the earlier macOS compile scan are distinct evidence, not linkable current output.
Complete native linking,
execution, gameplay and saved-game replay remain separate acceptance gates.
The earlier null-state consumer supplies entry-fault coverage only. The separate
String.Join(string,string[]) reference-adapter gap is not addressed by this slice.
