# Batch 002: string-array splitting

Implemented the general `String.Split(string[], StringSplitOptions)` overload in
Cilica's owned core. All **42 selected checks passed**: 10 new separator checks and
32 neighboring checks. This advances the independent DiffPlex LineChunker reduction
from `CIL3010 Split` to native semantic verification. Actual DiffPlex execution is
still unverified, with three known gates remaining.

## Candidate and scope

Reused [DiffPlex at f500e73f28e28813f942f1f16dec5a34b40e48e3](https://github.com/mmanela/diffplex/tree/f500e73f28e28813f942f1f16dec5a34b40e48e3),
Apache-2.0, selecting its netstandard2.0 asset under the initial .NET Standard 2.1
filter. The [manifest](batch-002.json) retains all 18 project/import/licence/source/
test file pins, dependency graph, selected roots and exclusions from the prior online
screen. No new download was needed for this immutable requirement. No upstream
MSBuild targets, packages, generators, tests or binaries were executed. Fastenshtein
remains skipped at its batch 001 pin because that source screen found no novel gap.

Worked in the user-designated `codex/alchemy-replay-baseline` worktree, starting at
`d5eafa9`, after coordination with **Review milestone 6**. Its concurrent compiler
and dispatch work is outside this slice. Separate build outputs reduce collisions;
the evidence records local source and binary hashes without claiming the entire
worktree was immutable during testing.

## Implementation decision and semantic evidence

Extend the existing managed Split implementation using ordered ordinal UTF-16
matching and two scans. Consume each match without overlap; the first matching
separator wins even if a later delimiter is longer. Ignore null/empty entries.
A null or empty separator array uses the existing Unicode whitespace classifier.
Reuse Substring, trimming and result identity behavior from the character overload.
There are no user callbacks between scans in the supported single-thread profile.
A cached Int32 separator count uses existing array-length admission. No compiler,
native operation, intrinsic grant, resource bound or profile is broadened.

The algorithm follows the already retained MIT-licensed .NET `String.Manipulation.cs`
at runtime commit `79d0c463f1b55624c874a11585f7e47731e8d675` (v10.0.11); its SHA-256 is
`1956723302dabe05550580baaf682491569dc3f825502d330d38978864d4679d`.
The exact adaptation entry in `library/provenance.json` is updated. No DiffPlex
implementation or test code was copied. Numeric TrimEntries flags follow the pinned
.NET 10 semantics already used by the character overload; the netstandard2.0
candidate itself uses its available flags.

The independent tests cover:

- Exact UTF-16 outputs, embedded NUL, unpaired surrogates, mixed newlines, ordered
  overlapping delimiters, duplicate delimiters and unmatched trailing prefixes.
- Null/empty arrays, null/empty entries, all four admitted flag combinations and
  every one of the 65,536 UTF-16 units in whitespace fallback.
- Canonical/fresh empty arrays, source/substring identity, repeated-call independence,
  later separator-array mutation, roots across allocations and final cleanup.
- Receiver/argument/options evaluation, throwing arguments, null receiver and exact
  invalid-options exception type, parameter name and message.

The new tests supply **34,254 repeated native/.NET comparisons**, plus six repeated
comparisons for the original retained `CandidateGapProbe.StringSeparators` root.
Both O0 and O2 runs use forced GC, ASan/UBSan, exact/short root-frame checks and
cleanup assertions. The 32 neighboring checks include character splitting, Trim,
managed exception composition, boxing, closed generics and mixed dispatch.
The original fixture remains; only its former negative test became a native control.
Count and single-string overloads retain exact negative tests. No final selected
check failed. The initial eight native test cases failed with the expected missing
Split declaration before implementation; logs are retained.

## Resume

Three DiffPlex gates remain, each confirmed by an independent diagnostic test:

| Requirement | Current result | Next action |
| --- | --- | --- |
| Boolean vectors / Array.Empty<bool> | CIL2001 | Coordinate a distinct storage/admission slice. |
| Char.ToString | CIL3010 ToString | Small next library candidate; verify value-call and identity semantics. |
| OrdinalIgnoreCase and conditional comparer selection | CIL3010 get_OrdinalIgnoreCase | Separate Unicode semantics and comparer admission review. |

These are explicit unsupported features, not demonstrated semantic defects. No
whole-project Cilica compatibility follows from the source map or these reductions.
Full suite, full DiffPlex compilation/execution, Alchemy replay and other native
platforms were not run for this slice. Candidate execution is unnecessary while
its closure has known gates; request approval for a concrete credential-free
isolated execution plan when that step can produce useful evidence.

No disposable candidate checkout was created in batch 002, so nothing additional
requires cleanup. Keep tests, manifests and the [evidence hashes](batch-002-evidence.json).
Batch 001 results and source pins remain unchanged.

## Reproduce

From this worktree, use the README's isolated-output command with directory
`artifacts/compatibility-batch-002`, the existing empty package feed and these filters:

```text
FullyQualifiedName~CandidateSeparatorTests
FullyQualifiedName~CandidateGapTests|FullyQualifiedName~StringSplitTests|FullyQualifiedName~CandidateTrimTests|FullyQualifiedName~MilestoneSixCompositionTests|FullyQualifiedName~NativeClosedGenericTests|FullyQualifiedName~NativeBoxedCilTests|FullyQualifiedName~NativeMixedDispatchTests
```

After the initial cached restore, use `--no-restore`. Retained logs are `before.log`,
`after.log` and `regressions.log` under that artifact directory. Validate the ledger:

```sh
python3 tools/CompatibilityCandidates/candidate_snapshot.py validate --batch batch-002
python3 tools/CompatibilityCandidates/candidate_snapshot.py status --batch batch-002
```
