# ADR 0072: Native boxed IR expansion and emission

Status: accepted for authored IR and local native conformance. Recorded 2026-10-04.
CIL boxing remains a separate gate.

`ValueScalarLowering.LowerBoxes` expands the checked source boxing contract into
`ScalarBoxedIrProgram` (`scalar-boxed-ir-v1`). The scalar program retains its exact
value catalog to authenticate payload types and slot kinds. `BoxScalars` carries
the complete scalar packet, `CheckBox` returns the exact checked owner as an Object
reference, and `AddressBoxSlot` lends one typed scalar payload member. Existing
profiles reject these operations without the boxed catalog.

Every source unbox emits a `CheckBox`, including an empty value copy or borrow.
That check remains a rooted temporary in its source frame even when all payload
leaves disappear. Synchronous callees cannot outlive this frame, so forwarding an
empty borrow does not discard its owner or its null/type check. Nonempty borrows
also use the existing location owner roots. Copies load all source leaves into
temporaries before later writes, preserving overlapping-copy behavior.

`ObjectCppEmitter.EmitBoxes` emits the scalar contract using ADR 0071 descriptors
and helpers. Typed box packets live in the bounded execution arena, not large
native-stack arrays. Source references are already rooted when allocation runs;
the result is published before any subsequent allocation/call. Exact unbox checks
precede scalar slot access. Fault status maps to the existing private pending
faults: null 4, allocation 5, invalid ownership/storage 6 and type mismatch 7.
Generated exits retain the source method/image and original IR offset.

Source and scalar verification both run. Box packets share per-packet and aggregate
argument budgets; generated temporaries, locations, roots, statements and arena
bytes remain bounded. Empty payloads can still add checked-owner roots, so target
root planning runs after expansion. A regression verifies that a source with only
three roots cannot expand thousands of empty unboxes past the 1,024-root limit.

O0/O2 ASan/UBSan probes execute authored source IR through lowering and emission.
They compare independent boxes, copied values, borrowed mutation and embedded
reference identity with .NET, collecting before allocation. Empty payload borrows
cross an allocating static call. Null and wrong-type copies/borrows preserve fault
origins and leave packed output unchanged. Allocation failure unwinds all scopes
and permits context reuse. Exact measured arena capacity succeeds; one byte less
fails cleanly. The 2 KiB native stack gate remains enabled.

Review checked exact scalar packets/slots, profile isolation, empty-owner lifetime,
phase A publication, fault origin and expanded resource accounting. No blocking
findings remain. Named-struct CIL boxing, primitive/enum/nullable cases, ValueType
views and boxed interface dispatch remain unsupported. No hosted work ran.
