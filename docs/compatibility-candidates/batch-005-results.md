# Compatibility batch 005 results

Implemented managed Dictionary comparer construction in the existing
`codex/alchemy-replay-baseline` worktree. Reconciled the prior failed final notice
with **Review milestone 6** before editing and reserved the affected files. Its
instruction/local-budget audits and invariant-lowercase work were committed
separately and preserved (6e04948, b39b01f, ad6b811). Later uncommitted Skip
iterator edits began after the final gate and are preserved/excluded; the 197-check
gate does not claim to validate those later sources.

Both comparer constructors now accept aliases and runtime-selected admitted
comparers, retain the comparer through GC, and forward hash/equality using ordinary
managed dispatch. Null retains default key operations. Explicit owned value-default
comparers are normalized to preserve mutable-key receiver behavior; custom
comparers and custom EqualityComparer subclasses receive copies. Removed only the
two obsolete Dictionary CreateOrdinal grants. Compiler/native profiles and limits
are unchanged. See [design](../decisions/compatibility-005-dictionary-comparers.md).

## Verified

- **197 final checks passed**, zero failed/skipped: 22 new comparer cases and 175
  adjacent/default-dictionary/comparer/GC/exception/boxing/generic/dispatch checks.
- Ten new comparer roots: **3,600 repeated O0/O2 native/.NET comparisons** with
  ASan/UBSan, collection before allocation, exact root counts and cleanup checks.
- Previously rejected constructor cases now pass **576** repeated comparisons;
  typed ordinal hash property/null-fault controls supply another **294**.
- Exact missing-member diagnostics for OrdinalIgnoreCase and CurrentCulture remain.
  Existing key/grant restrictions and HashSet ordinal construction are retained.
- Candidate manifest validation and status pass; no candidate snapshot is present.

Development evidence is retained, including the preimplementation constructor
rejections and a reduced semantic failure: DefaultValueKey(-1,-1) initially
returned -1068 instead of .NET's -967. Explicit-default normalization fixes it.
An initial constructor-chaining form and a cross-owner private-field access form
were rejected by existing compiler checks; ordinary constructor bodies/accessors
resolved them without broadening compiler access. The first broad gate found one
stale typed-ordinal-hash rejection, replaced by semantic hash controls. Final
results above are from the completed gate after every executable edit.

## Candidate status and next action

[DiffPlex](https://github.com/mmanela/diffplex/tree/f500e73f28e28813f942f1f16dec5a34b40e48e3)
remains **mapped-blocked**, not verified. The selected netstandard2.0 library is
Apache-2.0; the original 18 pinned source/project/licence/test files and dependency
screen remain in [manifest](batch-005.json). A fresh read of pinned Differ.cs
matched blob `281ee96c405676ba6f61958939d5121bdcc8584c`.

Its ignoreCase flag selects **OrdinalIgnoreCase or Ordinal**, and dictionary
hash/equality drives piece interning. There is no culture-sensitive comparer or
Compare requirement in that path. Conditional construction is now admitted, but
get_OrdinalIgnoreCase still fails exactly with CIL3010. Unicode ordinal-ignore-case
equality and compatible hashing are the next coherent slice; invariant lowercase
and ASCII folding are not substitutes. Supplementary/malformed UTF-16 semantics
must be handled explicitly. No full globalization claim is made.

No upstream MSBuild, restore, generator, test or binary was executed. Actual
candidate compilation and differential integration remain untested and may reveal
further defects. Before that execution, prepare a concrete approval request with
the pin, audited build surface, exact commands and credential-free isolation. It
is not needed for this still-blocked batch. No new disposable candidate copy was
created, and no files were deleted. Fastenshtein remains skipped at its earlier
mapped pin. Close the remaining gate and verify the selected roots before moving
to another candidate. Same-dictionary callback reentrancy and the full repository
suite have not been newly verified by this slice.

## Reproduce and resume

Use the isolated build command in [README](README.md), substituting
`artifacts/compatibility-batch-005`. The final test filter is the following list,
with each alternative prefixed by `FullyQualifiedName~`:

```text
Dictionary|EqualityComparerTests|StringComparerTests|OrdinalSetTests|OrdinalHashTests|NativeBoxedCilTests|NativeClosedGenericTests|NativeMixedDispatchTests|MilestoneSixCompositionTests|CandidateGapTests
```

Logs remain in `artifacts/compatibility-batch-005`; [evidence](batch-005-evidence.json)
records final test identities, source and artifact SHA-256 hashes. Resume with
`python3 tools/CompatibilityCandidates/candidate_snapshot.py status --batch batch-005`.
