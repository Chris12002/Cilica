# Batch 003: character text

Added `Char.ToString()` and `Char.ToString(char)` to the owned core and exact
width-preserving character receiver admission. **71 distinct checks passed**:
19 new checks and 52 adjacent regressions. The retained DiffPlex CharacterText
reduction now executes through Cilica and matches normal .NET.

## Pinned candidate and ownership

Continue [DiffPlex f500e73f28e28813f942f1f16dec5a34b40e48e3](https://github.com/mmanela/diffplex/tree/f500e73f28e28813f942f1f16dec5a34b40e48e3)
(Apache-2.0), selecting netstandard2.0 under the initial .NET Standard 2.1 filter.
The [manifest](batch-003.json) carries forward 18 exact file hashes, imported build
files, dependency graph, selected roots and exclusions. The immutable online screen
from batch 001 remains applicable; no candidate download or execution was needed.
Fastenshtein remains skipped at its recorded pin for lack of a novel source gap.

Verified the existing `codex/alchemy-replay-baseline` checkout at `5af013f` after
reconnection: no batch 003 existed. Coordinated exact files and CIL/IR versions with
**Review milestone 6** before editing. Its character-search slice became `f1f0199`
during this batch; its subsequent integer-parser work is unrelated and preserved.
Build outputs use the batch 003 directory and an empty local package feed, relying
on already cached dependencies. The evidence records concurrent source context;
isolated outputs do not freeze other contributors' source files.

## Behavior and validation

The [decision](../decisions/compatibility-003-character-text.md) admits the exact
parameterless character override with an existing UInt16-tagged frame receiver.
The managed method calls the static overload, which uses existing character-array
String construction. Each invocation returns a fresh one-code-unit string, including
NUL and isolated surrogates. No interning, character cache, new native operation,
new intrinsic grant or resource-limit change is introduced. Integer CIL v81 and
object IR v52 record the admission; scalar lowering keeps existing narrow borrows.

The implementation follows the already retained MIT-licensed .NET `Char.cs`
ToString and `String.cs` CreateFromChar contract at runtime commit
`79d0c463f1b55624c874a11585f7e47731e8d675` (v10.0.11). Provenance points to those
retained originals. No candidate source/test code was copied.

Eight native theory cases provide **14,772 repeated native/.NET comparisons**.
The 2,048 sweep packets each check 32 characters, covering all 65,536 UTF-16 units
with exact payload, length and fresh-result identity assertions. Other probes cover
integer-to-char normalization edges, local/parameter/ref aliases, later mutation,
arrays retaining outputs across allocations, argument faults and evaluation order.
The original reduction adds 36 repeated comparisons. Both O0 and O2 use forced GC,
ASan/UBSan, exact/short root frames and cleanup assertions.

Seven metadata mutations reject missing or contradictory character receiver facts,
wrong signed/unsigned widths, incompatible flags/provider evidence and an older
profile. Four negative checks retain unsupported provider, boxed virtual, field-borrow
and array-borrow calls. The 52 adjacent checks cover exact integer receivers and
grants, narrow borrows, character construction, exception composition, boxing,
closed generics, dispatch and both remaining candidate gates. Initial eight native
cases failed with the missing declaration; final selected checks all pass.

## Next action and limits

| Remaining DiffPlex requirement | Confirmed independent result |
| --- | --- |
| Boolean vectors / Array.Empty<bool> | CIL2001 (profile gap) |
| OrdinalIgnoreCase / conditional comparer selection | CIL3010 get_OrdinalIgnoreCase (library/admission gap) |

Coordinate Boolean storage as the next small slice. Broad Unicode ignore-case
behavior and comparer selection require separate semantics and admission review.
These gates are unsupported features, not observed semantic mismatches. Actual
DiffPlex compilation/semantic execution, the full suite, Alchemy replay and other
platforms were not run. Source mapping and independent regressions do not establish
whole-project compatibility. No upstream MSBuild targets, generators, tests or
binaries ran, so no external execution approval was needed.

No candidate copy was created this batch. Keep the pinned manifest, independent
regressions and [evidence hashes](batch-003-evidence.json); there is nothing new to
clean up. Reuse the README's isolated build command with
`artifacts/compatibility-batch-003` and filters:

```text
FullyQualifiedName~CandidateCharacterTextTests
FullyQualifiedName~CandidateGapTests|FullyQualifiedName~PrimitiveInstanceTests|FullyQualifiedName~CharacterStringTests|FullyQualifiedName~NarrowBorrowTests|FullyQualifiedName~DefaultIntegerTextTests|FullyQualifiedName~MilestoneSixCompositionTests|FullyQualifiedName~NativeClosedGenericTests|FullyQualifiedName~NativeBoxedCilTests|FullyQualifiedName~NativeMixedDispatchTests
```

Retained logs: `before.log`, `after.log`, `regressions.log`. Validate/resume with
`candidate_snapshot.py validate --batch batch-003` and `status --batch batch-003`.
