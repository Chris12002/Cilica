# ADR AC0175: Single-selector SelectMany

Recorded 2026-10-08 for AC6.18.

The unchanged ConstructionInvitations method calls SelectMany<TSource,TResult>
with a nonindexed selector, then ToArray. Add that exact overload, validating source
before selector and preserving the canonical empty-array result. Other overloads
remain unmapped. Adapt SelectMany.cs and SelectMany.SpeedOpt.cs from the already
pinned runtime commit; provenance now verifies 68 original source hashes.

A managed iterator retains separate outer/inner cursors and current/state fields.
Traversal is deferred, repeated enumeration clones as needed, inner disposal
precedes outer disposal, and faults leave the same state for subsequent explicit
disposal. Do not add automatic exception cleanup absent from the pinned iterator.
Current resets only after successful disposal. Nongeneric Current boxes normally,
and Reset throws NotSupportedException.

Use existing owned query interfaces for independent Count, Contains, ToArray and
ToList paths. Count checks aggregate overflow and delegates inner counting;
Contains delegates inner membership. Any/First use ordinary iteration. Materialize
with traced managed segments replacing private stack scratch and rented arrays.
Eight initial slots, pool-sized growth and exact runtime array/list recognition
preserve Count/CopyTo versus enumeration callbacks, destination offsets and sizes.
The two outputs have exact final lengths/capacities. Private pool reuse, stack
storage and allocation counts are not portability contracts. Segment fields stay
encapsulated behind owned managed accessors; no field-access rule is relaxed.

Fifteen fixtures supply 6,078 repeated O0/O2 native/.NET comparisons with forced
GC, ASan/UBSan, exact frame bounds and cleanup. Coverage includes lazy/repeated
cursors, empty/null sources, null inner sequences, all outer/inner acquisition,
MoveNext, Current and Dispose fault stages, selector failures, Count/Contains/Any/
First, materialization boundaries at 8/16/32 slots, failing Count/CopyTo, value and
reference-bearing results, active-cursor projection, checked count overflow and
nongeneric cursor behavior. Exact C# delegate-cache and input-dependent empty
cursor roots are counted, not accepted as arbitrary retained objects.
All 45 selected checks pass, including existing materialization, first, concat and
the 46-control/one-gap public baseline. Nested-vector source types keep their
existing actionable diagnostic; no nested-vector language extension is claimed.

Review checked state transitions and fault retention, disposal ordering, callback
selection, checked counts, managed segment rooting, exact output copies, provenance
and unchanged compiler/runtime profiles, grants and all limits.
Evidence: artifacts/alchemy-selectmany{,-refresh,-engine}.

Frozen discovery advances to Enumerable.Single(source) over string selections in
ContinueConstructionContribution: 958,875 charged metadata units in 4,141 ms.
StateLookup remains 15 methods/11,529 units (468 ms). Exact caller IL is archived.
No full engine/fingerprint/replay execution is claimed.
