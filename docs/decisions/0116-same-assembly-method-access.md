# ADR 0116: Same-assembly method access

Status: Accepted

## Decision

The integer-object profile admits public, assembly and family-or-assembly calls
within the same content-addressed assembly when every declaring owner permits
assembly access. The bounded metadata owner walk records this fact alongside
external visibility; closed snapshots retain it. Discovery and detached IR use
one access rule, and interface implementation facts retain the same evidence.

Private, family and family-and-assembly access across different owners remain
unsupported. A public nested type cannot bypass a private ancestor. Different
assembly images receive no internal access, including matching assembly names;
friend assemblies and broader protected access are not implemented. Existing
profiles keep their access boundary. Field and type-operand access rules are not
broadened by this method-call slice.

## Evidence and review

All 84 focused checks pass, covering owner/member visibility, cross-image
rejection, a hidden enclosing type, detached binding mutations and legacy
profiles. Two C# probes match .NET in 588 repeated O0/O2 sanitizer results for
value copies, borrows, allocation and caught faults. Review found no remaining
actionable issue. The private kernel now reaches closed generic default
initialization in PassPriority; native equivalence remains pending.
