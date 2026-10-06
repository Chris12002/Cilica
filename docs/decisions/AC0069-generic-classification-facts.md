# ADR AC0069: Declaration facts for generic receiver and argument classification

Accepted 2026-10-07 for AC8.18.

Classifying a closed generic receiver as inline storage previously cached its open
owner's full layout as well as the required closed layout. Inline now uses the
bounded direct-base reader from AC8.17 and still compares the exact selected
ValueType identity. Executable receivers continue through full constructed layout
validation. Generic argument visibility likewise uses AC8.11's declaration reader;
it preserves the existing same-owner/public rule without decoding unused fields.
Constraint, byref-like, access, signature and actual storage checks remain separate.
Both existing fact caches retain at most 256 entries and charge uncached reads to
the same metadata work ledger. No type, field, work or native resource bound changes.

A 130-owner reduced regression previously failed with 256 snapshots despite only
135 discovered reference roots. It now executes natively at O0/O2. A second test
passes 257 distinct public value declarations with unused malformed fields through
an unused generic parameter; those fields are never executable storage. The same
malformed declaration used as a local still fails, hidden arguments still fail,
and a 257-owner executable closure still fails its type bound. The access-negative
fixture uses valid storage so malformed metadata cannot mask its visibility check.

The two generated probes pass 588 repeated O0/O2 comparisons with forced collection,
ASan/UBSan and exact cleanup. Eight focused checks and 93 distinct adjacent checks
pass, including base/access readers, closed types/constraints, boxed type tests,
integer objects and the public baseline. Evidence:
artifacts/alchemy-inline-classification/ac8r2-*, ac8r4-*, ac8r5-constraint-tests.log
and ac8r6-*. The original reproductions are ac8r1-* and ac8r3-*.
Review checked exact base identity, resolver ownership, unchanged access predicates,
bounded caching/work and full validation when a layout is actually required.
No intrinsic, IR or native profile changes.

The frozen EngineStep advances to PhysicalDamagePayload.IsLegalSelection IL_00d0
and again reaches 256 structural snapshots. Partial discovery now has 25 value,
201 reference and 8 vector roots, with 38 snapshots outside value/reference roots.
StateLookup remains at 257/256 discovered identities. These are not emitted layout
counts; full engine/replay is unverified. Public controls/gaps remain 31/16.
Evidence: artifacts/alchemy-inline-classification/final/engine-and-state.json.
Core SHA-256 remains 9f0f67ed78737d3f7843fc4e20b3cc95b41e6e17379e3b5b04a3765e1ec0cef4;
manifest: 4507f3214a2281540a63b62f36ab6633c671212b834c6bf56583864becaada48.
