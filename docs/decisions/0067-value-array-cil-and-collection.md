# ADR 0067: Value/vector CIL and live-reference collection

Status: accepted for bounded compiler-library execution. Recorded 2026-10-04.

## Decision

`ValueTranslator.TranslateArrays` selects `value-arrays-cil-v1` and returns the
ADR 0066 composed wrapper. `ValueProgramDiscovery.ReadArrays` shares the existing
static closure reader with an explicit array option. The original CIL/value APIs
keep their existing capability gates. There is no new CLI profile or native ABI.

The signature grammar adds Int32 vectors as storage and synchronous parameter
borrows. Allocation accepts a named TypeDef/TypeRef resolving to the exact selected
runtime's Int32. A same-named type in another scope does not qualify. Resolved
operands are cached; arrays used only by operations/signatures are included in the
shared bounded value/reference catalog even when no struct field names them.

Body translation adds `newarr`, `ldelem.i4`, `stelem.i4`, and `ldlen; conv.i4` as an
adjacent pair within one block. Other native integer flow and element-address
borrows remain diagnostics. Array operands are exact Int32 vectors or null;
indices, lengths and elements are Int32. Shared object rules validate the result
before expansion. Reference equality/inequality and truth branches retain identity;
`cgt.un` against null lowers to inequality. Other ordered reference comparisons
and widening reference stack joins remain unsupported.

The source values expand through existing scalar root, carrier and caller-owned
return-packet contracts. Collection can occur in a nested call while its struct
parameter is the sole owner of a reference, or while a caller lends a nested struct
for replacement. No new collector or allocation implementation is introduced.

## Review and evidence

Manual review checked selected-runtime identities, signature/opcode gates,
operation-only catalog roots, cached resolution, exact stack/array types, field
copy aliasing, allocation fault offsets and unchanged old APIs. No blocking
findings remain. Focused regressions reject fake Int32/value allocation elements,
wrong array/index/element types, element addresses and split/missing length
conversion. Existing layout/array/object rules remain covered by targeted tests.

Local O0/O2 ASan/UBSan probes translate real C# into native code and compare outputs
with .NET, including extreme Int32 values, zero-length arrays and negative lengths.
The test enables collection before every allocation, makes 20 allocations per
successful call with an eight-object heap cap, and verifies identity, nested copies,
borrowed replacement and struct returns. Repeated calls reclaim every object.

A private caller removes its own input root before invoking an allocating callee,
then checks the returned array stays alive through only the caller's destination
root. Clearing that last root reclaims it. Exact/one-byte-short heap and arena
budgets are tested, along with nested allocation failure and successful reuse of
the same context. Root, borrow and arena scopes unwind completely on faults;
packed output and return destinations remain unchanged on failure. The 2 KiB
native stack-warning gate stays enabled. Results are recorded in the milestone
tracker, with no broader platform claim.

## Remaining scope

Class allocation/constructors composed with values, reference/value array CIL
allocation, inline values in heap objects, array-element borrows, instance struct
methods, boxing, generics and managed handlers remain later slices. The authored
vector IR can express the already-supported reference vectors, but this CIL gate
and its live-reference stress evidence use Int32 arrays.
