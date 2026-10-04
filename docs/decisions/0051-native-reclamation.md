# ADR 0051: Native reclamation and bounded allocation retry

Status: Accepted for the private native v12 runtime. Recorded 2026-10-04.

## Decision

Share a private mark phase between diagnostics and collection. Collection keeps
its marking guard and scratch alive until sweep finishes; no callback or managed
execution intervenes. An incomplete mark returns without freeing anything. Sweep
unlinks only unmarked allocations and calls their exact descriptor destructor,
which also deletes array buffers and immutable string buffers. Literal roots keep
the cache intact. Survivor identity, fields and synchronization words do not change.

Record the charged byte size in each allocation header, including owned buffers.
Validate the inventory count and sum of recorded charges before tracing. These
sizes, like native buffer extents, are runtime-owned metadata, not a validator for
arbitrarily corrupted native storage. Subtract each freed charge exactly once.
Header growth is itself charged through sizeof. Teardown frees remaining objects.

Object, vector and string allocations make at most one collection attempt on quota
pressure or native allocation failure, then retry if room exists. Delete any
unpublished partial allocation before collection. Size calculations check overflow
before multiplication. An incomplete collection never retries with partial marks;
invalid/busy traversal maps to the existing invalid-reference fault and exhaustion
to allocation failure. Preserve a pre-existing fault. Collection itself does not
change managed fault origins. Direct private native callers must root borrowed
references and string-buffer owners across allocating operations.

## Review and validation

Focused O0/O2 ASan/UBSan tests cover live cycles, unreachable cycles, typed arrays,
strings and literal retention, exact byte/count subtraction, invalid references,
accounting mismatch, edge-limit failure without reclamation, successful quota retry
and unrecoverable pressure with all objects live. Existing array/string differential
and fault-order tests remain enabled. The array native test harness now publishes
its borrowed array while deliberately exhausting the heap; a native local alone is
not a managed root. Split diagnostic helpers instead of raising the 2 KiB stack cap.
All six selected O0/O2 cases pass. The array loop now succeeds with a five-object
budget by reclaiming overwritten loop temporaries, with its result checked against
.NET. Stress before every allocation and retained handles are subsequent slices.
