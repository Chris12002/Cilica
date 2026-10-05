# ADR 0118: Class constructor value signatures

Status: Accepted

## Decision

The integer-object profile admits inline value parameters, value locals and
synchronous borrows in ordinary class constructors. Construction reuses existing
rooted allocation, argument expansion and checked locations. Value-returning
helpers used in base arguments retain caller-owned rooted return destinations.
Empty value arguments still perform construction and initialization.

Before the immediate base constructor runs, value computations and locations
independent of the new receiver are permitted. Taking the receiver's parameter
address, aliasing it or passing it to other calls remains invalid. The existing
straight-line constructor and exactly-once immediate-base requirements remain.
Legacy profiles keep their scalar/reference constructor boundary. Inline value
fields in heap objects and broader constructor control flow remain separate work.

## Evidence and review

Three C# probes match .NET in 882 repeated O0/O2 sanitizer results for rooted
value copies, aliasing borrows, empty values, base-argument helpers, reference
identity and thrown constructor/argument faults. Direct IL checks distinguish
receiver-address use before and after base initialization; detached mutations
preserve the mandatory base call. All 107 distinct focused checks pass. Review
found no remaining actionable issue; local main remains an ancestor.

The private kernel advances to boxing a closed reference argument for a null
test. Native kernel equivalence remains pending.
