# Batch 004: Boolean vectors

Implemented bounded Boolean arrays across the existing array pipeline. **83 distinct
selected checks pass** across final runs: 21 new checks and 62 adjacent checks.
The original independent DiffPlex Boolean-vector reduction now executes through
Cilica and matches normal .NET. The recorded ignore-case comparer gate remains.

## Pin, screening and coordination

Reused [DiffPlex f500e73f28e28813f942f1f16dec5a34b40e48e3](https://github.com/mmanela/diffplex/tree/f500e73f28e28813f942f1f16dec5a34b40e48e3)
(Apache-2.0), netstandard2.0 under the initial .NET Standard 2.1 filter. The
[manifest](batch-004.json) retains the 18 exact source/licence/project/test/import
pins and dependency graph from the original online screen. No candidate download,
MSBuild target, generator, test or binary execution was needed. No third-party
implementation or tests were copied into this slice.

Verified the existing worktree at `0a40556` and coordinated exact ownership with
**Review milestone 6** before editing. Its concurrent throwing-parser slice became
`af7d540` separately. The Boolean implementation preserves those edits and uses
isolated build outputs with cached dependencies and an empty local restore feed.
Evidence records source/binary identities without claiming immutable concurrent
source during every invocation.

## Behavior and evidence

A distinct Boolean vector identity uses existing UInt32 runtime slots. Stores keep
the low eight bits; unsigned and exact generic loads preserve that byte, including
noncanonical Boolean values introduced by authored IL. This is internal storage,
not a packed host Boolean buffer. Exact array descriptors, GetType, casts and
IList/IEnumerable metadata keep Boolean separate from Byte, SByte, Char and Int32.
Existing narrow-vector interface helpers supply Count, flags and SyncRoot only.

Allocation/default initialization, loops, generic element copies, heap aliases,
roots across allocation, per-type/per-runtime Array.Empty<bool>, static constant
initialization, evaluation/fault order and exact cast text are tested. Negative
lengths retain OverflowException. Model checks reject attached nominal identities
and the older profile. Both IR stages reject wrong array identities and missing
fault edges. Element borrows, interface reads/copies/clear, signed loads and wider
load/store opcodes retain explicit diagnostics.

The ten C# probes add **4,500 repeated native/.NET comparisons**. Two independently
authored IL probes compare stelem.i1/ldelem.u1 and generic stelem/ldelem against
normal .NET DynamicMethod execution, adding **9,264 comparisons** over all byte
values, negative/wrapping inputs and integer boundaries. Total new comparisons:
**13,764**, plus 294 for the retained original Boolean reduction. Native O0/O2 runs
use forced GC, ASan/UBSan, exact/short frames and cleanup assertions.

The initial eight native cases failed with CIL2001 before implementation. All C#
semantics and raw O2 comparisons passed on the first implementation. Raw O0 initially
exceeded the harness main's 2,048-byte stack cap; splitting into 128-input chunks
resolved it without changing production or test safety limits. Two new malformed
width tests initially expected CIL2001; the compiler correctly reported CIL1013
for invalid width/kind, and the tests now assert that exact diagnostic. No semantic
mismatch or unresolved final failure remains. The 83-check total deduplicates repeated
tests across `after.log`, `regressions.log` and `final-focused.log`.

Adjacent checks cover Byte/Char arrays, narrow interface metadata, constant-data
validation, empty-array ownership, GC, exceptions, boxing, generics and dispatch.
Integer CIL v83, object IR v54, layout v27 and native v76 record Boolean identity and
store narrowing. Resource limits and existing runtime allocation algorithms are
unchanged. See the [decision](../decisions/compatibility-004-boolean-vectors.md).

## Resume and cleanup

The remaining recorded DiffPlex gap is `StringComparer.OrdinalIgnoreCase` and
conditional comparer selection (`CIL3010 get_OrdinalIgnoreCase`). Treat Unicode
ignore-case semantics and comparer admission as a separate coordinated slice.
This is a known unsupported feature, not an observed candidate semantic defect.
Full DiffPlex compilation/execution, the complete Cilica test suite, Alchemy replay
and other host platforms were not run. No whole-project compatibility is claimed.

After the recorded gates close, request any required approval with the exact pin,
command and credential-free isolation plan before actual candidate execution.
Record new integration failures if present; only semantic execution can verify
candidate behavior. Retain reductions and mapping evidence before cleanup, then
screen the next small compatible candidate online. Fastenshtein remains skipped
at its previously mapped pin. No new candidate copy exists to remove in batch 004.

Retained evidence: [hashes](batch-004-evidence.json) and logs under
`artifacts/compatibility-batch-004`. Use the README isolated build command with that
directory. The new class is `CandidateBooleanVectorTests`. Neighboring filters:

```text
CandidateGapTests|NarrowVectorTests|NarrowArrayMetadataTests|StaticArrayDataTests|StaticArrayDataSafetyTests|EmptyArrayLibraryTests|MilestoneSixCompositionTests|NativeClosedGenericTests|NativeBoxedCilTests|NativeMixedDispatchTests
```

Prefix each alternative with `FullyQualifiedName~` when using dotnet test. Validate
and resume via `candidate_snapshot.py validate --batch batch-004` and
`status --batch batch-004`.
