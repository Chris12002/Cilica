# CircularBuffer same-source retry proposal

**Historical reviewed plan: approved and completed.** The parent confirmed the
user's existing “Ok use Circular buffer” approval covers this same-source,
same-scope retry after review of the compiler change. The earlier separate-approval
wording is superseded by that clarification. All 48 scenarios/4,531 observations
passed O0/O2; see [results and cleanup](circularbuffer-retry-20261008.md).
The first trial's failure, licence and receipts remain intact.

## Exact changes and unchanged inputs

Freeze compiler/library `d07346375cc22e2fbebf1294f9a62fb81287965b` instead of the
first trial's `87fb7e5f6f3752cf684c90601c1efa4e59af132f`. The new baseline adds exact
acyclic same-owner constructor initialization and its tested lowered array-slot
prefix composition. It also includes bounded ArraySegment views and peer narrow
enumeration/boxed-interface changes. CIL101/object IR61; native79, runtime ownership,
allocation/GC quotas and execution limits unchanged. See [111 passing checks](batch-013-results.md).

**No candidate or scenario scope change.** Use
[joaoportela/CircularBuffer-CSharp](https://github.com/joaoportela/CircularBuffer-CSharp)
commit `9ebb911dfe27a62c2793d0b02c642f79bbbf524b`, Unlicense, exactly:

| Input | Git blob SHA-1 |
| --- | --- |
| `LICENSE` | `fdddb29aa445bf3d6a5d843d6dd77e10a9f99657` |
| `CircularBuffer/CircularBuffer.cs` | `7aef4a0935c8b413fda8f360c803c4a12def2d15` |

Compile the single unchanged source with the original owned SelectedRoots.csproj
and Probe.cs. The explicit driver and offline package inventory are unchanged too;
all four hashes match proposal3625157 and are listed in the
[retry manifest](circularbuffer-retry-proposal.json). They already exist at the frozen
new baseline. Copy the driver into its test project only in a disposable export
after approval. No upstream project, test, generator, workflow, example or binary
execution. Keep the Unlicense and source notices with retained third-party material.

Use netstandard2.0 and existing NETStandard.Library2.0.3 reference assets, as before.
Same 48 scenarios:8 valid seeded constructions/source-independence checks;8 mixed
push/pop sequences;8 capacity/null/oversized-seed faults;8 wrapped extreme index
get/set cases;8 overwrite/clear/reuse sequences;8 empty-operation/recovery cases.
Execute only Int32 constructors, Capacity/Size/IsFull/IsEmpty, Front/Back, indexers,
Push/Pop and Clear plus their reachable helpers. ToArray, segments/enumerators,
other type instantiations and benchmarks remain excluded.

Translate before any oracle call, then compare every state/fault/message/parameter
UTF-16 transcript observation against normal .NET at O0/O2, three repetitions,
forced GC, ASan/UBSan and existing frame/root checks. Preserve candidate semantics;
do not patch source to bypass a gate. A new compiler gate, semantic mismatch or
resource limit ends this bounded trial with evidence, not a broader rerun.

## Isolation and required evidence

Revalidate the existing image
`sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80`
(SDK10.0.401/runtime10.0.12, Clang18.1.3, Python3.12, Ubuntu24.04ARM64).
Verify every existing offline package hash. No installs or new dependencies.
Build the disposable input image offline; run with network none, UID/GID10001,
read-only rootfs, all capabilities dropped, no-new-privileges, init, no log driver,
2CPUs,8GiB RAM and memory-plus-swap,256PIDs. No host mounts, sockets or credentials.
Only unprivileged nosuid/nodev tmpfs: /work9GiB and /tmp1GiB executable;
/evidence1GiB noexec. The8GiB memory cap still applies. Empty host Docker config
and explicit local socket stay outside the container. Inspect actual limits first.

Enforce30minutes total, host kill at1790s, evidence deadline1740s and bounded child
timeouts. Coordinate heavy work with the performance lane. Use a distinct retry
run/container name; preserve all first-trial artifacts. Reuse the retained first
trial's owned orchestration structure with only task identity and frozen baseline
changed; verify those concrete arguments before start.

The exact explicit method remains:
`Cilica.Metadata.Tests.ApprovedCircularBufferExecutionTests.Selected_roots_match_every_transcript_observation`.
Use invariant culture, no parallel tests and the original pin/assembly environment
checks. Set `CILICA_NATIVE_EVIDENCE_DIRECTORY=/evidence/native-hashes` initially
empty. Require one complete source/executable hash receipt per native batch before
success. Export pins, licence, archive/overlay/package/assembly hashes, actual
commands/limits, progress, all failures and output counts before cleanup.

Delete only verified task-owned candidate/export/input-image/container files after
evidence export. Retain all regressions, unique evidence and shared tooling. No push,
merge, hosted CI, spending or runtime optimization is requested. The task input
image, container and verified staging were removed after evidence retention;
the shared tooling image remains.
