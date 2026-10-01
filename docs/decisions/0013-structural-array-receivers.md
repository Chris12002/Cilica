# ADR 0013: Structural array receivers with selected-library inheritance

Status: accepted for bounded slice 1c analysis. Recorded 2026-10-01.

## Context

Array signatures and `newarr` expose possible runtime receivers without defining
a normal metadata class. Treating System.Array as a concrete receiver erases
shape and excludes it because the base class is abstract. Treating every array
occurrence as a gap also prevents following ordinary inherited bodies.

[System.Array documentation](https://learn.microsoft.com/en-us/dotnet/api/system.array?view=net-10.0)
describes the abstract base and the additional generic interfaces supported by
vectors. Those runtime-supplied interfaces require a separate helper contract;
ordinary interface metadata on the base cannot establish their implementations.

## Decision

Resolve System.Array through the explicitly selected runtime-library image,
using strict identities and normal type forwarding. `ResolveRuntimeArrayBase`
checks ownership and catalogue conflicts. Its definition must be abstract,
nongeneric, nonsealed and noninterface. This minimal shape check does not
validate framework authenticity, inheritance consistency, layout or execution.
Cache the resolution result within a walk, including failure.

A `ReachableEntity.ArrayReceiver` contains the original structural Vector/Array
signature and selected constructed base. Record vector versus general array,
element identity, rank, sizes and lower bounds. Nested arrays also introduce their
nested shapes. Repeated structurally identical signatures share a node. Bounds
and primitive/named spellings remain evidence distinctions; this is not canonical
runtime type identity, covariance analysis or a claim that each signature spells
a different runtime type. Open-variable shapes retain existing uncertainty instead
of being treated as concrete receivers.

Discover shapes from reached parameters, returns, fields, locals, type operands
and generic arguments, and construct a vector shape for `newarr` from its decoded
element in the caller's generic environment. Preserve the element dependencies.
Each array node adds a base-type dependency on the selected System.Array. Shapes
participate in ADR 0012's existing receiver queue and all graph/pair/work limits.
Register only nodes whose incoming edge succeeds. No catalogue-wide type scan or
receiver/site cross product is added.

Arrays use the selected base hierarchy for class dispatch and interfaces explicitly
declared by that hierarchy. Class bodies and explicit/inherited interface bodies
follow existing conservative matching rules. The receiver ID on each target edge
is the structural array node, not the abstract metadata base. Existing class
constraints exclude unrelated arrays; sealed named constraints retain their
single exact receiver. Static methods and intrinsic array operations retain their
existing separate binding rules.

If a generic interface operand or interface constraint is not established by the
base hierarchy, report a located `CIL3012` helper-contract gap for the array pair.
This check applies even when the method declaration belongs to Object or another
class and even when another named receiver supplies a target. It deliberately
retains uncertainty for unmatched generic interfaces without authenticating a
standard interface by its short name. Do not guess SZArrayHelper names, substitute
an element type to fabricate targets or treat variance as supported.

Without a selection or with an unavailable mapping, keep the existing synthetic
receiver gap at relevant sites. Selected mapping failures also preserve their
resolution diagnostics. Existing reference-only, native/runtime-body, open
generic, reflection and budget issues remain visible.

## Audit evidence

Advance the analysis profile to `explicit-metadata-closure-v5`. The existing
format-version-1 node schema gains the `array-receiver` kind. Its assembly/token
identify the selected array **base**, not a nonexistent array TypeDef. The
instantiation records `array=<structural signature>;base=<scoped definition>`.
It is not an external-member inventory entry. Actual method/field nodes retain
that inventory role. Readers must use node kind and instantiation with scoped
identity rather than assuming every type-like node is a normal TypeDef.

Detached text/JSON preserve shapes and receiver edges after session disposal.
Forwarding images and all scoped element definitions retain catalogue hash evidence.
Bounds, root selection and first-reason chains retain their existing contracts.
The profile does not establish executable array semantics or clear the filter gate.

## Boundaries and next work

Array-owned intrinsic MemberRefs, including multidimensional constructors and
Get/Set/Address, remain unsupported with explicit binding diagnostics. Direct
array constraints, runtime-provided generic interface helpers and covariance need
separate semantics. Successful inherited-body analysis cannot clear those gaps.

This completes the bounded array-analysis slice. Slice 1c and the ported-library
gate remain open. The next implementation slice should establish milestone 2's
small integer execution contract and typed intermediate representation. The
integer subset can progress while library/helper gaps remain explicit prerequisites
for later runtime/library milestones; see the
[primitive execution plan](../milestones/02-primitive-execution.md).

## Validation and review

Independent metadata fixtures cover every discovery source, inherited class
bodies, explicit nongeneric interface bodies, vector/rank/bounds/jagged shapes,
duplicate shapes, forwarding, conflict and ownership failures, reference bodies,
open environments, intrinsic owners, unrelated class constraints and edge rollback.
Generic interface gaps survive another receiver's success and generic constraints
on class operands. An authored .NET probe independently checks enumeration and
finds the selected Array.GetEnumerator implementation through an array receiver.
The input assembly is only read as data; reference behavior belongs to the authored
fixture. Text/JSON shape evidence remains usable after resolver disposal.

Review checked abstract-base handling, scoped element identity, receiver evidence,
constraints, failure preservation, limits and detached reporting. All 330 local
tests pass. The private application run reaches the node cap with 116 structural
array nodes, but no array receiver target edge before stopping. Its five application
images changed since the v4 baseline; counts are not a controlled comparison.
Three type-test filters leave the general-filter gate unresolved.
