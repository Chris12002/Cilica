# ADR 0036: Interface catalogs and reference flow

Status: accepted for detached catalogs/type flow; executable maps/native calls remain planned.
Recorded 2026-10-02.

## Context

ADR 0035 resolves an exact interface implementation, but the compiler catalog
previously described class layouts only. An interface is a reference type without
its own instance storage. Abstract method declarations also have no body to place
in the current executable method IR. Establish the type/storage foundation before
adding executable declarations/maps and native calls.

## Decision

`object-layout-v3` retains `ObjectInterface` records separately from
`ObjectTypeLayout`. Contracts contain exact content-hash/TypeDef identities,
attributes and parent interfaces. Classes retain declared interface edges; fields
can refer to classes or interfaces with the same `ObjectStorage.Reference` kind.
The selected fieldless System.Object remains a class. Interface roots are valid.
Contracts are public, nongeneric, abstract, fieldless and have no class base,
native layout or initializer obligations. Catalogs do not authenticate method
declarations/implementations, just as class layouts do not grant executable access.

`ReadTypeInput` snapshots all direct InterfaceImpl rows. Each consumes shared
metadata work; the list has an independently configurable 0..256 count bound.
Separate field, base and interface decoders preserve the exact field boundary.
Catalog classes/contracts together share 256 types, 4,096 aggregate fields,
65,536 metadata work units and 64 enclosing owners. Snapshot signature nodes
remain resolver-owned; the finished catalog contains detached identities only.

Validate edge kinds before computing ancestry: class bases name classes;
declared interfaces and interface parents name contracts. Reject missing,
duplicate, noncanonical, unrelated and cyclic graphs, and class/interface identity
collisions. Use a parent-first iterative graph walk rather than recursive traversal.
Cached ancestor sets contain at most 256 squared identities. Include Object in
each interface's assignment ancestors. Field-reference cycles remain legal and
do not count as inheritance cycles.

`typed-reference-ir-v3` permits exact class/interface/null reference flow through
locals, parameters, returns, direct calls and declared block joins. Concrete
classes widen to ancestors and implemented contracts, including inherited
contracts. Interfaces widen to parent contracts and Object. Null enters any
reference storage. Loads and call results retain their declared type. Equality
and truth tests keep their existing reference rules. Narrowing, unrelated
interface conversion and forged identities fail validation. Catalog usage closure
includes contract edges; explicit roots cannot smuggle unused types into a program.
Existing `IrType.Class` holds either identity; adding another reference storage
kind would duplicate representation without supplying dispatch semantics.

The object-operation/native profile remains class-only. Reject any interface
catalog at `ObjectIrValidator` before operation validation, root assignment or C++
generation. Static methods with no interface value/storage dependency need not
include their owner's layout. Native profiles/ABI are unchanged. Interface
declarations without bodies, implementation maps, casts/tests, CIL join inference
and native calls are separate work. Private explicit bodies must become reachable
only through authenticated interface maps, preserving ADR 0035's access boundary.

## Validation and review

An independently authored diamond hierarchy checks all 64 source/target assignment
pairs against .NET `IsAssignableFrom`, including interface-to-Object and inherited
class contracts. Detached tests cover locals, direct calls, returns, declared joins,
null, retagging rejection and field/usage closure. Raw metadata covers graph
construction, interface-only roots, detached lifetime, contract JSON roundtrip,
malformed shapes, exact/excess combined type limits and direct-list/member/field
budgets. Equal-name/equal-token contracts from different images remain distinct.
Native entry points and CIL programs containing included contracts are gated.

Review checks graph edge kinds, queue/closure bounds, cycle handling, exact identity,
resource accounting, storage representation and every native consumer's validation
entry point. Actual collection, native interface root survival and interface
dispatch conformance are not established by these detached tests. Performance
tuning remains deferred under ADR 0029.

Final validation: zero build warnings/errors; all 698 relevant local
native/object/reference-IR/type-input/C++/Int32/exact-target checks pass in 40.5
seconds, including existing O0/O2 sanitizer, root/resource and stack regressions.
Local main and cached origin/main remain ancestors. No hosted work, push,
additional agent or service was started.
