# ADR 0109: Closed interface CIL calls

Status: Accepted

## Decision

The explicit integer-object CIL profile discovers scalar/reference interface
calls without pretending abstract declarations have IL bodies. Each call retains
its exact declaration, argument types and dispatch semantics. The shared plan
builder maps every compatible catalog class and supplies authenticated facts for
discovered implementations.

Discovery alternates method inspection and catalog/dispatch preparation until
no new executable targets remain. Constructed identities and immutable snapshots
are retained between passes; metadata work and executable/declaration/slot facts
remain bounded by shared ledgers. A target body can introduce further interface
calls and receiver types. Plan signatures are compared with body inputs before
translation; existing access and unsupported-operation checks still apply.

Calls keep argument evaluation before null checks, then pass through the existing
value roots, exception routing and native interface machinery. Struct/borrow
interface signatures, variance-only target selection, generic methods, default
bodies, class virtual call preparation and array helpers remain diagnostics.
Legacy CIL entry points retain their previous boundaries.

## Evidence

Four C# closures match .NET in 1,176 repeated O0/O2 ASan/UBSan results with forced
collection, exact/short frame budgets and cleanup checks. They cover wide
arguments/results, same-width signed/unsigned specializations, explicit mappings,
reference identity, chained discovery, wide values and caught fault ordering.
All 136 focused checks pass. Review found no remaining actionable issue.

The private real-kernel probe passes its earlier bodyless interface-call gate
and reaches a class virtual declaration at IL_02ba (`WriteCanonical`). Native
kernel transcript equivalence is still unestablished.
