# ADR 0037: Detached interface declarations and dispatch plans

Status: accepted for bounded dispatch preparation; CIL call integration/native execution remain planned.
Recorded 2026-10-02.

## Context

ADR 0036 supplies interface identities/reference flow and ADR 0035 resolves exact
targets. Executable `IrMethod` requires a body, which abstract interface members
do not have. Initial interface mappings can also differ from effective virtual
overrides. Preserve these distinctions before connecting dispatch to the existing
body translator, rooting and native lookup.

## Decision

Add `ReadMethodDeclarationInput`, which snapshots a method's signature, attributes,
owner visibility/generic facts, initialization obligations and body RVA presence
without reading instructions. Signature definitions remain resolver-owned.
Share bounded provenance/declaring-owner logic with `ReadMethodInput`, preserving
the latter's IL requirement, initializer ordering and existing work accounting.
Declaration work counts the selected method, enclosing owners, initializer owner
visits and scanned member rows. Declaring depth is bounded to 64 before formatting.

Add `interface-dispatch-plan-v1` separately from executable `ObjectIrProgram`.
`ObjectInterfaceDeclaration` contains a bodyless signature with an explicit receiver.
`ObjectDispatchMethod` pairs an exact signature/name with existing class binding/slot
facts. Each target records receiver, initial mapping, effective implementation and
explicit-mapping status. Declarations are public abstract nongeneric instance
members on externally visible interfaces. Class facts represent ordinary IL-backed
instance methods, including public nonvirtual methods and private final newslots.
Int32/class/interface references and void returns use the existing IR type model.

`ObjectInterfacePlanBuilder` takes explicit resolver-owned roots/declarations and
the selected runtime library. Snapshot declaration reference types before catalog
construction. Reuse ADR 0035 for each compatible class; snapshot mapping, selected
implementation and canonical slot introduction exactly once per method identity.
Sort detached outputs by content hash/token. A single caller-owned metadata ledger
covers snapshots, catalog reads and target resolution; do not reset it per phase.
Catalog types/fields retain existing limits. Declaration/class facts share 256
method identities, 4,096 parameters per signature including the receiver, and
65,536 aggregate parameter slots. Target entries retain the 65,536 dispatch bound.
Unsupported signatures, initialization, generics, missing implementations or
resource exhaustion return actionable diagnostics without partial plans.

`ObjectInterfacePlanValidator` checks asserted ownership, signatures/flags, unique
declarations and method facts, complete compatible receiver coverage, canonical
slot introductions, nearest included overrides and final restrictions. Nonvirtual
maps retain their original body. Private bodies require explicit mapping facts and
private final newslots. A derived class that only inherits a contract must retain
the initial mapping, even when it hides a matching class method. Reimplementation
edges through derived interfaces or new explicit mapping assertions permit a new
mapping. Structural signature equality avoids display-name or textual keys.
Reject unrelated method facts. Aggregate signature limits are checked before
building larger validation indexes.

Detached assertions are not proof of the original metadata, all omitted members
or the absence of MethodImpl rows. Exact source resolution authenticates builder
choices; body translation must later check IL, access and initialization. Plans
do not allocate frames, publish roots, grant private entry access or emit calls.
The existing object-operation/native profiles and ABI remain unchanged and gated
for interface catalogs. No fake `IrMethod` body is introduced for an interface.

## Validation and review

The authored C# hierarchy agrees with .NET on 47 receiver/member and canonical-slot
choices, including inherited/reimplemented/explicit mappings, hiding, diamonds,
overloads, properties and inherited nonvirtual methods. Plan facts survive reader
disposal; the fieldless fixture also roundtrips as contract JSON. Raw metadata
checks reference/void signatures and separate equal-name/equal-token cross-image
contracts, including two contracts sharing one class body. Unsupported bodies,
default contracts, missing mappings and foreign readers fail without partial plans.
Bodyless inputs remain rejected by the body reader. Owner depth, exact/short/shared
metadata ledgers, combined method counts and aggregate signature storage have
boundary regressions. Forged declarations/maps/slot facts fail structural checks.
Reduced regressions prevent hiding from replacing an inherited mapping and a
derived override from replacing a private final mapping.

Review covers method/catalog identity, mapping versus effective-slot separation,
explicit flags/access boundaries, source ownership before cache lookup, every work
ledger, input snapshot lifetime, parameter accounting and native rejection gates.
It replaces textual signature keys with structural equality and moves aggregate
parameter checks before validation-index construction. Declaration reading asserts
RVA presence rather than claiming that uninspected instructions are valid IL.

Final validation: zero build warnings/errors; all 738 relevant local
native/object/reference-IR/declaration/type-input/C++/Int32/exact-target checks
pass in 43.0 seconds, including existing O0/O2 sanitizer, root/resource and stack
regressions. Local main and cached origin/main remain ancestors. No hosted work,
push, additional agent or service was started.

## Next slice

Connect bodyless declarations to call-signature validation and reachability without
requiring executable bodies for the declarations. Reuse exact receiver/declaration
choices while fixed-point body discovery expands the catalog. Translate selected
private bodies through their validated mapping while preserving ordinary direct
call access checks. Add typed native lookup only after executable IR is validated;
retain caller arena argument storage, argument-before-null ordering, root publication,
memory/depth faults and cleanup. Interface casts/tests and native O0/O2 differential,
root/resource/fault/stack evidence remain required. Performance tuning stays deferred
under ADR 0029.
