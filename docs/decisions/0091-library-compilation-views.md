# ADR 0091: Explicit library compilation views

Status: accepted for slice 6b.2a. Recorded 2026-10-05.

## Decision

`CreateLibraryCompilationScope(plan)` creates a disposable resolver view over
the strict source session's pinned snapshots. It applies the validated type and
member correspondence to compiler resolution, including signatures, locals,
fields and closed caller environments. Compiler entry points can consume this
view through their existing resolver argument. The strict source remains usable
alongside it, with unchanged assembly identity and declaration behavior.

The source owns all images. Opening an explicit input through a view delegates
to the source; neither changes reference-directory authority. Disposing a view
does not close shared readers. Operations after source disposal fail, and views
cannot be nested or create further binding plans. These rules avoid reopening
files between planning and compilation or globally mutating resolution policy.

Member lookup retains the exact declared-owner restriction of ADR 0090, even
through the normally inheritance-aware APIs. Missing mappings, changed pinned
images and unsupported members remain errors. Mapping grants no new intrinsic,
unsafe or execution authority; translated IL still passes normal profile checks.
Build provenance and authenticated intrinsic grants remain a separate gate.

Ordinary audit/reachability entry points reject compilation views: their report
schema does not yet retain mapping evidence. Contract inspection uses the strict
source. A later explicit implementation-closure report must include bindings
before it can be used for the milestone's library/filter gate.

## Validation and review

Five new scope cases verify snapshot sharing, strict/view isolation, method,
field and local signatures, missing mappings, changed images, disposal and native
execution. A further regression verifies closed caller arguments on mapped
generic member owners. Existing generic-overload/MethodSpec tests also exercise
the view. The focused run passes 157 tests, with the subsequently added closed
caller regression passing in the 19-case member suite (158 distinct tests).

An independent authored fixture calls a reference declaration twice. The native
program includes only the mapped implementation's IL, and matches authored .NET
arithmetic at signed overflow edges under O0/O2 with UBSan. Ordinary compilation
cannot execute the reference stub. No compiler input is loaded into the test
process. The solution builds without warnings/errors.

Review covered lifetime ownership, identity isolation, generic substitution,
unsupported inheritance, reference-body fallback and report provenance. The
report review added the explicit audit guard. No actionable findings remain in
this bounded compilation-view slice. No shared library or kernel compatibility
claim follows from the small native probe; no hosted work was used.
