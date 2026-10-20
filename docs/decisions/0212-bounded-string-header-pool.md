# ADR 0212: bounded per-instance string header storage

Accepted 2026-10-20. Native v105.

The v104 profile attributes substantial DiffPlex time to native allocation and
free calls for 256 short strings per diff. Retain managed allocations and their
identities, but acquire string headers from lazily allocated native slabs.

Each instance reserves at most 16 slabs of 256 slots by default. Its private byte
budget can disable or restrict reserve growth. At the cap, or when slab allocation
fails, use ordinary header allocation; this is not a new managed object limit.
Existing live object/byte quotas and collection/failure retry behavior remain.
All string headers use the pool; larger character buffers retain ordinary allocation.

A flag in header padding distinguishes pooled storage. Standard-layout slot/header
pointer interconversion is asserted. Free-list links live outside StringObject,
allowing ASan to poison the whole recycled object while retaining accessible links.
Unpoison before reuse or slab destruction. Reset all object state before publication.
Release unpublished headers when character-buffer allocation fails.

Only ordinary managed inventory objects are roots/traceable allocations. Slabs
and free lists never retain objects semantically. Public ownership checks still
reject recycled slots. Collection releases buffers and returns pooled slots;
instance teardown destroys live inventory before releasing every slab.

Review covered pointer provenance, partial allocation failure, overflow-safe reserve
growth, quota fallback, initialization, poisoning alignment and teardown ordering.
The old single-allocation test explicitly disables pooling to keep testing the
individual allocation path. New O0/O2 sanitizer probes cover live identities,
reserve exhaustion, managed quotas, stale-slot poisoning, instance isolation,
disabled/failed reserve, native failure recovery and long/empty strings.

See [measurements](../performance/string-pool-performance-20261020.md).
