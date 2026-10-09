# CircularBuffer follow-up: segment/copy composition — 2026-10-09

The next selected surface is **CircularBuffer<Int32>.ToArraySegments, ToArray and
direct GetEnumerator consumption** at unchanged upstream
`9ebb911dfe27a62c2793d0b02c642f79bbbf524b`. Existing constructor/deque/indexer/Clear
results stay verified; these new roots remain **unverified and unapproved for
upstream execution**. Albireo scope is unchanged.

## Online screening and scope

The pinned [project](https://github.com/joaoportela/CircularBuffer-CSharp/blob/9ebb911dfe27a62c2793d0b02c642f79bbbf524b/CircularBuffer/CircularBuffer.csproj)
selects netstandard2.0 within the initial2.1 filter. The library has one source
file and no runtime package dependency. The pinned Unlicense and
[NUnit tests](https://github.com/joaoportela/CircularBuffer-CSharp/blob/9ebb911dfe27a62c2793d0b02c642f79bbbf524b/CircularBuffer.Tests/CircularBufferTests.cs)
were reread online; their packages, build scripts and tests will not execute.
The earlier immutable tree/dependency review is retained; a fresh GitHub tree
request failed and is not claimed successful. No new candidate copy was downloaded.

[Source lines270–314](https://github.com/joaoportela/CircularBuffer-CSharp/blob/9ebb911dfe27a62c2793d0b02c642f79bbbf524b/CircularBuffer/CircularBuffer.cs#L270)
compose two ArraySegment values in an array-backed IList, a foreach calling the
five-argument Array.Copy overload with segment properties, and an IEnumerator
state machine with nested enumeration/disposal. These are meaningful previously
untested compositions; framework compatibility alone does not establish support.

## Confirmed owned reduction

An independently authored six-method probe was built in a disposable export of
Cilica **1408a1a102c0aaca0c65f236e0fdd9acbafca4b3**. No upstream code was present.
All six one-pair .NET oracle assertions pass (.NET10.0.11); the test separately
records admission rather than asserting that the candidate works:

| Owned composition | Translation result |
| --- | --- |
| Segment-list creation/indexing and backing alias | Admitted |
| Foreach segments with direct property Array.Copy arguments | CIL2001 at IL0049 |
| Direct property Array.Copy without enumeration | Same CIL2001 at IL003b |
| Equivalent typed-local property values before Array.Copy | Admitted |
| Yield stream consumed through IEnumerable<Int32> | Reaches unsupported segment enumerable member |
| Yield cursor consumed directly as IEnumerator<Int32> | Admitted |

The first gap is **same-block array-producer proof for struct getter receivers**.
`ValueVectorCallDiscovery.cs` recognizes calls but lacks ldloca/ldarga address
producers needed by these getters. Its error mentions joined stacks even though
the minimal direct-property input has no join. Any eventual fix should extend
only the precise address producer proof, retaining the existing normal stack/type
checks, evaluation order, metadata limits and rejection of general Array aliases,
joined producers and cross-element conversion. Do not rewrite candidate source to
spill locals and hide this gap.

The IEnumerable result is a separate conservative interface-closure issue:
discovery includes `ArraySegment<Int32>.IEnumerable<Int32>.GetEnumerator`, whose
owned body is explicitly unsupported. The exact candidate method returns
IEnumerator directly, and that reduction admits. This follow-up excludes the
broader IEnumerable consumer until its own design; it does **not** claim missing
segment enumeration is needed for the selected direct path.

The admission results alone do not establish a native semantic defect. A subsequent
owned control run at **a7d5a468b7b119a16f98fbe276fd19a1cb835d7b** passes all three
tests:126 repeated comparisons,42 exact-budget successes and42 one-short checks
across O0/O2, with ASan/UBSan, forced GC and lifetime/survivor assertions. The three
rejections remain explicitly checked. This covers only the admitted owned
controls, not upstream or full-library behavior. [Exact mapping output](circularbuffer-segments-20261009/mapping-controls.log)
and [interpretation](circularbuffer-segments-20261009/owned-mapping.json) are retained.

## Minimal reference preparation and next approval

The [owned adapter](../../tools/CompatibilityCandidates/circularbuffer-segments/README.md)
contains no CircularBuffer dependency. Its Development-player and Editor sources
compile against installed Unity6000.3.2f1 APIs with zero warnings/errors; Unity was
not launched. The proposed reference uses six owned methods and seven fixed
argument pairs, retaining all48 observations as explicit triples. The diagnostic
profile selects direct-property copy, typed-local copy and direct iterator probes,
with one expected-value iteration plus8 explicit warm iterations (nine selected
iterations total, after the full transcript), and three frames of at most128 iterations. Each
iteration calls all three complete allocating probes. No competitive or pure-Copy
measurement is proposed.

The [exact proposal](circularbuffer-segments-proposal.json) pins every owned source,
wrapper and project input. **Approval requested for the owned reference stage only:**
import/build those sources in the existing normal-account Mac Unity6000.3.2f1,
verify all48 observations, capture generated code, and collect/inspect the bounded
three-marker diagnostic profile. No upstream source is included. Existing normal
Mac limitations remain explicit: no credential/network or OS CPU/RAM isolation;
1790seconds per command,64MiB console/raw soft bounds and1GiB export bound require
an outer watchdog. Only the installed toolchain and built-in JSON module are
allowed. Actual universal architecture/flags and process architecture must be
recorded rather than inferred from requested settings.

The previous approvals covered different roots/workloads. This proposal does not
reuse them, authorize new upstream execution or enable a generic runner. The
coordinated Unity lane owns runner/capture changes; this lane owns the candidate
workflow and authored reduction. .NET remains the semantic target. Any Unity
difference needs its own explicit review; the Albireo exception cannot be reused.

After reference review, the production design should cover getter address forms,
side-effect/fault ordering, null/invalid ranges, reference/value rooting across
allocation, and negative malformed/join/general-Array proof cases. Then freeze the
corrected compiler and an exact full transcript adapter for a separately approved
16-case selected-candidate trial. The later upstream recipe is intentionally
**not execution-ready**: DLL/final harness pins and exact compiler depend on the
unimplemented fix. No blanket future approval is requested.

## Preparation checks and retention

One owned mapping test passes, with three admitted and three rejected translations;
three subsequent native/control tests pass, and two installed-API adapter compile
configurations pass. The Unity lane reviewed the adapter without a blocking issue.
[Native controls](circularbuffer-segments-20261009/owned-native-controls.json) and
[independent adapter review](circularbuffer-segments-20261009/adapter-independent-review.json)
record exact scope and future runtime marker/identity checks. The only player-source
change after API compilation corrects an inherited comment. Initial preparation failures
(C#9 namespace syntax and cancellation-token analyzer) were corrected and retained.
A sandboxed multiprocess restore stalled before assets; only its inspected task PID
was stopped. Single-process offline restore used existing cached dependencies and
an empty feed. No packages were installed or upstream binaries run.

The frozen owned export and logs are task artifacts; source, diagnostics and recipe
inputs are retained here before cleanup. No shared compiler/core/runtime files or
performance worktree were edited. Rejected-path native conformance, upstream trial, Unity build/player/profile,
production fix and full-suite validation are all pending. A wrong-directory
zero-test attempt and a corrected ordinal-comparison analyzer failure are also
retained and excluded from passing counts.
