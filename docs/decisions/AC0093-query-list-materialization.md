# ADR AC0093: Query list materialization

Accepted 2026-10-07 for AC6.14.

Implement Enumerable.ToList with source null validation and owned query dispatch;
other sources use the existing general enumerable list factory. Extend the owned
materialization interface, without adding a framework/private-layout intrinsic.
Where, Select, Distinct, OfType and ordered queries copy private traced array
results into exact-capacity lists. These bounded managed temporaries preserve
callbacks and values at the cost of extra private allocation. Concat instead
uses its cheap count and appends sources in order, preserving Count/CopyTo and
per-item callbacks and list growth. No compiler/native profiles, grants or
production quotas change.

Select distinguishes materialization terminals 3 (array) and 4 (list). Negative
IList counts throw OverflowException for arrays and capacity ArgumentOutOfRange
for lists; query count -1 remains unknown. Propagated fixed-destination limits
reject extra elements after reading source Current but before any selector in a
composed chain, matching the pinned Span store ordering. Other terminals retain
their existing behavior.

Fourteen authored probes pass 2,112 repeated O0/O2 native/.NET comparisons:
source null/empty/arrays/lists/enumerables, collection copy faults, cursor and
finally faults, concat source combinations/chains, filtering/projection/ordering,
default distinct and OfType, exact capacities, captured list mutation, references
and reference-bearing wide values, original cursors, corrupted storage, negative
counts, and under/overreported query counts. Thirty ToList checks plus 25 existing
Select/captured-array checks pass across a/b/c logs (55 distinct checks).

Initial a: all 10 captured-list checks passed; 16 general checks were blocked by
an unrelated static data initializer in the reduced fixture. Explicit element
writes avoid that separately tracked unsupported feature. Initial b: 16/18 pass;
ordering cases needed the existing one-per-instance default comparer root in the
exact retention assertion. Split query cases assert exactly one/zero roots;
all 29 c checks pass in 315.754 seconds. No runtime fix, relaxed root assertion,
timeout increase or removed input was used for either fixture correction.

Review covered null/validation order, source-specific capacity, count callbacks,
selector timing and fusion, partial failure/disposal, array ownership, roots across
copy/allocation and existing cursor independence. ASan/UBSan, forced collection,
exact storage recovery, 2,048-byte native frame warnings and 30-second subprocess
caps remain enabled. All 63 original-source hashes verify.

Frozen engine/state probes still reach the production type limit; they were not
executed. FindIndex is the next previously observed engine operation. Full replay,
other query overloads and unimplemented collection shapes remain unfinished.

Evidence: artifacts/alchemy-to-list/ac6-14a-*, ac6-14b-*, ac6-14c-*,
engine-and-state.json and core/library-build.json.
Core: c65a272e18ed0d4fab19d1eed55910a77315806d580e3a4317875de42a1aa2d6.
Manifest: d9fa490d75c9e66203cdd69d4641e2f25fba2a25b756cc1e490dcd984ee4c5df.
Fixture: 821ed77a1b2ecb127093bc7c3715d104de13f14c4e58aef5cf1cd8311eca59ea.
