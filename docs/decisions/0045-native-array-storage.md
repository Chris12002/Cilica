# ADR 0045: Native vector storage and checked element operations

Status: Accepted for bounded retained-heap execution on the tested macOS ARM64 target.
Recorded 2026-10-03.

## Context and decision

ADR 0044 translates bounded Int32/reference vector operations. Native lowering must
preserve variable lengths, exact vector identity, managed fault order and roots
without treating an array's selected Object anchor as a nominal class allocation.

`object-frames-native-v8` implements those operations. An `ArrayObject` has the
ordinary first-member Object header, an Int32-bounded length and typed payload
pointers. A nonempty array owns a separate `new[]` allocation of uint32 elements
or Object pointers. This deliberately avoids flexible-array extensions and byte
arithmetic that would manufacture C++ subobject pointers. Value initialization
produces zero integers and null references; empty arrays still allocate distinct
headers and retain distinct identity. The descriptor destroys the typed payload
and header together. Reference cycles do not require recursive destruction.

Charge `sizeof(ArrayObject) + length * sizeof(element)` to the existing retained
heap byte budget and one object to its count limit. These are logical storage
charges, excluding allocator bookkeeping. Check negative length first (overflow
status 2), then roots/observer, object count and available bytes. Use subtraction
and division before multiplying or adding. Allocate both pieces before linking
into the heap or updating counters. If payload allocation fails, delete the
unpublished header. Allocation/resource failure remains status 5 at the newarr
origin. This slice retains the 1 MiB/4096-object default and instance lifetime.

## Type checks and fault order

Native symbols include the structural vector element identity. Each vector has a
separate descriptor and an element-acceptance predicate for reference stores.
Emit exact descriptor-address membership predicates from the validated assignment
closure for nominal classes, interfaces and arrays. That same closure implements
class ancestry, interface implementation, array covariance and widening to Object.
Compare descriptor addresses before reading unknown descriptor contents. Keep
nominal dispatch owners and receivers separate from vector identities.

Generated array operations check null (status 4), instance membership and exact
array-view compatibility (status 6), then bounds (status 10). Compare unsigned
index bits against the bounded length, which rejects negative Int32 indices too.
Reference stores then check element ownership/known descriptor (status 6) and the
allocation's actual element predicate (status 11 on mismatch). Null elements pass.
Only then write the slot. A failed store leaves its previous contents intact.
Loaded references pass the existing ownership/static-type checks. Method-entry
reference validation still precedes the body; an invalid native argument need not
reach the managed null/bounds/store ordering.

The emitter normalizes ldlen to the already validated bounded Int32 operation and
keeps every fault's image, MethodDef and IL origin. Operand evaluation remains in
IR order, so argument arithmetic/allocation can fault before the store itself.
The packed Int32 boundary still leaves results untouched on failure and releases
execution storage/roots. New statuses are private experimental fault tags, not
managed exception objects or a stable embedding ABI.

## Roots, inspection and boundaries

Arrays and loaded reference elements use the existing phase A slots. Array
allocation validates published roots and invokes the same pre-allocation observer
as class allocation. Descriptor callbacks expose dynamic reference count and the
actual reference element slots alongside fixed field reference maps. These are
inspection/future-tracing metadata; there is no collector or reclamation stress
claim. Arrays may appear as Object elements, including self-reference, while
jagged static array types remain outside the signature grammar.

Array helpers and ownership/bounds stages stay in small native functions. The
existing execution arena and native-stack gates remain. Primitive execution and
the CLI's Int32-only native profile are unchanged. Array-target casts, general
native integers, token-form element instructions, interior references, broader
primitive/value elements and framework array interfaces remain unsupported.

## Validation

The native suite compares authored C# with generated C++ at O0/O2, including
Int32 allocation/defaults/aliasing/calls/mutation/length, reference fields and
identity, interface and virtual calls with array signatures, covariant stores,
interface elements, cycles through Object[], class casts/tests, loaded references
across loops and allocations, and exact fault origins. Independent private-boundary
checks cover empty-array identity, byte/count limits, exact/short execution budgets,
cleanup/reuse, dynamic reference slots, failed-store nonmutation, foreign/invalid
pointers and copied/unreadable descriptors. Compilation disables C++ exceptions and
RTTI, enables ASan/UBSan and enforces the 2 KiB frame warning; separate stack-usage
reports require static frames. These are local target-specific execution results.

Review completed with a warning-free solution build and 919 selected local checks
passing in 102 seconds. The array harness compared 155 .NET/native outcomes per
O0/O2 build; both static stack reports measured a maximum of 656 bytes across its
generated code and driver. Review covered C++ object/payload lifetimes, arithmetic
bounds, publication before safepoints, actual-element checks and native ownership.
Local main remains an ancestor. No push, hosted runs or additional agents ran.
