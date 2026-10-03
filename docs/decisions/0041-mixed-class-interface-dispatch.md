# ADR 0041: Mixed class/interface dispatch

Status: Accepted for bounded metadata resolution and retained-heap execution.
Recorded 2026-10-03.

## Context

The interface resolver validates explicit interface MethodImpl mappings, but the
class resolver rejected every hierarchy containing a MethodImpl row. Consequently,
a class could support an interface call while an ordinary public virtual call on
the same instance failed translation. Removing that rejection alone would silently
ignore class-slot remapping, which the current class-slot algorithm cannot model.

## Decision

`class-virtual-target-v2` admits interface-only MethodImpl mappings after validating
them through the same bounded interface hierarchy reader used by exact interface
resolution. The reader checks transitive contract membership, accessibility,
nongeneric abstract instance declarations, body ownership and identical signatures,
and duplicate mappings. It rejects every class declaration in a MethodImpl row,
including when mixed with valid interface rows. Mappings on ancestors are checked.
Hierarchies without MethodImpl rows retain their existing class-resolution path.

The shared reader builds its interface graph iteratively, with existing type,
owner-depth and caller-supplied metadata-work limits. Class and interface resolution
then retain separate selection rules: a class call follows its canonical class
slot; an interface call follows its mapping and effective implementation. Newslot
hiding, final overrides and explicit private interface bodies retain their existing
rules. The class API still requires a public virtual declaration; this change does
not make private explicit bodies publicly callable or support arbitrary class
MethodImpl remapping, covariance, abstract classes, generics or default interfaces.

Validation reads mapping declarations, not their IL. A class-only program can use
ordinary virtual methods while an unused explicit interface body contains unsupported
IL. That body is translated only if a reachable call actually selects it. The
existing fixed-point translator discovers class overrides, interface implementations
and newly allocated receiver types. Detached binding agreement and access checks
remain unchanged, regardless of which kind of call is discovered first.

No emitter, runtime, root assignment or ABI change is required. Existing typed
dispatch tables and arena argument packets already keep the two call paths distinct.
IR/native profile names, depth/frame/heap limits and fault propagation are unchanged.

## Validation and review

Authored .NET fixtures cover explicit mappings alongside ordinary class overrides,
newslots, final overrides, inherited and reimplemented interfaces, direct base calls,
class-first and interface-first discovery, and class-only programs. Allocating
reference/void methods discover another receiver through a selected override, then
exercise both kinds of dispatch with live caller roots.

Raw fixtures reject class mappings, mixed valid/invalid rows, duplicate mappings,
wrong body owners/signatures and unimplemented/nonpublic contracts. Exact and
one-unit-short metadata budgets verify that the extra validation uses the shared
ledger. An unused mapped body with unsupported `neg` IL remains outside executable
closure. A two-image fixture combines a base class slot and an interface declaration
from one assembly with separate class/interface implementations in another.

Native comparisons retain null-versus-argument fault order and exact origins,
unchanged packed outputs on failure, root observations during allocations,
exact/short frame budgets, every allocation-failure stage, cleanup and reuse.
Validation stays local; no hosted work, collector, performance improvement or new
platform support is implied. Reference-type inference at branch joins remains the
next class/interface integration gap.

Review completed with a warning-free solution build and 315 focused local tests
passing. The new native harness compares 184 outcomes against .NET at each of O0
and O2, with ASan/UBSan and static stack reports enforcing the existing 2 KiB frame
limit. Seventeen class target/slot comparisons also match .NET. Review covered
mapping rejection before slot selection, metadata-budget accounting, detached
cross-image identities, roots across allocations and fault cleanup. Local main
remains an ancestor; no push or hosted work ran.
