# ADR 0059: Detached managed storage locations and synchronous borrows

Status: Accepted for bounded detached IR and root planning. Recorded 2026-10-04.

## Context

Pure value operations copy their operands. They cannot represent CIL operations
that initialize or mutate a struct in a local, argument or object field. Exposing
a native pointer prematurely would lose its heap owner or let a frame address
survive the invocation that owns it. The next native slice needs an explicit
storage and lifetime contract first.

## Decision

Add `ManagedLocationIrProgram` (`managed-location-ir-v1`) around the existing
value program. A managed location has an exact scalar/reference/value element
type and a readonly flag. It is distinct from a copied value, an object reference
and an unmanaged pointer. Nested locations, null/void elements, arithmetic,
identity comparisons, implicit boxing and type reinterpretation are rejected.
Element identity is invariant even for reference elements. A writable borrow can
be lent as readonly; a readonly borrow cannot become writable.

Address expressions name one invocation's local or by-value parameter storage,
or an exact field and object receiver. A nested inline field extends a location
through a validated FieldDef path. There is no address-of-temporary expression or
numeric byte offset. Readonly parents and initonly fields force readonly child
locations. Whole-value replacement/initialization is distinct from separately
writing a readonly field. As with existing detached value operations, validation
is not proof of metadata access rights or constructor authorization.

The lifetime rule is deliberately conservative: an entry cannot accept borrowed
locations without a caller; internal direct calls can borrow and synchronously
reborrow locations. Borrowing calls, including recursive calls, finish before
the lending frame returns. No method returns a location. Locations cannot be
stored in locals, object/value fields or block inputs; ordinary copies and
reference values can still flow through those sites. Locals and joins for byrefs,
array-element locations, ref returns, boxed storage, native calls and callbacks
require separate contracts rather than guessing provenance or lifetime.

Location loads copy their element. Stores require a writable location and an
assignable element value; initialization resets writable storage. Loads, stores,
initialization and heap-field address creation retain explicit method-exit fault
edges for native ownership/validity checks. Frame address creation and nested
field derivation are pure. Direct void mutators use existing invoke statements
and call safepoints, with the same complete closure and aggregate resource checks.
No managed operation executes in this slice and no runtime fault tag is added.

Phase A plans distinguish a location's heap-owner root from the interior storage
identity. Frame-origin locations have a null heap owner and borrow an active
ancestor/current frame; their backing scalar/reference slots remain rooted by
the lending frame. Each location copy has its own owner root, including parameters
and derived locations. Location carriers count as two logical storage slots
(owner plus storage identity); native byte layout/authentication is still a later
gate. The 1,024-root method and 65,536-expanded-slot program bounds remain.

The shared verifier validates these operations only through the new wrapper.
Existing value-copy, reference, primitive and native object profiles continue to
reject them. This avoids advertising native byrefs before there are checked
carriers, stable storage, return packets and live-reference collection evidence.

## Validation and review

Focused detached regressions cover local/parameter origins, nested/heap fields,
readonly propagation and lending, void calls/reborrowing, escapes, exact element
identity, malformed types/operands, fault edges and exact/excess root/storage
budgets. Legacy emitters reject the new operations. Existing copy/native and
collection regressions validate the shared verifier remains compatible.

Native CIL struct execution, mutable inline class/array fields, carrier lifetime
and ownership authentication, and stress collection through borrowed locations
remain unimplemented. Native guards must validate ownership before dereferencing
heap storage and authenticate an active frame before accessing frame storage.
No other platform or broader managed-runtime compatibility follows from this ADR.
