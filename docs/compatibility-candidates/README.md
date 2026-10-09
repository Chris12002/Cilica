# Small-project compatibility batches

This workflow turns pinned public projects into independently testable Cilica
requirements. It is a source-screening and regression workflow, not an automatic
third-party build farm. The current batch is [batch-020.json](batch-020.json);
[reference handoff](reference/albireo-decode.json) and historical results distinguish implemented support, diagnostics and
tested and excluded candidate roots.

Latest execution follow-ups: [CircularBuffer selected roots verified](circularbuffer-retry-20261008.md)
on frozen d073463 with 48 scenarios, 4,531 observations and 27,186 repeated O0/O2
comparisons; all 72 native source/executable hash pairs retained.
[CRC32 selected roots verified](crc32-execution-20261008.md)
on frozen Cilica c64312e with 41 scenarios and 4,374 repeated O0/O2 comparisons;
[DiffPlex selected roots verified](diffplex-int32-20261008.md) on 3d0bb53 with
64 scenarios and 11,232 comparisons. Earlier batch reports preserve their
historical outcomes. The CRC report records its generated-C++ hash retention gap.

Separate [Release performance measurements](diffplex-performance-20261008.md)
show a large current throughput gap on one verified character-diff workload.
Correctness trial durations are not performance results.

## Accepted Unity reference/profile flow

The standard flow is now: **screen/pin a library → identify gaps → inspect and
profile representative Unity IL2CPP code → map references to owned regressions →
implement Cilica support → .NET differential correctness and appropriate performance
checks**. Follow the [executable reference workflow](unity-reference-workflow.md)
and [Unity runner handoff contract](unity-runner-contract.md).

Starting with batch020, every candidate needs a pinned `reference_manifest`.
`candidate_snapshot.py` checks online-screening evidence and that manifest before
candidate actions; verified/closed outcomes cannot pass while reference/profile
or validation evidence is pending. Explicit reviewed reasons cover mapped skips,
irrelevant profile paths and blocked or unmatched Unity references. Historical
batches retain their original claims; reopen them in a new batch.

```sh
python3 tools/CompatibilityCandidates/reference_workflow.py handoff \
  docs/compatibility-candidates/reference/albireo-decode.json > /tmp/albireo-unity-handoff.json
python3 tools/CompatibilityCandidates/reference_workflow.py reference-gate \
  docs/compatibility-candidates/reference/albireo-decode.json
python3 tools/CompatibilityCandidates/reference_workflow.py gate \
  docs/compatibility-candidates/reference/albireo-decode.json
```

The current Albireo gates intentionally fail: no upstream execution or Unity run
is approved/completed. Handoff creation is read-only and grants no permission.
Static capture, executed profiling and semantic validation remain distinct.
The existing Unity runner is DiffPlex-specific and needs a reviewed adapter for
new libraries; these hooks do not silently execute it or install tools.

## Resume and coordinate

1. Verify `git status`, branch, HEAD and all active worktrees. Read AGENTS.md and
   applicable local skills. Ask the owning thread about overlapping work before
   selecting a gap. The primary checkout and the active Alchemy compatibility
   worktree differ substantially; never infer freshness from their directory names.
2. Run `python3 tools/CompatibilityCandidates/candidate_snapshot.py status --batch batch-020`.
   The CLI default remains the historical batch 001; use explicit `--batch batch-NNN`
   for current or later batches. Existing immutable screening can be reused at the
   same pin. Start with two candidates and one independently useful feature. Reopen a mapped entry only for a new pin,
   changed profile or concrete semantic failure. Do not keep adding candidates to
   an unfinished batch.
3. Screen online before downloading: resolve a full commit; read the selected
   project, inherited/imported props and targets, package/project dependency graph,
   licence/notices, reachable sources and existing tests. Select a .NET Standard
   2.1 or compatible earlier Standard asset. A .NET Framework asset is not eligible
   merely because its version looks older. TFM admission does not establish Cilica
   compatibility.
4. Record exact roots, conditional compilation, reachable IL/storage/dispatch/library
   requirements, dependencies, semantic failures, unsupported profile features,
   evidence level, status and next action. Account for branches in reachable bodies;
   do not assume constant arguments prune them. Separate source mapping, successful
   translation, native execution and differential semantic verification.

## Pull only reviewed data

The manifest pins source paths and Git blob hashes. This bounded snapshot avoids
checking out hooks, submodules or build machinery beyond files reviewed as text.

```sh
python3 tools/CompatibilityCandidates/candidate_snapshot.py validate
python3 tools/CompatibilityCandidates/candidate_snapshot.py fetch diffplex
python3 tools/CompatibilityCandidates/candidate_snapshot.py verify diffplex
```

If direct network access fails, retrieve the same pinned files through an approved
read-only connector. Save its exact UTF-8 strings as a JSON object mapping paths to
contents, then run `stage diffplex --input-json /path/to/reviewed-text.json`.
All files must match the manifest before staging begins. Failed partial downloads
can resume; modified, unrecorded or symlinked content is refused. Review manifest
changes before running any mutating command. A source pin is evidence of identity,
not evidence that its code is safe to execute.

Neither command restores packages, invokes MSBuild, loads assemblies or executes
candidate tests/binaries. Do not run upstream targets, generators or executables
without required approval and a credential-free isolated environment. `env -i`
alone is not that isolation. Prefer independently authored reductions and the
repository-owned harness. If external execution is blocked or unnecessary, record
`not-run` rather than presenting a regression as candidate equivalence.

## Implement and verify

Complete the applicable reference/profile gate and map findings to owned tests first.
Reproduce one exact missing declaration or semantic failure with an independent
fixture. Check other active branches for existing support before implementing.
Implement the general requirement, preserving exception/evaluation order, identity,
rooting, arithmetic and documented bounds. Never change a candidate algorithm or
switch runtime to conceal a Cilica failure. Reuse supported library primitives
before adding compiler or native capabilities. Review each slice before committing.

Compare outputs, identities and faults against normal .NET, with native O0/O2,
ASan/UBSan and forced collection where applicable. Keep existing handcrafted GC,
exception, boxing, generic and dispatch tests. Rerun the candidate when its closure
is admitted and isolated execution is approved; compilation alone is not success.
If other known requirements still block it, re-map them and retain the exact next
gate instead of broadening this batch indefinitely.

When another thread builds this checkout, isolate output. Example (only existing
cached packages are admitted by this empty local feed):

```sh
mkdir -p artifacts/compatibility-batch-001/empty-feed
dotnet test tests/Cilica.Metadata.Tests/Cilica.Metadata.Tests.csproj \
  --artifacts-path artifacts/compatibility-batch-001/build \
  -p:CustomBeforeDirectoryBuildProps="$PWD/tools/CompatibilityCandidates/isolated-build.props" \
  -p:RestoreSources="$PWD/artifacts/compatibility-batch-001/empty-feed" \
  -p:NuGetAudit=false --filter 'FullyQualifiedName~Candidate'
python3 -m unittest discover -s tools/CompatibilityCandidates -p 'test_*.py' -v
```

The props hook preserves repository props and distinguishes the two example
Kernel/Prepare project names. Use the **before** hook: the SDK owns the after hook.
Record concurrent source changes and binary hashes; isolated outputs do not freeze
another contributor's source. Do not commit their working files or staged changes.

## Finish and clean up

Before cleanup, retain regression tests, pins, licences for retained third-party
material, concise results, source/diagnostic evidence and next actions. Valid terminal
statuses are `mapped-skip` (no novel requirement), `mapped-blocked` (known remaining
requirements) and `verified` (recorded semantic execution, limited to named roots).
They are not interchangeable. `mapped-skip` does not mean tested compatibility.

```sh
python3 tools/CompatibilityCandidates/candidate_snapshot.py cleanup diffplex
```

Cleanup requires the exact task ownership marker and verifies every file against
its pinned blob. It refuses unique files, modified content, symlinks and unexpected
directories, then removes only the verified snapshot. Never point a general-purpose
recursive delete at Cilica or another worktree. Do not delete tests, manifests,
results or unique evidence. Downloaded licence files stay beside source until that
source is removed. Batch 001 retains no candidate implementation or copied tests.

## Native evidence before cleanup

For future approved candidate drivers using VerifyTranslated, set
`CILICA_NATIVE_EVIDENCE_DIRECTORY` to an absolute path inside the bounded evidence
volume (for example `/evidence/native-hashes`). The helper records emitted C++
SHA-256 before compilation and successful executable SHA-256 before running it,
prior to ResolverFiles cleanup. Export these small JSON receipts with the logs and
verify every successful compile has both hashes. New custom native harnesses must
invoke NativeEvidence.Record at the same two points. Preserve failed source-only
receipts and distinguish them from executed outputs. These receipts do not contain
the generated source itself. This change does not recover the historical CRC32
emitted-source hashes; that report's limitation remains explicit.

## CircularBuffer original gate and successful retry

The first approved [frozen trial](circularbuffer-execution-plan.md) built the unchanged
candidate but stopped at constructor-chain validation before oracle/native execution.
Its [failure and cleanup](circularbuffer-execution-20261008.md) remain immutable.
After independent general constructor support, the reviewed
[same-source retry](circularbuffer-retry-plan.md) on d073463 passed all 48 scenarios,
4,531 observations and 27,186 repeated O0/O2 comparisons. Existing direct user
approval covered the unchanged scope, as explicitly confirmed by the parent.
See [results and complete hash receipts](circularbuffer-retry-20261008.md).
ToArray, segments/iterators and other type instantiations remain untested.


## Character-set trimming follow-up

[Batch014](batch-014-results.md) screens four small candidates online and implements
eight missing trimming overloads with19 passing checks/15,042 new comparisons.
No new candidate executed. Albireo.Base32 remains blocked at independently diagnosed
ToCharArray and other not-yet-reduced compositions; unsafe/locking candidates and
current SimpleBase targets were excluded with explicit reasons.


## Character-array copy and decoder blocker

[Batch015](batch-015-results.md) implements parameterless ToCharArray and verifies
its minimal readonly-reference composition. All27 checks pass with16,068 new repeated
native/.NET comparisons. Func<Char,Boolean> remains an exact diagnosed narrow-delegate
gate. No new upstream candidate executed; full decoder compatibility is not claimed.


## Char delegate follow-up

[Batch016](batch-016-results.md) supports exact Char delegate parameters with 51
passing checks and 14,808 new repeated native/.NET comparisons. The owned Any probe
now reaches the separate narrow-vector interface gate, retained as an exact
negative. No upstream candidate executed; earlier skip decisions remain unchanged.


## Char-array enumeration and Any

[Batch017](batch-017-results.md) admits exact Char generic enumeration and verifies
Any with live mutation. All 90 checks pass with 9,588 new native/.NET comparisons
and four precise GC/cache checks. The next owned decoder gate is ToUpperInvariant;
no upstream candidate was executed.


## Invariant uppercase and completed owned decoder API composition

[Batch018](batch-018-results.md) implements exact pinned ToUpperInvariant with
39 passing checks and 18,564 new native/.NET comparisons. The owned decoder API
sequence now executes; actual Albireo.Base32 Decode validation requires the
separately approved bounded execution recipe. No upstream candidate executed.


## Approved Albireo trial: .NET pass, qualified Unity reference

[The completed trial](albireo-trial-20261009.md) passes all 32 selected Decode cases
on .NET/Cilica (851 observations; 5,106 repeated O0/O2 comparisons). The exact DLL
also built and ran in Unity, producing a retained source capture and three valid
diagnostic profile markers. Unity differs on null exception formatting and U+017F
invariant casing; an explicit semantic-reference exception preserves failed Unity
equality while accepting source/diagnostic use. Batch020 verifies the selected
Decode slice against .NET. No production change or comparative speed claim was made.
Batch020 is resumable from
its input-bound receipts; approval does not extend to another library.


## Next selected surface: CircularBuffer segment copies

[Batch021 mapping](circularbuffer-segments-20261009.md) confirms an owned
Array.Copy property-receiver proof gap at frozen1408a1a. Segment-list construction
and direct iterator controls pass126repeated O0/O2 native comparisons at a7d5a46;
upstream execution and the rejected paths remain unverified.
The owned Unity reference adapter is API-compiled and its bounded host proposal
awaits scoped approval. This is a new surface, not an extension of prior execution
approvals. No production or performance-runner files changed.
