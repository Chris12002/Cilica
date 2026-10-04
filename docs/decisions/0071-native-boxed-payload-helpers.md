# ADR 0071: Native boxed-payload helpers

Status: accepted for private runtime helpers and local native conformance probes.
Recorded 2026-10-04. Boxed IR lowering/emission and CIL translation remain planned.

`ObjectCppEmitter.EmitBoxedRuntime` emits a validated scalar-location program plus
`boxed-payload-runtime-v1` helpers for its supplied value catalog. Both inputs must
share the exact detached reference catalog. Every value layout in the bounded
catalog receives a private descriptor and typed scalar payload storage. Combined
class and boxed field slots share the 65,536 native slot limit before source
expansion. Existing `EmitProgram`/`EmitLocations` behavior is unchanged.

A box has the existing managed header followed by scalar members derived from the
value plan. Each nested field path gets a distinct member; typed accessors preserve
C++ subobject provenance. Reference maps contain only reference members. The same
allocator, object/byte limits, collector, destructor and scalar location carrier
serve boxes and ordinary objects. There is no synthetic metadata class or second
heap. Empty values still allocate a header with fresh identity.

Descriptor identity uses the value's content hash and TypeDef token. Known-box
checks compare descriptor addresses before dereferencing them; copied/unknown
descriptors are rejected. Boxes enter the selected Object predicate and can be
stored in Object fields, including traced cycles. They do not enter nominal class
or interface predicates. Their current descriptor base is Object: ValueType views,
boxed interface dispatch and broader type-test semantics remain gated.

`box_value` validates the full input packet, then allocates and copies. Its caller
must publish all input references across the allocation. No managed allocation or
callback occurs during payload copying. `unbox` checks null, instance ownership
and exact descriptor identity before exposing the owned object; borrowed storage
must keep this owner rooted, even when the payload is empty. Existing checked heap
locations address scalar payload members after that exact check.

`copy_box` validates every contained reference before writing any output, and
`replace_box` validates the whole input before changing any payload member. Typed
packets are compiler-owned and disjoint from payload storage. Whole-value
replacement may copy readonly fields; projected readonly field views must carry
the existing readonly capability. These helpers do not infer a field path from a
native pointer. Status returns distinguish null, type mismatch, invalid ownership,
allocation failure, readonly replacement and collection reentry. They do not yet
supply generated managed fault origins or executable boxing IR.

O0/O2 ASan/UBSan probes compare boxed identity, copy isolation and borrowed mutation
with a .NET oracle. They check exact identities for equal empty layouts in distinct
images, bad pointers/descriptors, incompatible or foreign references, and a corrupt
final reference with unchanged output. Collection before allocation preserves a
box/child cycle under a three-object cap using only a borrowed owner root; removing
edges and the last root permits reclamation. An empty borrowed owner also survives
allocation pressure and is reclaimed after its root ends.

Exact heap capacity succeeds and one byte less fails without publishing a box.
Object-count exhaustion and recovery, readonly writes, busy collection callbacks
and preservation of pending fault details have local evidence. The 2 KiB native
stack gate stays enabled; one oversized test-harness function was split to meet it.
The combined resource regression uses individually valid class/value layouts whose
total native slots exceed the shared limit. The catalog-mismatch regression uses
two valid catalogs so it exercises the intended check.

Review checked pointer validation order, member provenance, source/owner roots,
copy atomicity, descriptor membership and allocation accounting. No blocking
findings remain. This is the helper checkpoint for 5e.2a. Connecting boxed IR to
these helpers, preserving empty borrows during scalar expansion and proving
generated fault provenance remain the next slice. All 78 selected local checks
pass, including six O0/O2 native configurations; the tightened catalog mismatch
check also passed separately. The compiler builds with zero warnings/errors.
No hosted work ran.
