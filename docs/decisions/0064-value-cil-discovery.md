# ADR 0064: Bounded value CIL discovery

Status: accepted for metadata discovery. Recorded 2026-10-04.

## Decision

`ValueProgramDiscovery.Read` gathers a direct static method closure before body
translation. It returns typed method snapshots, exact per-instruction call/field/
type operands and the existing value catalog. This result is not executable IR:
stack verification and supported-opcode translation remain required.

The first signature grammar accepts Int32, named nongeneric structs, void returns
and synchronous byref parameters of Int32/struct storage. References embedded in
struct layouts retain their existing precise maps. Standalone reference signatures,
modifiers, byref locals/returns, generic instantiations, special-name methods,
initializers, exception regions and uninitialized locals remain diagnostics.
Plain CLI byref signatures describe writable locations; C# `in` parameter attributes
alone do not constitute CLI readonly capabilities. Required signature modifiers
are rejected rather than ignored. Readonly fields retain their metadata flags.

Discovery shares one metadata-member ledger with value/reference layout building
and enforces aggregate method, IL, signature-storage and call-argument limits.
Repeated definition/operand lookups are cached, with instantiation checks before
method identity reuse. Entry remains an Int32 signature. Layout discovery validates
actual selected ValueType ancestry even for bare tokens encoded without a kind.

Call and field access permit the same declaring owner, or public members on fully
externally visible types. Private-scope members are rejected. Value layouts now
retain enclosing-type visibility from metadata, so a public field inside a hidden
outer type cannot bypass the check. Static methods cannot write readonly fields;
address translation must propagate readonly capability.

## Review and validation

Manual slice review checked metadata ownership, definition-cache identity, shared
budgets, visibility through enclosing types, readonly writes and discovery versus
execution claims. It caught and removed a quadratic field-write scan and added
explicit private-scope field rejection. Real return/borrow C# fixtures are read as
metadata; reduced malformed/inaccessible fixtures test diagnostics. No input code
is executed by discovery. Focused discovery/layout results are recorded in the
milestone tracker; executable CIL evidence belongs to the next slice.
