# ADR 0128: Exact implemented-interface constraints

Status: Accepted

## Decision

Validate nominal interface constraints in closed type and method snapshots.
Substitute both declaring-type and method arguments before checking exact closed
identity. Walk declared interfaces and class bases, including inherited generic
interfaces, without executing constructors or constructing dispatch plans.
Cache each argument's closure within the snapshot. Charge all inspected rows to
its existing metadata ledger; bound ancestry to 64 levels and 256 types and
queue every discovered declaration for constraint and stack-only validation.

Reject cyclic/duplicate ancestry, incorrect interface/class edges, open arity and
interface value-type encodings even if another edge already matches. Normalize
primitive arguments through the selected runtime identity. Selected
UnmanagedType-modified ValueType constraints are provable for supported integer
primitives only; aggregate unmanaged proofs remain unsupported.

This enables ordinary C# struct helpers constrained to IEquatable<T> and other
exact interfaces to use ADR 0127's checked direct receiver calls. Class/dependent
parameter constraints, array helpers and variance-only constraint conversions
remain diagnostic. Library mapping's constraint correspondence is a separate
gate; this decision does not authorize a mapped library or broaden body access.

## Evidence and review

All 141 focused local checks pass, covering CLR agreement for direct/inherited, self-referential,
primitive and mixed owner/method environments, malformed ancestry and budget
exhaustion. Two additional C# probes supply 588 matching repeated native O0/O2
sanitizer results, including mutable receivers and the actual runtime IEquatable
contract. Existing constrained calls, special constraints, variance declarations,
stack-only rejection and library binding regressions remain covered.

Review added rejection of interface value-type encodings and corrected a stale
test that still rejected AllowByRefLike. No runtime ABI or application rules
changed. Required library implementations and native kernel equivalence remain
pending.
