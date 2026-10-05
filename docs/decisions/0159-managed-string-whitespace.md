# ADR 0159: Managed string whitespace and static bodies

Status: Accepted

## Decision

Implement String.IsNullOrWhiteSpace as ordinary managed IL over the already granted
Length and UTF-16 indexing primitives. Preserve the pinned null check and scan.
Use explicit BMP White_Space ranges instead of importing the runtime-private
Unicode table machinery. Retain pinned String, Char and CharUnicodeInfo sources,
licenses and hashes. No general character classification API is claimed.

Permit ordinary static selected String IL methods in the integer-object profile.
Discovery validates their bodies, access, signatures and instructions normally;
InternalCall members retain exact grants. Detached object validation admits only
static non-initializer String bindings, preserving instance/delegate restrictions.
This is object operations IR v13; native representation and runtime are unchanged.

## Evidence and review

All 65,536 UTF-16 units are compared in 2,048 exact bit masks against .NET, alongside
49 mixed/null/empty cases at O0/O2. Native tests force collection before allocations,
use ASan/UBSan and repeat with frame and ownership checks. Three focused and twenty
composed tests pass; full solution builds cleanly. Detached mutations reject String
instance and initializer bindings. Review checked each interval boundary, surrogate,
BOM and zero-width-space exclusion, normal static-body validation and retained grants.

The concrete kernel remains blocked by the original metadata work budget after
this dependency is supplied; native transcript and implementation/filter closure
are still milestone acceptance gates.
