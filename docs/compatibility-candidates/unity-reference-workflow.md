# Candidate reference and profiling contract

Accepted flow: **find a library → screen and pin → identify gaps → inspect and
profile representative Unity IL2CPP code → write owned regressions → implement
Cilica support → compare against .NET and check performance**. The user adopted
this workflow on 2026-10-09. It changes engineering practice, not execution authority.

## Required steps

1. Screen online before downloading. Pin repository, commit, licence/source blobs,
   supported .NET Standard asset through 2.1, imported build machinery, dependencies,
   reachable roots and upstream tests. Reuse screening only at the same pin.
2. Record missing semantics and excluded runtime capabilities separately. Select the
   smallest useful roots and bounded input cases. Existing independently authored
   reductions can be prepared before external execution is approved.
3. Create a reference manifest and send its generated handoff to the existing Unity
   lane. Obtain any required exact execution authorization and isolation plan before
   building or running upstream inputs. An approval receipt is a record, not a new
   grant. Check its actual scope before executing. Existing same-scope authorization
   remains valid; workflow adoption is not blanket candidate execution approval.
4. Capture the applicable generated IL2CPP code and runtime locations. Prefer the same
   candidate DLLs and input cases as the .NET oracle; record all dependency assembly
   hashes, Unity version and actual build settings. Map symbols/findings to gap IDs
   and independent regression names. Reuse matching captured evidence when sufficient.
5. Profile a bounded representative affected path, using those input pins. Preserve
   raw output, method/path attribution, settings and interpretation. This can be a
   short diagnostic run; a full engine benchmark is unnecessary. Static inspection
   is not runtime profiling, and instrumented durations are not clean speed ratios.
   A metadata-only or irrelevant executed path can use a concrete reviewed
   `not-applicable` reason instead of a heavyweight run. It cannot be omitted silently.
6. Run `reference-gate` before closing the investigation/design phase. Implement the
   general feature with owned fixtures, preserving Cilica ownership, roots, GC,
   exceptions, dispatch and resource contracts. Do not copy Unity implementations,
   adopt its raw pointer assumptions, substitute its runtime, or enlarge budgets.
7. Run normal .NET versus Cilica semantic comparisons, native O0/O2, sanitizers,
   forced collection and boundary tests as applicable. Retain outputs and source/
   binary hashes. Check performance on the affected path when relevant; coordinate
   a quiet window for competitive wall-time claims. Mark gaps closed and use `gate`
   only after review. Candidate equivalence requires execution of the named candidate
   roots, not merely an owned reproducer or successful compilation.

## Executable commands

New batch020 and later entries require a `reference_manifest` repository-relative
path. `candidate_snapshot.py` checks it for **every action**, before downloading,
cleanup or reporting a candidate as verified. `verified`, `mapped-skip`, or any
closed gap must pass the evidence gate. Historical batches001–019 are preserved;
a reopened historical candidate belongs in a new batch (batch020 demonstrates this).
The gate is local; this does not enable CI or perform Git actions.

```sh
python3 tools/CompatibilityCandidates/reference_workflow.py init \
  docs/compatibility-candidates/reference/example.json \
  --batch batch-020 --candidate example
python3 tools/CompatibilityCandidates/reference_workflow.py handoff \
  docs/compatibility-candidates/reference/example.json > /tmp/example-unity-handoff.json
python3 tools/CompatibilityCandidates/reference_workflow.py fingerprint \
  docs/compatibility-candidates/reference/example.json
python3 tools/CompatibilityCandidates/reference_workflow.py reference-gate \
  docs/compatibility-candidates/reference/example.json
python3 tools/CompatibilityCandidates/reference_workflow.py gate \
  docs/compatibility-candidates/reference/example.json
```

The example ID must exist in its batch. `init` refuses to overwrite a manifest.
`status` reports missing gates without failing; `reference-gate` and `gate` exit 2
on incomplete evidence. None of these commands downloads, restores, builds, executes,
installs, sends messages, or grants approval. Handoff is the reusable runner hook:
an agent/operator reviews it, checks authorization, and adapts the existing Unity
runner for the exact candidate. This is executable manifest validation and handoff
creation, not yet a generic automated Unity runner.

Each evidence field is `{ "path": "repository/relative/file", "sha256": "64 hex digits" }`.
Paths cannot traverse out of the repository or use symlinks. SHA-256 is checked at
both gates. Keep small durable reports/receipts in the repository and large generated
archives in their declared evidence location, with archive and source hashes, symbol
locations and applicable licence notices. A hashed receipt can describe that external
archive: the checker authenticates the receipt bytes; reviewers must verify its
claims and referenced archive. A status string or a hash alone does not prove behavior.

`input_fingerprint` binds candidate commit, selected roots, input-case receipt hash,
candidate/dependency DLL hash map, and hashed owned harness/adapter inputs for every
runtime. Record it when running, then preserve it with
profiling and differential results. Changing these inputs invalidates those gates.
The Unity record declares its actual `reference_commit`, `reference_roots`,
`reference_input_cases`, `candidate_assemblies` and `owned_inputs`. All must bind to
the common fingerprint for the normal matched path. Include both common workload
sources and runtime-specific wrappers/projects in the owned-input map: pinning only
the library cannot detect a changed adapter. The executed receipts must bind these
inputs; reviewers check the build actually used them.
Record the tested Cilica commit separately in `validation.cilica_commit`.

Capture and execution evidence uses a small JSON envelope, itself pinned by the
outer `{path, sha256}` reference. The gate verifies its stage, outcome, fingerprint
and each raw artifact hash, so editing a manifest fingerprint cannot re-label stale
results as a fresh run. Example:

```json
{
  "stage": "profile",
  "input_fingerprint": "<fingerprint of actual profiled inputs>",
  "outcome": "recorded",
  "artifacts": [{"path": "docs/compatibility-candidates/evidence/profile.json", "sha256": "<SHA-256>"}]
}
```

Stages are `unity-capture`, `unity-correctness`, `dotnet`, `cilica`,
`comparative-output`, `profile`, and `performance`. Capture/correctness outcomes must
be `passed`; diagnostic/profile/performance outcomes use `recorded`. A failed Unity
correctness envelope is retained with `failed` and blocks closure. The adapter
produces these envelopes around original results without rewriting those results.
This format does not inspect the semantic contents of arbitrary logs: final review
must still confirm observations, exceptions, method coverage, timing conditions and
that the reported inputs were actually used.

## Distinct evidence states

- `unity.status`: pending, captured, captured-unmatched, blocked, or not-applicable for a mapped skip.
  Captured means a retained build/code-capture record, not player execution.
- `unity.correctness.status`: not-run, passed, or failed; executed results have their
  own receipt/fingerprint. A failed Unity semantic result blocks closure.
- `inspection.status`: pending, inspected, or not-applicable for a mapped skip.
  Every applicable gap needs concrete source/runtime symbols and semantic findings.
- `profile.status`: pending, measured, or not-applicable with reviewed scope reason.
  Measured identifies `unity-il2cpp` or the explicit alternative runtime and inputs.
  `kind=diagnostic` requires an explicit qualification and interpretation review;
  it makes no performance-equivalence claim. `kind=comparative` requires matched
  capture, passed Unity and .NET/Cilica correctness, and a retained matching-output
  verification receipt. Unmatched captures cannot support comparative timing.
- `validation.status`: pending or passed for named .NET/Cilica differential results.
  `coverage` distinguishes `owned-regression` from `selected-candidate`; only the
  latter can promote the candidate to verified. Performance results or a reviewed
  no-performance-claim rationale are separate.
- `gaps[].status`: open, closed, or out-of-scope with a reason. Closed gaps need
  owned regression names and an implementation record. A fully mapped skip records
  no novel requirement plus reviewed reference/profile non-applicability; it makes
  no execution/compatibility claim.

## Unity limitations and explicit alternatives

If Unity cannot admit the selected framework/API/platform, set `unity.status=blocked`
and retain the exact blocker. A successful capture with different assemblies,
harness or input cases uses `captured-unmatched` and records its actual identity;
it is a qualified source reference, not a failed conversion or matching-input proof.
Add `exception` with `blocker_evidence`, `alternative`, `limits` and
an explicit design-review `decision` receipt. Examples of an alternative are pinned
.NET source plus an owned runtime probe, or an older generated capture used only
as an architectural reference. Still map concrete findings to owned tests and
perform normal .NET/Cilica correctness checks. Profiling an unmatched capture binds
to its actual Unity fingerprint and must be
qualified diagnostic work. A blocked path needs a recorded alternative runtime
or a reviewed path-relevance exception. Never label that alternative a
successful matching-input Unity conversion.

No new tools, licences, paid services, host execution outside the approved isolation,
or arbitrary upstream targets/generators/tests are authorized by this exception.
If approved isolation cannot accommodate Unity, record that exact constraint and
select a reviewed alternative or wait for scoped authorization.

## Reuse the existing Unity pipeline

The retained [runner input/output contract](unity-runner-contract.md) records exact
outputs, bounds and the successful LaunchServices route; direct repeated Mac player
launches previously aborted, so launch reliability must be verified per adapter.

The performance lane owns these existing entry points in the Cilica-performance
worktree: `tools/Performance/unity/pipeline.py --stage build|capture|verify|all`,
`capture.py --project --output`, and `run_bounded.py --log --seconds -- COMMAND`.
Its README documents the tested local Mac pipeline. Coordination owner:
`01a11bd8-62f0-7412-8d23-69edc92bd0de` (reconfirm active ownership when resuming).

These tools are **currently DiffPlex-specific**: `prepare.py` downloads its exact
14-file pin, the template invokes its owned BenchProbe, capture requires that symbol,
and verification expects 523 observations. Do not run them unchanged for another
library. The handoff requests explicit template/entrypoint, capture matcher and
oracle-result adapter/fixture adaptation (`check_result.py` already compares generic values/checksum/error JSON), retains fresh per-attempt receipts and checks hashes
before capture/verification. Keep that adaptation with its approved recipe and
input/output contract; do not duplicate the Unity runner in this directory.

## Next candidate: Albireo Decode

[Batch020](batch-020.json) carries forward the unchanged pinned screening and
[existing Decode execution proposal](albireo-decode-proposal.json).
[Its reference manifest](reference/albireo-decode.json) is intentionally pending.
No source download, candidate execution or Unity conversion has occurred under this
new batch. Encode remains excluded. The old proposal excludes host candidate
execution; it does not authorize running this candidate in the host Unity Editor.

The next step is a read-only handoff for selected Decode cases and the known owned
API composition. The Unity lane specifies the smallest adapter/profile and either
a compatible approved isolation recipe or a precise blocker/alternative. Resolve
execution scope before building. Then record matching inputs, concrete generated
references, a representative profile, candidate differential results and final review;
only then can batch020 become verified.
