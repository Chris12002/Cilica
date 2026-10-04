# ADR 0060: Private native scalar location carriers

Status: Accepted for private runtime helpers. Recorded 2026-10-04.

## Context

ADR 0059 establishes detached borrows but cannot authenticate a native storage
address. Before emitting those operations, the runtime needs checked access to
live frame slots and owned object fields. Collection must keep the original
backing reference slot or heap owner alive.

## Decision

Native v15 introduces private scalar `Location` carriers. Frame locations name a
registration token, storage kind and slot index. Heap locations contain their
owner, exact declaring descriptor and inherited field index. Neither carrier
stores an interior pointer. Resolution checks lifetime, bounds and storage kind
before obtaining a typed address. These helpers allocate no managed storage and
preserve the pending fault and its origin; callers receive a separate status.

`BorrowScope` registers an already-published root frame and its integer storage
and reference-type predicates. Registration is separate from `RootScope`, so
ordinary methods do not consume tokens. Tokens are unique within the generated
closure across contexts and instances; issuance permanently stops at exhaustion.
Resolution searches only the current context's bounded active registrations.
Popping a root frame retires its registrations, including one whose native scope
outlives that root frame. Reusing a frame address cannot revive an old token.
Generated native scopes must nest and unwind in order. Registration tables, native
slot extents and predicates are trusted runtime storage, not a native-memory sandbox.

Registration supports at most 64 active frames, 1,024 reference slots and 65,536
integer slots per frame. These are defensive carrier bounds; the compiler's tighter
aggregate work, root and execution-byte limits still apply. Registration records
are small native stack objects; backing slots live in their existing storage.
Small non-inlined validation helpers retain the local 2 KiB stack-warning gate
under O0/O2 sanitizers.

Class descriptors now carry immutable per-field kind, readonly and reference
acceptance metadata, including inherited fields. Heap access validates ownership
before reading the header, exact descriptor membership, ancestry and field bounds.
Readonly fields reject writes even if a carrier's readonly view flag is cleared.
Frame readonly lending remains a validated compiler restriction represented by
the carrier's flag. Reference loads/stores reject foreign, unknown or incompatible
values before dereference or mutation. Failed loads preserve their output.
All operations reject collector-callback reentry while marking.

A heap carrier's owner must be separately published as a precise root across
allocation. A frame carrier borrows reference slots already rooted by the lending
frame. Carriers do not independently retain objects, and cannot be kept after their
owner root is dropped. In particular, they provide no stale heap-address generation
guarantee after collection/reuse. Retained host handles remain a separate mechanism.

## Validation and limits

Local O0/O2 ASan/UBSan probes exercise nested borrowing, frame reuse and retirement,
foreign contexts/owners, inherited and readonly fields, wrong-kind/type accesses,
unchanged outputs/faults, exact and excess bounds, callback reentry and permanent
token exhaustion. Collection stress verifies that frame reference stores and a
rooted heap carrier keep children alive, and clearing those slots permits reclamation.

This slice does not emit managed-location IR or translate struct CIL. It supplies
no whole-struct operations, inline heap values, struct-return packets, array/boxed
locations, escaping byrefs or public native ABI. Existing emitters continue to
reject location IR until a separate lowering and native frame integration is
validated. No additional platform or broader language compatibility is claimed.
