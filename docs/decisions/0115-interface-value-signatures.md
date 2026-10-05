# ADR 0115: Interface value signatures

Status: Accepted

## Decision

The explicit integer-object CIL profile carries inline values and synchronous
borrow parameters through exact interface dispatch. Metadata plans retain the
same checked value layouts as the executable catalog. Validation compares both
catalogs structurally, including field storage widths, before accepting a
detached plan. Ref returns remain unsupported.

Interface declarations and implementation facts use the same signature expansion
as executable methods: value arguments become scalar leaves and value returns
use caller-owned rooted destinations. Read-only borrow capabilities, empty-value
calls, exact closed identities, receiver checks and argument evaluation order
survive lowering. Public standalone plan construction retains its existing
scalar/reference boundary; the composed discovery session supplies value facts.

## Evidence and review

Four C# probes match .NET in 1,176 repeated O0/O2 sanitizer results with forced
collection and exact/short frame budgets. They cover generic inherited and
explicit implementations, nested interface discovery, copied and borrowed wide
reference-bearing values, caught faults, null order and empty returns. Detached
plan round trips and layout/destination mutations are checked. All 69 distinct
focused checks pass; review found no remaining actionable issue.

The private kernel advances to a same-assembly internal helper call in its
CommandId constructor. Native kernel equivalence remains pending.
