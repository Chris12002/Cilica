# DiffPlex: verified selected roots after Int32 location support

The fourth approved trial **passes all 64 scenarios** on Cilica `3d0bb53e94b5356cbbe59fae3c2ebca93af68fc8`.
All **1,872 transcript observations** match normal .NET at O0 and O2, with three
repetitions: **11,232 native/.NET comparisons**. This is execution of Cilica's own
IL-to-C++ output/runtime, with forced collection, ASan/UBSan, frame quota checks and
root/borrow cleanup. The source assembly hash is unchanged from all earlier trials.

## Implementation and local checks

[Decision](../decisions/compatibility-011-int32-array-locations.md) and commit
`3d0bb53` add bounded Int32 vector locations, preserving receiver reads after
argument mutation, copies, array replacement, synchronous reborrows, GC owner
roots and null/bounds/argument fault order. Exact Int32 views accept existing
UInt32/Int32-enum array aliases. Other declared scalar borrows and native escapes
remain diagnostic. CIL92/IR59/native79; no descriptor layout, grant or quota change.
Review milestone 6 was contacted before edits, after validation and at release of
the compiler files. Its separate GroupBy work was excluded from this pinned export.

All **31 new checks** pass (2,352 repeated native/.NET comparisons). The adjacent
suite initially passed 153/155; both failures were stale Boolean/Int64 allocation
rejections, reproduced on pre-change 90291f9. Their corrected layout assertions and
positive native controls pass in the eight-check rerun (588 further comparisons).
This yields **186 distinct passing host checks**, including handcrafted GC,
exceptions, boxing, generics and dispatch. [Original failure log](diffplex-int32-20261008/host-regressions.log),
[baseline reproduction](diffplex-int32-20261008/host-baseline-tests.log),
[repaired checks](diffplex-int32-20261008/host-value-vector-repaired.log) and
[host validation summary](diffplex-int32-20261008/host-validation.json) remain retained.

## Unchanged candidate and actual execution

| Cilica pin | Furthest stage | Completed candidate scenarios |
| --- | --- | --- |
|268024e|CIL3003: netstandard2.0 binding|0/64|
|ae04d32|CIL2001: native-size ldlen truth branch|0/64|
|e5faa49|CIL2001: Int32 element receiver location|0/64|
|3d0bb53|Oracle and native O0/O2 transcript verification|64/64 at each level|

The earlier [contract](diffplex-execution-20261008.md),
[length](diffplex-retry-20261008.md) and
[element-address](diffplex-ldlen-20261008.md) failures remain preserved.
All trials use the same 14 selected unchanged files from
`mmanela/diffplex@f500e73f28e28813f942f1f16dec5a34b40e48e3`, Apache-2.0,
netstandard2.0, without upstream project scripts/generators/tests. All 24 reviewed
file blobs were reverified. Candidate SHA256:
`514ff0d67c5d10e0be5532d3ddd01a73fa705d3e28402e6c117fd14987b9e621`.

The owned adapter exercises three-/four-argument character and line diffs,
whitespace/case options, empty inputs, line endings, supplementary and malformed
UTF-16, ordinal special cases and both null arguments. Transcripts compare exact
piece counts, each UTF-16 code unit, every diff block's positions/counts, and null
exception parameter names/messages under invariant culture. All 56 focused guest
checks also pass; the explicit candidate test passes 1/1 in 465.172 seconds.
[Complete stage log](diffplex-int32-20261008/differential.log) and
[focused log](diffplex-int32-20261008/focused-regressions.log) retain all completion milestones.

Candidate build: zero warnings/errors. Cilica guest build: zero errors and one
preexisting CS0108 warning in CandidateHashAlgorithmOwner.State. No full repository
suite, upstream test suite, UI/rendering/three-way surfaces, other candidate pins,
other runtime platforms or actual 32-bit ABI are claimed.

## Isolation, cleanup and resumption

Same approved offline environment: UID/GID 10001, read-only root, no host mounts,
credentials or sockets, dropped capabilities/no-new-privileges; 2 CPUs, 8 GiB RAM/no
extra swap, 256 processes, 10 GiB scratch and 1 GiB output. The host deadline remains
30 minutes. The runner took 536.83 seconds; evidence export and
container removal finished at 538.3 seconds. No new tooling was installed and no
upstream MSBuild target/generator/test was run. Tool versions and packages are
retained in the evidence.

[Result and exact commands](diffplex-int32-20261008/result.json),
[container controls](diffplex-int32-20261008/container-inspect.json),
[input/package hashes](diffplex-int32-20261008/input-provenance.json),
[intervening commits](diffplex-int32-20261008/baseline-changes.txt), and
[cleanup receipt](diffplex-int32-20261008/cleanup.json) support resumption. Full owned recipes,
licence, image metadata and receipt hashes remain in `artifacts/diffplex-int32-20261008`.

Removed this trial's container, input image and candidate layers, source staging,
archive and the verified disposable baseline snapshot. Retained all tests, evidence,
previous failures and the 470,855,666-byte verified tooling image:
`sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80`.
It contains no candidate source. No live Cilica or peer work was removed.

The manifests mark these named roots **verified** and instruct future batches to
skip them unless a changed profile/pin or concrete semantic failure warrants a
rerun. The next curated candidate, CRC32.NET, has its static requirements mapped
in [batch 008](batch-008.json), but needs a separate reviewed execution plan and
approval; it was not executed under DiffPlex's authorization.
