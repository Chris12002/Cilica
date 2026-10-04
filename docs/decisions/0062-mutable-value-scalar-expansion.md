# ADR 0062: Mutable inline value expansion

Status: Accepted for authored IR with scalar/void returns. Recorded 2026-10-04.

## Context

Pure copies already expand into scalar slots, and native scalar locations can lend
those slots safely. Mutable struct locals and by-value parameters should share
that representation rather than introduce a second frame or collector model.

## Decision

`ValueScalarLowering.LowerLocations` accepts a validated `ManagedLocationIrProgram`
and explicit direct static method bindings. It returns a revalidated
`ScalarLocationIrProgram` for `ObjectCppEmitter.EmitLocations`. The profile is
`value-location-scalars-v1`; the existing pure-copy entry point remains separate.

Each inline value expands according to the existing nested field-path plan. Its
borrow expands into one exact scalar location per leaf. Addressing locals or
by-value parameters selects their own flattened storage, so mutating a by-value
parameter leaves its caller's copy unchanged. Nested fields select contiguous
leaf ranges; calls and void mutators expand their argument packets consistently.
Whole loads produce snapshots before any following stores. Whole stores and
initialization use the existing checked scalar operations. Empty values consume
no scalar slots, but calls involving them remain calls with normal fault/depth
behavior, even when their expanded argument packet is empty.

`ReadOnlyLocation` explicitly adds a readonly view with the same invariant element
type. The verifier rejects different elements, nonlocations and writable results.
Nested readonly fields and readonly parents produce readonly scalar views rather
than silently aliasing writable carriers. Whole-value replacement remains distinct
from writing an individually readonly field: replacing writable value storage can
copy all its fields, while deriving a readonly field location cannot permit writes.

Copy ordering preserves overlapping and self-aliasing cases. RHS leaves are already
snapshots, not lazy reads interleaved with writes. Expanded borrowed parameters are
all authenticated before entering a callee body; reference value parameters are
also checked before use. Validated frame borrows remain live for the synchronous
call. Leaf stores perform no allocation or callback. Under these bounded runtime
invariants, a valid whole store cannot encounter an expired later frame leaf after
mutating an earlier one. This is not transactional recovery from arbitrary native
memory corruption, concurrent mutation or untrusted replacement of internal buffers.

Before constructing expanded carrier arrays, lowering charges each location leaf
and owner against the 65,536 logical-slot bound. Parameter/local, statement and
argument limits are checked during expansion, and the output is revalidated with
the scalar target's root and resource limits. Actual native carrier and argument
bytes remain charged to the execution arena. There is no raw pointer arithmetic,
boxing or implicit allocation in this pass.

## Validation and limits

Local O0/O2 ASan/UBSan comparisons against C# cover nested mutations, by-value
parameter isolation, swaps, self-aliasing, whole snapshots and readonly reads with
normal/extreme Int32 inputs. A private native harness swaps live embedded reference
slots, verifies exact object identity and collection reachability, then verifies
reclamation after clearing those roots. A malformed final borrowed leaf is rejected
before any destination field changes. The 2 KiB stack-warning gate is retained.
Detached checks cover empty/readonly-field layouts, readonly view rejection,
expanded carrier/statement limits, binding restrictions and existing pure lowering.

The source remains authored detached IR: this pass does not authenticate inline
metadata field access rights or translate CIL. Source value IR still excludes
allocating object operations; the reference probe supplies objects from the private
native harness. It does not establish collection during an allocating whole-value
source program. Native inline fields in classes/arrays, struct-return packets,
instance struct methods, boxing and broader milestone 5 semantics remain separate
gates. No new platform compatibility is claimed.
