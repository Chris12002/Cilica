# ADR 0066: Value and vector operation composition

Status: accepted for authored IR validation and lowering. Recorded 2026-10-04.

## Decision

`ValueArrayIrProgram` is an explicit wrapper for inline copies, synchronous borrows
and existing vector allocation/access operations. `ValueScalarLowering.LowerArrays`
validates source bindings and types, expands values/return packets, and revalidates
the scalar location output. The earlier value entry points still reject allocation.

Object operation rules accept the validated value catalog for direct static method
signatures. This reuses array length/index/element checks and call accessibility;
it does not relax instance, constructor, dispatch or public boundary rules.
The composed wrapper explicitly rejects class/string allocation and other object
operations that the value lowering cannot yet preserve. Entry remains Int32-only.

Root planning treats vector allocation as a collection point. Struct copies and
return destinations retain their existing per-field roots. Array operation operands
are remapped to scalar SSA values; no new collector, carrier or native ABI is added.
Both source and expanded output remain subject to existing resource budgets.

## Review and scope

Manual review checked old-profile rejection, source signature/access validation,
allocation fault edges, operand remapping, embedded-root plans and return-packet
reuse. Focused negative cases exercise bad lengths/elements, missing fault edges,
nonstatic/inaccessible calls and unsupported object operations. All 104 selected
value/location/composition checks pass. CIL allocation translation and native
live-reference collection evidence are the next slice; this IR checkpoint does
not claim them. Class constructors remain separate work.
