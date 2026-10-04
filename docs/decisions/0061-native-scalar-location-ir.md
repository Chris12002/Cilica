# ADR 0061: Native scalar location IR

Status: Accepted for authored scalar IR and local native evidence. Recorded 2026-10-04.

## Context

ADR 0060 validates carriers without executing managed-location IR. Struct expansion
needs an executable scalar target that can lend storage while ordinary object
operations allocate and collect. Reusing the object backend retains its frame
budgets, access rules, constructor ordering and fault cleanup.

## Decision

Add `ScalarLocationIrProgram` (`scalar-location-ir-v1`) around `ObjectIrProgram`.
Its explicit validation/emission path accepts Int32/reference locations alongside
existing object operations. `ObjectCppEmitter.EmitLocations` uses the existing
emitter after validating this wrapper; `EmitProgram(ObjectIrProgram)` retains its
original rejection of locations. No raw address is introduced into public IR.

The shared location rules now support scalar object catalogs independently of
inline value catalogs. Field-address operations also use the object verifier's
visibility rules. Locations retain exact element identity and readonly lending,
cannot escape into locals, fields, block joins or returns, and cannot enter the
packed Int32 boundary. Methods with borrowed parameters must currently be direct
static methods. Existing constructors and dispatch remain supported with their
ordinary scalar/reference signatures.

Native v16 gives each location parameter/temporary a carrier in its budgeted frame
and a distinct `LocationOwner` root. Every carrier copy publishes its owner before
any allocating call. Frame-backed locations have null owners and refer to the
lender's already-rooted backing slots. Only methods taking the address of a local
or by-value argument create `BorrowScope` registrations. Argument initialization
precedes managed allocation, and borrowing callees validate the resolved storage
kind, exact reference predicate and readonly capability on entry.

The location path uses a larger private argument packet; ordinary object emission
keeps the previous packet. Both use arena storage charged by target `sizeof`, not
native stack-sized argument arrays. Phase A planning bounds 1,024 roots per method
and 65,536 logical slots per program, counting a carrier and its owner separately.
Readonly views can be added during argument/value copies but never removed.

Checked loads/stores/init use ADR 0060 helpers. Heap address creation reports the
existing null fault (4); invalid storage/capability reports ownership/type fault
(6). Registration exhaustion reports execution-storage fault (9). These defensive
faults carry the current method/IL origin. Normal returns and pending-fault exits
unwind borrow registration, roots and arena storage in order. No managed exception
objects or public borrowed-reference ABI are introduced.

## Validation and limits

Authored IR matches the equivalent C# oracle at O0/O2 with ASan/UBSan and the 2 KiB
stack-warning gate. Probes cover local/by-value argument mutation, synchronous
reborrowing, replacement of a reference slot, inherited heap fields, readonly
arguments, allocating callees, integer extremes and repeated collection before
allocation. A private call keeps an owner alive solely through its incoming carrier
once the callee publishes it. Invalid/readonly carriers, allocation failure and
token exhaustion exercise fault-origin and complete cleanup checks. Detached tests
cover visibility, invariance, escape/fault-edge restrictions, legacy rejection and
exact/excess root and expanded-storage bounds.

This is not CIL byref translation or native whole-struct execution. Expanding
`ManagedLocationIrProgram` into this scalar target, whole-value mutation/copy
atomicity, struct-return packets and CIL metadata/body translation remain separate
gates. Array/boxed locations, byref locals/joins, ref returns and borrowed instance
or virtual signatures remain unsupported. No additional platform compatibility is
claimed.
