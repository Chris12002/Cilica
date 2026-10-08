# CircularBuffer selected roots verified

The unchanged pinned CircularBuffer source now passes the reviewed retry on Cilica
`d07346375cc22e2fbebf1294f9a62fb81287965b` (CIL101/object IR61/native79).
**48 scenarios, 4,531 complete transcript observations, 27,186 repeated native/.NET
comparisons passed at O0/O2.** The explicit check passed, including forced collection,
ASan/UBSan, strict 2 KiB native frames, managed frame exhaustion/unwinding and exact
post-collection root counts. The runner finished in 592.826 seconds; this is a
correctness duration, not a performance measurement.

The user approved “Ok use Circular buffer” (message
`01a11b66-eb59-737b-b724-c9025b5706de`). After reviewing the changed compiler baseline,
the parent explicitly confirmed this same-source, same-scope retry was covered by
that approval. The earlier separate-approval wording in proposal `de62af0` is
superseded by that clarification. No candidate source, scenarios, dependencies,
isolation limits or risk boundaries changed.

## Exact scope

Candidate: [joaoportela/CircularBuffer-CSharp](https://github.com/joaoportela/CircularBuffer-CSharp/tree/9ebb911dfe27a62c2793d0b02c642f79bbbf524b),
commit `9ebb911dfe27a62c2793d0b02c642f79bbbf524b`, Unlicense, netstandard2.0.
Only LICENSE and CircularBuffer/CircularBuffer.cs were fetched and verified against
pinned Git blobs and SHA256. The original four owned recipe files from `3625157`
retained identical hashes. Existing NETStandard.Library2.0.3 reference assets and
all offline packages were verified; no upstream projects, scripts, tests, generators
or prebuilt upstream binaries ran. The source was compiled unchanged with the owned
adapter, then executed only inside the approved isolated environment.

Verified Int32 roots include capacity-only/seeded constructors, Capacity/Size,
IsFull/IsEmpty, Front/Back, indexers, PushFront/PushBack, PopFront/PopBack and Clear.
The six groups of eight scenarios cover source independence, mixed operations,
constructor faults, wrapped extreme indices, overwrite/clear/reuse and empty-operation
recovery. Every transcript length and state/fault/message/parameter UTF-16 observation
was compared with normal .NET. Each observation ran three repeated comparisons at
each optimization; separate exact-budget calls and one-slot-short failures are
additional checks, excluded from the 27,186 count.

ToArray, segment/iterator roots, reference/value candidate instantiations, upstream
tests and full library compatibility remain untested. The initial netstandard2.1
filter admits this earlier Standard asset; target-framework compatibility alone
was never treated as execution evidence. Skip these verified roots unless a changed
pin/profile or concrete regression warrants reopening them. A broader root set needs
its own mapping and reviewed recipe.

## Feature and evidence trail

The first trial stopped before oracle/native generation at same-owner constructor
initialization. Its [original failure](circularbuffer-execution-20261008.md) and raw
receipts remain unchanged. General support was reduced and tested independently:
[Array.Clear](batch-010-results.md), [Array.Copy](batch-011-results.md),
[ArraySegment views](batch-012-results.md) and
[exact acyclic constructor chains](batch-013-results.md). The last feature passed
111 focused/adjacent checks and 2,058 new repeated comparisons before this retry.
The retry includes that frozen compiler state; no production runtime optimization
was adopted and no compiler source was edited during execution.

Candidate DLL SHA256: `313b61c51f1b7b7a4b36e89d79133507fc3a612538a11409977d38fed762ce07`.
It is byte-identical to the first trial's assembly. The owned driver translated the
root before invoking the .NET oracle. The [complete log](circularbuffer-retry-20261008/differential.log)
and [runner result](circularbuffer-retry-20261008/result.json) retain every stage.
All 72 native batches (36 per optimization) have complete generated-C++ and executable
SHA256 pairs in [native-hashes](circularbuffer-retry-20261008/native-hashes).
These are hash receipts; generated source and binaries were disposable and are not
retained. This satisfies the new recipe's retention requirement but does not repair
the separately documented historical CRC32 generated-source hash omission.

[Input provenance](circularbuffer-retry-20261008/input-provenance.json),
[actual isolation](circularbuffer-retry-20261008/container-inspect.json),
[Unlicense](circularbuffer-retry-20261008/CircularBuffer-LICENSE),
[exported evidence hashes](circularbuffer-retry-20261008/evidence-sha256.json) and
[all retained receipt hashes](circularbuffer-retry-20261008/receipt-sha256.json)
make this run resumable and auditable. Frozen compiler archive and overlay hashes,
exact package/tooling pins, commands, orchestration and complete build logs are retained.

The container used no network, host mounts, sockets or credentials; UID/GID10001,
read-only root, dropped capabilities, no-new-privileges, 2 CPUs, 8 GiB RAM/swap,
256 PIDs and the existing bounded tmpfs/30-minute watchdog. Shared tooling was
SDK10.0.401/runtime10.0.12, Clang18.1.3, Python3.12 and Ubuntu24.04 ARM64.

## Cleanup and remaining checks

The watchdog exported evidence and removed the task container. Cleanup verified
1,953 disposable input files against the archive, owned overlays, candidate and
package hashes, then removed only this task's staging and exact input image.
The shared tooling image, all Cilica/peer source, independent regressions, licence,
original failure and new evidence remain. See [cleanup receipt](circularbuffer-retry-20261008/cleanup.json).
No trial process remains active. Full repository tests, Alchemy replay and excluded
candidate roots were not run. No push, merge, hosted CI or new installation occurred.
