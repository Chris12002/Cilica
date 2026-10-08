# CircularBuffer selected-root execution proposal

**Awaiting separate execution approval. No CircularBuffer source has been staged,
compiled or run.** This requests one bounded correctness trial; preparation is not
permission to execute. The parent explicitly reserved external execution approval.

## Frozen input and recipe

Use [joaoportela/CircularBuffer-CSharp](https://github.com/joaoportela/CircularBuffer-CSharp)
at commit `9ebb911dfe27a62c2793d0b02c642f79bbbf524b`, under the Unlicense.
The complete pinned tree, project/dependency graph, licence, implementation, tests,
NuGet configuration and workflows were inspected online before download. See the
nine-file [screening manifest](batch-011.json). Only these inputs enter the trial:

| Input | Git blob SHA-1 |
| --- | --- |
| `LICENSE` | `fdddb29aa445bf3d6a5d843d6dd77e10a9f99657` |
| `CircularBuffer/CircularBuffer.cs` | `7aef4a0935c8b413fda8f360c803c4a12def2d15` |

The selected netstandard2.0 asset passes the initial .NET Standard 2.1 filter;
it does not prove Cilica compatibility. The library has no additional runtime
packages. Use existing official NETStandard.Library 2.0.3 reference assets. No
custom preprocessor constants are needed. Compile the single unchanged candidate
source and authored adapter using the owned SDK-only recipe with explicit source
items, analyzers disabled and deterministic Release output. Do not run upstream
projects, NUnit tests, scripts, workflows, generators, examples or binaries.
Retain the Unlicense and source notices with any downloaded source and results.

Freeze compiler/library commit `87fb7e5f6f3752cf684c90601c1efa4e59af132f`:
CIL98/IR60/native79, including bounded public Copy/Clear and peer callback checks.
Use an exact disposable export and only the three authored C#/project overlays in
[the validation folder](../../tools/CompatibilityCandidates/circularbuffer-validation/).
The fourth owned file pins the pre-existing offline package closure. The
[proposal manifest](circularbuffer-execution-proposal.json) records every overlay
SHA-256. Copy the explicit test driver into the export's test project only after
approval. Do not use moving shared HEAD or performance transforms. Pin verification
and any absent/changed dependency fail closed. New compiler gates end this trial
with diagnostics; they do not authorize raised limits or broader capabilities.

## Selected closure and observations

Execute only `CircularBuffer<int>` constructors, Capacity/Size/IsFull/IsEmpty,
Front/Back, indexer get/set, PushBack/PushFront/PopBack/PopFront and Clear, plus
reachable implementation helpers. Array.Copy and Array.Clear follow the ordinary
Cilica path. ToArray, ArraySegment, ToArraySegments, enumerators, reference/value
instantiations and the full project are outside this trial. The full unchanged
source remains compiler input, but only the selected closure is executable.

The authored adapter defines 48 deterministic scenarios:

- 8 valid seeded constructions, capacities 1–8, checking source independence.
- 8 mixed front/back push/pop sequences, capacities 1–4 and high-bit integers.
- 8 invalid capacity/null/oversized-seed combinations preserving validation order.
- 8 wrapped index get/set scenarios: Int32.MinValue, -2, -1, 0, 2, 3, 4 and MaxValue.
- 8 overwrite/wrap/clear/reuse sequences at capacities 1–8, including repeated Clear.
- 8 empty-operation and subsequent push/pop/empty-operation fault sequences.

Transcripts retain each operation, capacity, size, full/empty flags, every logical
payload, front/back values, exception kind and complete message/parameter UTF-16.
Preserve the candidate's actual negative-index behavior; do not repair its logic.
Translate the selected IL before invoking the oracle, then compare every transcript
observation (including length) against normal .NET at O0/O2, three repetitions,
forced GC, ASan/UBSan and existing frame/root cleanup checks. The same unchanged
candidate assembly feeds both Cilica and .NET. Compilation alone is not success.
Record unsupported gates, mismatches and resource exhaustion separately.

## Isolation, commands and cleanup

Revalidate and reuse existing tooling image
`sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80`
(.NET SDK 10.0.401/runtime 10.0.12, Clang 18.1.3, Python 3.12.3, Ubuntu 24.04 ARM64).
Use only package bytes matching `offline-packages.json`; no installs or downloads
inside execution. The already retained CRC create/supervise scripts provide the
reviewed isolation pattern, adapted only to this task's name, inputs and driver.
Their historical receipts are in `crc32-execution-20261008`; do not reuse its run ID.

Require offline input-image build and container `--network none`, UID/GID10001,
read-only rootfs, all capabilities dropped, no-new-privileges, 2 CPUs, 8 GiB RAM and
8 GiB memory-plus-swap limit, 256 PIDs, init and no Docker log driver. No host binds,
live checkout, home, credentials, keychain, agents or host sockets in the container.
Writable tmpfs only: /work 9 GiB and /tmp 1 GiB executable; /evidence 1 GiB noexec;
all nosuid/nodev and unprivileged-owned. The memory cap remains 8 GiB. Use an empty
host Docker client config and explicit local socket, neither mounted. Inspect actual
limits/mounts before start. Enforce 30-minute total wall time, 1,790-second host kill
watchdog and 1,740-second evidence deadline, plus bounded per-child timeouts.
Coordinate with the performance lane before heavy execution; no timing claim here.

After approval: verify/fetch only the two pinned text blobs, export the frozen
compiler, install hashed owned overlays, restore from /work/offline-feed, build
Release `/work/out/CircularBuffer.SelectedRoots.dll`, build the frozen test export,
and run only the explicit driver with invariant culture and no test parallelism:

```text
CILICA_APPROVED_CIRCULARBUFFER_PIN=9ebb911dfe27a62c2793d0b02c642f79bbbf524b
CILICA_CIRCULARBUFFER_ASSEMBLY=/work/out/CircularBuffer.SelectedRoots.dll
CILICA_NATIVE_EVIDENCE_DIRECTORY=/evidence/native-hashes
CILICA_CXX=clang++-18
CILICA_CSC_DIRECTORY=/usr/share/dotnet/sdk/10.0.401/Roslyn/bincore
dotnet <isolated-test-output>/Cilica.Metadata.Tests.dll -parallel none -culture invariant -noColor -explicit only -method Cilica.Metadata.Tests.ApprovedCircularBufferExecutionTests.Selected_roots_match_every_transcript_observation
```

The native-hash directory must begin empty. The driver verifies one source/binary
hash pair per O0/O2 batch before success. Export those receipts, all input/assembly
hashes, actual commands/limits, licence, logs, diagnostics and results before deleting
only this task's pinned disposable source/export/container/input image. Preserve
regression tests, failed attempts, unique evidence and the shared tooling image.
Earlier CRC generated-C++ hash omissions remain explicit; this trial must capture
hashes before temporary cleanup. No push, merge, hosted CI or paid service is requested.

This proposal has had static closure/XML/hash review only. Its adapter, driver and
candidate have not been compiled or executed. A separate approval is the remaining
execution gate; even a successful trial will establish only the named roots.
