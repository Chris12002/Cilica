# ADR 0205: Checked address lookup for broad descriptor sets

Status: accepted. Native v98. Recorded 2026-10-20.

Sets of at least eight accepted descriptors now use a fixed open-addressed table
with a power-of-two capacity of at least twice the set size. Small sets retain
their linear comparisons. The table compares complete pointer values after hashing;
null and unknown pointers are rejected without dereferencing the candidate.
Nominal/interface ancestry, array covariance and boxing relations are still supplied
by the same validated compiler relation. known_type, GC and public-boundary checks
continue to use exact membership; no check is removed.

A function-local static constructs the table directly in static storage. It needs
no heap allocation or table-sized temporary on the native stack. C++ static
initialization supplies synchronization, and construction cannot fail or call
managed code. Table occupancy is at most half capacity, so insertion and lookup
always encounter an empty slot even under collisions. Module promotion preserves
one shared inline function/static and matching descriptor identities.

Review checked collision termination, unknown-address handling, fixed storage,
construction/publication, module identity and all unchanged membership consumers.
Forty-six distinct focused checks pass across the final runs: exact set membership,
colliding misses, null/invalid addresses, a 4,096-entry table under the 2 KiB native
frame screen at O0/O2 with ASan/UBSan, typed modules, casts/interfaces, packed
boundaries, internal diagnostics, execution stack budgets, GC membership/tracing
and exported handles. The test driver initially exceeded the O0 frame screen;
sharing its repeated assertion loops fixed the driver without relaxing the gate.

Fresh release DiffPlex output matches all 523 observations. Three processes and
21 samples per variant measure v97 137.554 us versus v98 121.062 us: 11.99% less
execution time. Largest native frame stays 400 bytes. Exact source and sample
records are retained under artifacts/descriptor-typed-20261020 and the combined
slice report. No hosted jobs or public ABI changes.
