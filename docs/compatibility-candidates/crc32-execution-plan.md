# CRC32.NET selected-source execution proposal

**Awaiting separate approval; not executed.** Prepared 2026-10-08. The user's
instruction limits existing third-party execution permission to DiffPlex. This
proposal requests one CRC32.NET correctness trial, not a benchmark or upstream
project/test run. No candidate source has been staged locally, compiled or run.

## Exact input and build boundary

Repository: [force-net/Crc32.NET](https://github.com/force-net/Crc32.NET), commit
`26c5a818a5c7a3d6a622c92d3cd08dba586c263c`. Re-read online at this immutable pin:
licence, both implementation files, SDK project, tests/project, assembly metadata,
pack script and nuspec. The recursive Git tree independently confirms these blobs:

| Retained input | Git blob SHA-1 |
| --- | --- |
| `LICENSE` | `12cc4faf0db1fc00a6e0f5b2a8a6cad5cdde1a95` |
| `Crc32.NET/Crc32Algorithm.cs` | `ccacd0314260decfd7ef7f092b14f178c0dec2ff` |
| `Crc32.NET/SafeProxy.cs` | `832c05aba1b2cdeab3e241e24bde87d14266b501` |
| `Crc32.NET/Crc32.NET.Core.csproj` (inspection only) | `cec1661d45250144ddff09a83ce949e788075f22` |

Licence is MIT, Copyright (c) 2016 force. Keep LICENSE and existing source notices
with the two unchanged compiled files, and retain the licence with results.
Batch-008 contains the complete ten-file screening manifest.

The project offers netstandard1.3, netstandard2.0 and net20. Select netstandard2.0,
which passes the .NET Standard 2.1 filter; this is not a Cilica compatibility claim.
The selected library has no extra runtime NuGet dependency. The owned recipe uses
existing official NETStandard.Library **2.0.3** reference assets, rather than the
upstream project's implicit 2.0.0 package preference; retain that distinction.
Define `NETCORE;NETCORE20` exactly for this target. Use only the existing SDK's
imports, no default source glob, no analyzers, signing, package generation or
upstream imports. Do not invoke upstream csproj, pack.cmd, NUnit tests, alternative
CRC packages, generators or binaries. Upstream tests were inspected as text only.

Reviewable owned files are in
[tools/CompatibilityCandidates/crc32-validation](../../tools/CompatibilityCandidates/crc32-validation/):
`SelectedRoots.csproj`, `Probe.cs`, and `ApprovedCrc32ExecutionTests.cs`.
The driver stays outside normal test discovery until copied into the disposable
compiler export. Its explicit attribute and pin environment check are additional
accident guards, not a sandbox or approval. All three files are currently uncompiled.

Freeze compiler/library baseline `c64312e6993a5bce4078cb23961e737cd1df8e24` (includes
Range and exact earlier-contract omissions). Export that commit and overlay only
these three owned files at their recorded hashes; copy the driver to its test
project in the export. Do not silently take a moving shared HEAD or profiling
experiment. Record the archive, overlay, reference, assembly and generated C++
hashes. Verify the candidate blobs before any build. A newly discovered gate ends
this trial with evidence; it does not authorize a broader closure or raised limits.

## Operations and success criteria

Call only `Crc32Algorithm.Compute(byte[])` and
`Crc32Algorithm.Append(uint,byte[],int,int)`, including their reachable private
helpers, static SafeProxy construction and 4,096-entry UInt32 table initialization.
The unchanged source retains HashAlgorithm subclass declarations, but instance
construction, provider hashing, ComputeAndWriteToEnd, CRC-at-end validation,
CRC32C and other providers are outside the selected execution roots.

The authored adapter defines **41 deterministic scenarios**:

- Eight whole buffers of lengths 0, 1, 15, 16, 17, 31, 32 and 257.
- Eight offset-three slices of those lengths with high-bit initial CRCs.
- Eight empty appends at valid offsets, including the end, preserving the seed.
- Eight null, negative, out-of-range and unchecked offset-plus-length overflow
  cases. Preserve the upstream validation order, including IndexOutOfRangeException
  after an overflowing sum; do not repair the candidate to conceal its behavior.
- Eight two-part appends at cuts 0, 1, 15, 16, 17, 31, 32 and 33, checked against
  whole-buffer append, plus the ASCII `123456789` check value `0xCBF43926`.

Each transcript reports all 32 checksum bits, an exact per-byte input-unchanged
check, fault kind, and full exception message/parameter UTF-16 units and lengths.
The driver validates known checksum, expected fault categories and unchanged-input
invariants against normal .NET, then translates the same candidate IL through
Cilica and compares every transcript observation at native O0/O2 with the existing
three repetitions, forced collection, ASan/UBSan and frame/root checks. Permanent
roots use the existing persistent-root mode for the candidate's static table.
Do not use NativeAOT, another runtime, copied CRC logic, or a checksum-only pass as
a substitute. Compiler/library failures, candidate semantic mismatches and exhausted
resources are separate results. No compatibility or passing result is claimed yet.

## Existing tooling and hard isolation limits

Reuse verified image
`sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80`:
.NET SDK 10.0.401/runtime 10.0.12, Clang 18.1.3, Python 3.12.3, Ubuntu 24.04 ARM64.
Use the previously hashed offline package closure recorded in
`artifacts/diffplex-approved-20261008/input-provenance.json`. Missing or changed
packages fail closed; this proposal does not approve installs or new tooling.

The existing DiffPlex create/supervise recipes are the template, with a distinct
CRC task name and only the reviewed inputs changed. Require `--network none`,
non-root UID/GID 10001, read-only rootfs, all capabilities dropped,
`no-new-privileges`, **2 CPUs**, **8 GiB RAM** and 8 GiB memory-plus-swap limit,
256 PIDs, init and no Docker log driver. No host binds, live checkout, home,
credentials, keychains, SSH agents or host sockets inside the container.
Writable tmpfs only: `/work` 9 GiB, `/tmp` 1 GiB, `/evidence` 1 GiB; evidence is
noexec, all nosuid/nodev and owned by the unprivileged user. Memory caps remain
8 GiB even though tmpfs size ceilings sum to 11 GiB.

Build a disposable input layer from the exact existing image with networking
also disabled; no package installation. Fetching verified pinned text outside the
container is read-only acquisition and cannot run upstream hooks. The container
itself remains offline throughout. Use an empty host Docker client config and the
explicit local socket; do not mount either. Inspect actual container limits and
mounts before starting. Enforce **30 minutes total wall time**, existing 1,790-second
host kill watchdog, evidence collection deadline at 1,740 seconds and bounded
per-child timeouts. Do not start while the performance lane owns clean timing.

After approval, the fixed guest sequence is: stage/verify `crc32-net` using the
batch-008 manifest; copy the owned recipe/adapter into `/work/recipe`; restore only
from `/work/offline-feed`; build Release `Crc32.SelectedRoots.dll`; build the frozen
Cilica test export with the reviewed explicit driver; run that driver only with
`CILICA_APPROVED_CRC32_PIN=26c5a818a5c7a3d6a622c92d3cd08dba586c263c` and
`CILICA_CRC32_ASSEMBLY=/work/out/Crc32.SelectedRoots.dll`, invariant culture and
`-explicit only -method Cilica.Metadata.Tests.ApprovedCrc32ExecutionTests.Selected_roots_match_every_transcript_observation`.
Capture progress after staging, builds, translation, oracle and each native batch.

Retain plan, pins, licence, hashes, logs, counts, diagnostics and concise results
before cleanup. Remove only this task's disposable candidate/export/input layers,
container and generated artifacts after evidence export. Keep the verified tooling
image, regression tests and unique evidence. Approval would cover this one bounded
trial and source closure; additional candidates or runtime capabilities need a
separate decision. This plan does not request a push, merge, hosted CI or spending.
