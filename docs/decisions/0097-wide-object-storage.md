# ADR 0097: Explicit Int64 object and frame storage

Status: accepted for slice 6c.6 authored IR. Recorded 2026-10-05.

## Decision

An explicit `ObjectTypeCatalog.Integer64` capability admits Int64 scalar types,
class fields, arithmetic/conversions, calls and synchronous borrows. Existing
catalogs default to false and reject wide fields/types. It selects scalar storage
semantics, not library intrinsic permissions or a public ABI.

Native profile v18 adds UInt64 fields and separate wide frame slots. Internal
argument/result packets have a distinct wide-bit member. Phase A root maps still
contain only references and location owners. Borrow registrations retain separate
Int32/Int64 extents under one combined 65,536-slot limit. A wide location has its
own storage kind: narrow loads/stores cannot reinterpret it, and lifetime,
readonly, ownership and bounds checks precede access.

The shared integer emitter supplies arithmetic, comparison and fault semantics.
Method returns, branch transfers, field pointers and call packets preserve width.
Value scalar lowering remaps the new scalar expressions when adjacent inline
values expand; Int64 fields inside inline/boxed values remain the next gate.
The packed external test boundary remains Int32. Internal packet sizes changed,
so this is a new private native profile, not a compatible public ABI extension.

## Validation and review

Authored IR composes wide mutable/readonly fields, cyclic references, constructor
arguments, parameter/local/field borrows, allocating callees, calls/returns,
comparisons, branches and checked/wrapping sums. O0/O2 ASan/UBSan builds run 288
repeated .NET comparisons/fault observations with collection before allocations.
All paths return execution storage and roots to their empty state; post-call
collection reclaims the objects. Native checks cover width mismatch, readonly
writes, stale frames, foreign owners, bounds, allocation failure and the combined
borrow budget. Malformed catalogs/IR fail before emission.

The first focused run passes 144 tests; the composed native follow-up passes 19,
including existing object, boxing, exception and frame probes. A separate lowering
regression checks operand remapping after struct expansion. Review checked typed
subobject pointers, reference-only tracing, private packet changes, unsigned
arithmetic, exceptional exits and combined bounds. No actionable findings remain.
No hosted work was used. Wide CIL discovery, inline/boxed payloads and arrays are
not implemented by this slice.
