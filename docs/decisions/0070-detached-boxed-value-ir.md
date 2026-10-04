# ADR 0070: Detached boxed-value IR

Status: accepted for authored IR validation, logical payload layouts and source
root planning. Recorded 2026-10-04. Native execution and CIL translation are planned.

`BoxedValueIrProgram` selects `boxed-value-ir-v1` on the existing composed
value/object flow. Its entry remains Int32-only, with direct static value methods
and the existing scalar/reference constructor restrictions. Three explicit
operations separate allocation, copying and mutation:

- `BoxValue(Type, Operand)` copies an exact named struct into a fresh managed
  object, returning the selected Object reference view. It never interns boxes,
  including boxes of empty structs, and does not call a class constructor.
- `UnboxValue(Type, Operand)` checks a reference and returns a whole-value copy.
  Later mutation of the box or the copy must not change the other value's fields.
  Embedded reference identities are copied, not their target objects.
- `UnboxLocation(Type, Operand)` performs the same check and lends the box's
  payload storage. Whole-value writes and nested field borrows use the existing
  managed-location rules; readonly views cannot be strengthened to writable ones.

Both unboxing operations require a nonnull object and an exact payload type.
Null faults precede type mismatch; the future native implementation must check
instance ownership before reading a descriptor. A null or dynamically mismatched
reference is valid IR with an explicit exceptional exit, not a compiler error.
No subtype, equal-layout or cross-image substitution satisfies exact unboxing.
All three operations need explicit method-exit fault edges. Allocation can fail;
unboxing does not itself allocate.

The shared verifier admits these operations only through the new wrapper.
`BoxedValueIrValidator.PlanPayloads` derives used payload plans from validated
operation types, retaining the existing content-hash/TypeDef identities, nested
scalar paths and embedded-reference maps. A payload type is not a synthetic class
with fabricated metadata fields. Existing catalog, path, depth and expansion
budgets apply; repeated box sites reuse logical plans. Empty payloads retain their
type identity even though they have no scalar slots.

Source phase A root planning treats each box as an allocation safepoint, roots
the copied source's embedded references and roots its result. Payload locations
carry an owner root even for empty structs and can pass only to synchronous
static helpers. Existing restrictions prohibit their escape into locals, joins,
heap fields or returns. The 1,024-root per-method limit includes empty boxes and
borrow owners; expanded source storage remains bounded.

This contract covers named nongeneric structs already admitted by the value
catalog. Primitive, enum, nullable and reference-type boxing/unboxing, boxed
interface dispatch, ValueType reference views and value-type casts require later
contracts. Existing CIL profiles and native lowering still reject boxing. No
native descriptor, payload offset, ABI or executable behavior is claimed here.

Review checked profile isolation, exact type/image identities, fault edges,
root ownership, readonly propagation and shared resource limits. Authored tests
cover repeated nested payloads, empty values, copying versus borrowing contracts,
allocating borrowed helpers, malformed operations and exact root-budget limits.
Runtime identity, copy isolation, fault order and collection must receive native
differential evidence in the next slice.

All 231 selected local boxing/value/reference/location/layout checks pass. The
compiler builds with zero warnings/errors. No blocking review findings remain;
no hosted work ran.
