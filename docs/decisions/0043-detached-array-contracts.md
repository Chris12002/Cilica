# ADR 0043: Detached vector identities and array operations

Status: Accepted for detached catalogs, value/operation validation and phase A root
assignment. Array metadata/CIL lowering and native execution remain planned.
Recorded 2026-10-03.

## Context

Arrays add structural types and variable-sized allocations. A class layout has
fixed fields and a TypeDef identity; an array has an element type and a per-instance
length. Treating arrays as ordinary classes, or erasing them to Object, would lose
the information required by covariance, element loads, runtime store checks and
eventual heap tracing. Establish a validated contract before translating array IL
or changing the retained heap.

## Decision

`object-layout-v4` retains nominal class/interface identities and adds explicit
array identities in `ObjectTypeCatalog.Arrays` and `ObjectModel.Arrays`. A vector
uses the selected Object's image/token as its runtime anchor plus a discriminated
element: Int32 or a nominal class/interface identity. The element is part of record
equality; the anchor is not a fabricated array TypeDef. Ordinary definitions,
field/method owners, bases and interface edges must remain nominal. Names and
equal tokens in different images cannot merge reference element identities.

The initial profile is zero-based, one-dimensional vectors. Elements cannot be
arrays, other value types, generics, pointers or byrefs. A reference element's
class/interface and all its ancestors/fields/contracts belong to the catalog.
Array references can occur in fields, retaining ordinary logical reference slots.
Classes, interfaces and distinct vectors share the existing 256-type limit.
Reject missing/default/duplicate/unrelated vectors and invalid anchors/elements.
Reject unsupported recursive shapes before dictionary hashing or record traversal;
validation must not consume the host stack on caller-forged nested types.

`typed-reference-ir-v4` admits arrays through the existing reference storage kind.
Null flows into arrays. Vectors widen to the selected Object. Reference vectors are
covariant when their nominal element types are assignable, including class ancestry
and implemented/inherited interfaces. Int32 vectors remain invariant. No boxing
turns Int32[] into Object[]. System.Array, array-implemented framework interfaces,
generic array helpers and array-target casts/type tests are outside this slice.
The bounded ancestry cache includes declared vector identities; this slice validates
declared joins, rather than synthesizing inferred array types from CIL.

`object-operations-ir-v7` adds four detached operations:

- `NewArray`: an explicit catalog vector and an earlier Int32 length; the result
  retains that vector identity. Element access follows existing visibility rules.
- `ArrayLength`: an array or null reference; returns the bounded Int32 length.
- `LoadElement`: an explicit static array view, an assignable receiver and an
  Int32 index; the result exactly matches the view's element type.
- `StoreElement`: the same receiver/index rules plus a value assignable to the
  static element type. A covariant view does not prove runtime store safety.

Each operation requires an explicit method-exit fault edge. Negative lengths,
negative indices and null receivers remain valid IR with runtime faults, rather
than translation errors. Required lowering semantics are: evaluate operands in
statement order; allocate distinct zero-initialized storage; report negative length
as overflow and allocation exhaustion as allocation failure; check null before
bounds for element access; then check a reference store against the allocation's
actual element type and report an array-type-mismatch fault without mutation.
These are obligations for subsequent lowering, not implemented runtime claims.
The Int32 length contract does not introduce general native-sized integer support.

Phase A assigns existing reference slots to array parameters, locals, inputs,
allocation results and loaded reference elements. `NewArray` is an allocation
safepoint. Reference stores cannot leak an unfinished constructor receiver; array
allocation unrelated to that receiver remains legal before base initialization.
Dynamic element tracing requires a later native descriptor/allocation design.

The metadata builder and CIL translator continue rejecting array fields/signatures
and instructions. The object emitter explicitly rejects any array-bearing catalog,
including one with only array locals and no allocation. This keeps incomplete
lowering from treating an array anchor as the ordinary Object descriptor. Primitive
emission remains unchanged. Interface plan/executable catalog agreement includes
the array identities, while mapping receivers and method owners stay nominal.

## Validation and next slices

An exhaustive 14-by-14 assignment matrix matches .NET for the modeled nominal and
vector types. Detached tests cover JSON round trips, cross-image element identities,
covariant calls/locals/returns/declared joins, null and identity comparisons,
array-field closure/reference slots, exact/excess combined type limits, typed
operation results, explicit fault edges, root/safepoint assignment, constructor
escape rules and inaccessible allocation elements. Forged operations and 4,096-level
nested identities fail without partial acceptance. CIL and native rejection tests
keep the implementation boundary explicit.

Review completed with a warning-free solution build. The broader local run passed
874 object/reference/dispatch/Int32/C++/control-flow checks. A final review fix
validates callee signatures before assignment or dispatch can compare/hash them;
its 275 focused array/reference/interface/dispatch/join checks all pass, including
the new reduced regression. Existing native tests exercise unchanged nominal
execution; they are not array execution evidence. Local main remains an ancestor.
No push, hosted work or additional agents ran.

The next slice reads SZARRAY signatures and lowers supported array CIL, preserving
native-length conversion rules, instruction origins and metadata/work budgets.
The following slice implements bounded variable storage, dynamic element metadata,
runtime covariance checks, ordered failures and .NET/native conformance. Actual
collection, jagged/multidimensional arrays and broader library semantics remain
separate work.
