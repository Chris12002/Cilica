# ADR 0023: Typed reference value flow

Status: accepted for detached IR validation; CIL objects and native references remain planned. Recorded 2026-10-01.

## Context

The existing executable IR assumes every value is Int32. The layout foundation
retains exact class identities, but method values, joins and calls need those
identities too. Adding allocation before defining their assignments would hide
type mistakes and rooting obligations. Slice 3b is therefore split into 3b-a
(this structural contract) and 3b-b (CIL object operations and declaration rules).

## Decision

1. Replace the bare `IrType` enum with a sealed value record: Int32, a reference
   carrying an `ObjectTypeId`, or the null stack type. Preserve the `IrType.Int32`
   spelling and existing primitive semantics. This is an early evolving compiler
   API, not a stable serialized format. Invalid kinds and contradictory payloads
   are rejected at the validation boundary.

2. Share the existing ordered statements, block-local values and direct-call
   nodes. Add an explicit null expression and a separate `ReferenceIrProgram`
   profile. One validator enforces the shared control-flow/resource rules using
   profile-specific type rules; the primitive emitter supplies no reference
   catalog and consequently rejects reference/null IR before emitting C++.

3. An `ObjectTypeCatalog` starts at several explicit roots, reusing the same
   layout checks as the single-root `ObjectModel`. The selected object base and
   every reference-field target must be present. Roots are unique and canonical.
   The program additionally rejects types unused by declared value/storage types
   or their field closure. The existing 256-type/4,096-field bounds remain shared.
   Catalog composition is detached validation, not new metadata discovery.

4. Parameters, locals and return signatures use concrete references or Int32.
   Null is allowed only in value temporaries and block inputs. Load results and
   call results retain exactly their declaration's type. Assignments, arguments,
   returns and edge copies accept exact types, null into any reference, and any
   included class into the selected object base. Other inheritance is outside
   the layout profile; there are no implicit downcasts or sibling conversions.

5. Joins validate against declared destination types. Two sibling classes can
   enter an Object input; a class and null can enter that class's input. The
   future CIL translator must infer these inputs, including backedges; this
   validator does not compute a least common type. Values cannot retag stack
   inputs, parameters, locals or returned call values to bypass a cast.

6. Equality/inequality of references (including null) yields Int32. Zero/nonzero
   reference branches are permitted. References cannot enter arithmetic,
   bitwise operations, ordered comparisons or mixed integer/reference comparisons.
   Existing integer arithmetic retains its explicit fault edges. Every call has
   an explicit method-exit fault edge; this is not yet a safepoint/root assignment.

7. Preserve complete method/block reachability, canonical content-scoped method
   targets, consecutive value IDs, earlier operands and aggregate program bounds.
   Closure traversal is iterative, permitting cycles without recursive validation.
   Unknown nodes/types, absent targets, mismatched arity, forged signatures and
   unsupported assignments fail with an API argument error and no artifact.

## Limits and subsequent work

This contract does not authenticate metadata method declarations. It represents
explicit parameter value flow, not verified instance-call or constructor access.
Void methods, allocation, constructor/base intrinsics, field operations,
readonly/access checks and CIL translation remain in 3b-b. There is no native
reference entry, object fault ABI, byte layout, heap ownership, root frame or GC.
Internal reference-returning methods do not authorize references at an embedding
boundary. Native root/lifetime evidence remains a prerequisite in slice 3c.

## Alternatives considered

A bare reference enum tag would permit incompatible classes to share storage
without proof. Name-based typing would confuse same-named classes in different
images. Retain the layout model's content hash and TypeDef identity instead.

A separate copy of the control-flow validator would allow bounds and fault rules
to drift between primitive and object profiles. Sharing the structural validator
keeps those rules consistent while an explicit catalog controls reference types.
Implementing allocation at the same time would make type and lifetime defects
harder to isolate; it stays behind the next translation and rooting gates.

## Validation

Independent caller-constructed IR tests cover null/reference locals and returns,
exact call result types, argument assignability, null and sibling joins,
backedges/recursive calls, identity comparisons and truth branches. Rejections
cover retagged values, downcasts, arithmetic/type mixes, invalid kinds/identities,
null callee parameters, missing fault edges and unused catalog roots. Exact and
excess call-argument and block-input/edge-copy boundaries are exercised. A reduced
forged-callee regression guards against dereferencing a null parameter type.

Existing metadata layout and primitive frontend/backend tests remain applicable;
the native Int32 differential corpus must remain green. Object/reference
execution has not run and is not evidence supplied by this ADR.
