# Batch 001 result — 2026-10-08

Work was aligned with the user's active `codex/alchemy-replay-baseline` worktree,
observed at `08a2d75`, after coordinating with **Review milestone 6**. The primary
`codex/milestone-7-c-abi` checkout at `/Users/christian/Cilica` is clean again.
All temporary duplicate helper changes there were removed. No new Cilica copy,
remote push, hosted job, external package installation or additional agent was used.

## Implemented support

`System.String.Trim()` now executes as ordinary managed code in the owned library.
It uses the existing Unicode whitespace predicate, scans the edges, returns the
same string when unchanged, and delegates changed results to the existing checked
Substring implementation. Empty results are canonical. Interior NULs, surrogates
and whitespace are preserved. No compiler/native operation, intrinsic grant,
resource bound or culture behavior was changed by this slice. Other Trim overloads
remain diagnostic. This is an independently authored composition, not retained
DiffPlex implementation code.

The initial primary-only IsNullOrEmpty discovery was already solved by the other
worktree (ADR AC0014). That existing implementation was reused unchanged, with
new independent regressions; it is not claimed as a new capability in this batch.

## Candidate outcomes

| Candidate/pin | Selected asset | Outcome |
| --- | --- | --- |
| Fastenshtein `b5c8438db4c722694672d6a65a11363a2a8a7cb0` (MIT) | netstandard2.0 | String entry-point requirements mapped to existing support; skipped as a feature-discovery target. An independent two-row edit-distance probe passes. Actual candidate execution is untested. |
| DiffPlex `f500e73f28e28813f942f1f16dec5a34b40e48e3` (Apache-2.0) | netstandard2.0 | Parameterless Trim requirement implemented and reduced behavior verified. Candidate still blocked by Boolean vectors, Char.ToString, string[] Split and OrdinalIgnoreCase/comparer admission. Actual candidate compilation/execution is untested. |

The [manifest](batch-001.json) retains URLs, commits, pinned source blob hashes,
licences, framework/dependency graphs, selected roots, test inspection, requirements,
excluded scope and next actions. These two statuses are source-mapping results,
not full-library compatibility claims. No candidate semantic mismatch was observed
because no upstream executable was run. No upstream MSBuild, generator or test ran.

DiffPlex's 18 reviewed text files were staged from GitHub connector responses after
a direct download timed out. Every byte was checked against its pinned Git blob,
with License.txt alongside the source. Post-change reinspection and independent
reductions confirmed the remaining gates:

- BooleanVector: CIL2001, composed vector element restriction.
- CharacterText: CIL3010, missing ToString implementation.
- StringSeparators: CIL3010, missing exact Split overload.
- IgnoreCaseComparer: CIL3010, missing get_OrdinalIgnoreCase implementation.

A complete candidate rerun cannot establish equivalence while these gates remain.
The next batch should coordinate ownership, reduce one of them, implement its
semantics and revisit the pinned candidate. It should not duplicate the review
thread's existing LINQ, ordinal dictionary or dispatch work.

## Validation and review

- Before implementation on the active worktree: two Trim cases failed with exact
  CIL3010; five migrated independent string/edit-distance cases passed.
- Final focused run: **16 passed** (CandidateStringTests, CandidateTrimTests,
  StringWhitespaceTests and MilestoneSixCompositionTests).
- Neighboring run: **46 passed**, including Substring, Split, closed generics,
  boxing, mixed class/interface dispatch and four remaining-gap probes. The four
  gap probes passed again after tightening their exact ID/member assertions.
- Snapshot tooling: **8 passed**, including unique/modified-file preservation,
  symlink/path rejection, owner checks, incomplete resume and connector hash checks.
- The focused Trim probes cover every UTF-16 code unit at O0/O2, exact mixed-string
  payload/identity, empty/all-whitespace strings, receiver side effects and faults,
  live results across allocation, forced GC, ASan/UBSan and exact/short frame budgets.
  They provide 13,350 repeated native/.NET outputs; existing quota passes add further
  checks. Candidate string/edit-distance probes add 972 repeated outputs.
- Selected builds passed with cached dependencies and isolated output. The build
  setup initially hit an unsupported CLI switch and same-named example-project
  output collision; the recorded command now uses a verified before-props hook.
- Full solution-wide test run, actual candidate execution, mobile/device tests and
  performance measurements were not run. No broader compatibility is implied.

Review checked bounded edge indices (including empty/all-whitespace strings),
unchanged identity, exact substring length, shared whitespace semantics, existing
rooting/exception paths, no new intrinsic privilege, terminal statuses and cleanup
refusal on unique data. Existing handcrafted semantic tests are retained. The
other thread's uncommitted compiler files were neither edited nor staged by this
batch. Main remains an ancestor; no branch integration was attempted.

[Evidence hashes](batch-001-evidence.json) identify this slice's sources, isolated
binaries and local logs under `artifacts/compatibility-batch-001`. The branch was
actively edited by the review thread; these hashes qualify the measured build.

Cleanup completed: the verified DiffPlex snapshot and connector staging payload
were removed after retaining this evidence. Fastenshtein had no local copy.
