# ADR 0153: Lexical access to nested cache owners

Status: Accepted for nongeneric lexical scopes in the integer-object profile

## Decision

Metadata type and method snapshots retain a bounded declaring-type chain with
TypeDef tokens, visibility and generic arity. Carry those facts into class layouts,
static field declarations and method bindings. The compiler and detached validator
use the same access rule for private nested owners: each private boundary requires
the caller to be the containing type or inside its lexical scope, in the same image.

This permits public fields and public/assembly-accessible methods on generated
private cache classes without making their owners globally visible. Public instance
field access and type tests follow the same rule. Private members still require
the exact declaring owner; protected nesting and nonglobal generic lexical access
remain outside this addition. Existing public and assembly method access retains
its previous contract.

Validate chain depth, token kind, uniqueness, visibility, generic arity and parent
relationships. Repeated declarations across layouts, fields and bindings must agree.
Externally/assembly-visible claims cannot contradict a supplied chain. Work is
bounded by the metadata ledger. Missing chains grant no new lexical access.
Object layout v11 and object operations IR v12 carry the detached facts.

## Evidence and review

Three probes compare 882 repeated O0/O2 native/.NET results with sanitizers,
collection before allocation and persistent cache roots. They cover a generated
noncapturing lambda, an explicit private cache class, instance fields and sibling
nested access. Negative cases reject unrelated/cross-image callers, private member
access, cycles, contradictory parents/visibility, missing/default chains and forged
assembly visibility. The full solution builds without warnings.

Forty composed compiler/runtime tests and 21 metadata input tests pass. Eleven
focused lexical tests pass after adding two further access regressions (63 distinct
checks). Review covered scope boundaries, default-array handling, budget limits,
metadata-only snapshots and consistency between discovery and detached validation.

The unchanged concrete kernel passes the initialized private callback cache and
now reaches the missing System.ValueTuple`2 mapping at StepPriority IL_00b8. This
advances compilation closure, not native transcript equivalence.

## Follow-up correction

Interface mapping construction must retain the same lexical chains as ordinary
method discovery. Compare independently read chains structurally, including binding
and static/catalog agreement, rather than relying on immutable-array object identity.
Copied snapshot and missing-evidence regressions plus related native/metadata checks
pass (107 tests). This corrects agreement checking without widening lexical access.
