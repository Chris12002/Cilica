# ADR AC0056: Bounded type-classification failure evidence

Accepted 2026-10-07 for AC8.10.

Predicate Single advances the frozen engine to a generic metadata-classification
limit. Report which counters reached their bound before changing admission or
capacity. Both ordinary and closed snapshot failure paths now include cached and
nominal/closed counts, field reads, remaining shared metadata work, explicit value/
reference/vector roots, validated Int32 enums, snapshots outside explicit roots,
per-image counts and the attempted identity. The existing caller/IL path remains.

Reports use already collected metadata only. Per-image and outside-root samples
are capped at eight; sampled names are capped at 128 characters. They state that
these partial snapshots/roots are not emitted runtime layouts. The compiler does
not read new input or spend metadata work while formatting the failure. Limits,
profiles and library contracts are unchanged.

The reduced 257-enum-locals fixture fails with 256 cached snapshots: fifteen
exception-runtime references and 241 validated scalar enums, with 64,316 of
65,536 metadata work units left. A one-enum control succeeds. Initial expected
counts omitted the exception-runtime roots; full diagnostics exposed that error
and the corrected assertion pins those actual counts. All 36 selected metadata
checks pass. Existing value/reference catalog boundary tests still pass.

Frozen EngineStep now reports 256 cached snapshots, 191 nominal declarations,
66 closed snapshots, 834 fields, 27,208 metadata work units remaining, 23 value/
176 reference/5 vector roots and six validated Int32 enums. Sixty-five snapshots
are outside explicit value/reference roots. This does not prove they are absent
from the eventual layout: fields, bases and interface discovery can add them.
StateLookup retains its existing 257/256 partial catalog failure.

Review verified read-only formatting, deterministic ordering, bounded samples,
remaining-budget reporting and unchanged classification/field/type checks. Logs
and immutable-input refresh are under artifacts/alchemy-type-classification.
The owned core hash remains ccb5b124686d6d0e494ae976fb6686d997f4ca2841bf91ab04cb4fe00de69977.

Next use declaration-only lexical visibility reads where full fields/interfaces
are not needed. Keep structural validation for actual layout/enum decisions and
retain both metadata-work and runtime-type bounds. Full replay remains unverified.
