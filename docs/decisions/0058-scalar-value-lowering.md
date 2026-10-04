# ADR 0058: Scalar lowering of pure value copies

Status: Accepted for bounded authored IR and local native conformance.
Recorded 2026-10-04.

## Decision

Lower `ValueIrProgram` to the existing `ReferenceIrProgram` before native emission.
Struct parameters, locals and block inputs become ordered Int32/reference slots.
Default values produce zero scalar storage; reference defaults load private,
zero-initialized typed locals. Field extraction selects immutable scalar IDs;
field replacement creates a new mapping without changing the operand. Nested
fields use the validated layout's scalar ranges. Struct copies do not allocate
managed wrappers or acquire object identity.

Preserve separate block-input snapshots before outgoing edge copies, so a
backedge that swaps two structs cannot overwrite its own sources. Calls expand
their arguments in declaration order. Require the original entry to retain the
packed Int32 boundary. Struct returns currently produce an explicit unsupported
error: native return packets need their own ownership/root and arena-byte design.
Do not silently discard fields or represent a return as an implicit box.

Use the existing object backend with the caller's detached declaration bindings
after lowering. It supplies the actual arena, phase A root publication, fault
propagation and native byte/stack bounds. Existing primitive/reference emitters
still reject inline IR directly. This pass is not a new runtime or a CIL value
translator; it does not authorize field access or implement mutable locations.

Before lowering, validate value IR and bound expanded scalar storage. Check
expanded parameters/locals, statements, call packets and edge arguments before
materializing them. Repeated stores can exceed the statement budget even when
their source value is small; the pass rejects that case. Cache field ranges and
block-input offsets rather than scanning them for every use. Validate the complete
lowered reference IR before returning it; native emission then applies its
existing root and arena bounds to the actual scalar representation.

## Review and evidence

Review checked immutable-copy independence, nested field ranges, simultaneous
backedge transfer, default reference types, expanded call signatures, erasure of
pure operations without loss of fault origins, and pre-allocation resource checks.
No new C++ pointer arithmetic, layout casts or managed boundary ownership is added.

Authored detached IR for three probes is lowered and executed at O0/O2 under
ASan/UBSan and the existing 2 KiB static-frame warning. Copies, scalar-returning
struct calls, parallel stack swaps and nested value updates match independent C#
probes for eight inputs including both Int32 extremes. All calls clean up their
frames and use zero managed allocations. These tests contain null reference fields;
they do not establish live embedded-reference collection stress or CIL translation.
The final selected value/layout/metadata/no-execution suite passes 98 checks.

Managed locations, struct-return packets, value fields in heap objects, live
embedded-reference stress, boxing, generics, delegates and managed exception
execution remain milestone 5 work. No mobile/web compatibility is claimed.
