# ADR 0022: Detached object layouts and reference storage

Status: accepted for the bounded logical layout model; native objects and rooting remain planned. Recorded 2026-10-01.

## Context

The Int32 compiler now has complete call closures, explicit arithmetic faults
and reproducible native execution. Objects introduce identity, storage and
lifetime obligations that must be represented before allocation is lowered.
Reusing integer storage for references would hide those obligations. Metadata
handles and formatted names alone cannot identify types across images.

## Decision

1. `ReadTypeInput` snapshots bounded structural metadata through the existing
   resolver. Named signatures remain session-owned inputs, just as in method
   input. `ObjectModelBuilder` converts them into a detached, immutable graph
   before the resolver is disposed. Parsing never loads or invokes an assembly.

2. Type identity is image SHA-256 plus TypeDef token. Field identity also records
   its declaring type and FieldDef token; FieldDef tokens must be unique within
   each image. The reserved module TypeDef is not an instance class. Names and
   assembly descriptions are diagnostic information.
   Forwarders use the resolved definition's identity, and namesakes remain distinct.

3. The `object-layout-v1` graph starts at one explicit type and includes the
   selected object base and every class referenced by its fields. Select a runtime
   library explicitly through the existing `ResolveRuntimeType` mapping; no host
   runtime probing or name-only substitute supplies `System.Object`. The selected
   base must be fieldless, unsealed, nongeneric, concrete and have no base.

4. The initial model accepts concrete, nongeneric automatic-layout classes
   deriving directly from that base. Int32 fields and named CLASS-reference
   fields have distinct storage kinds; primitive OBJECT maps to the selected
   base. Reference cycles are valid and traversal is iterative. Readonly and
   access flags are retained; a layout does not grant access or allow writes.

5. Fields are ordered logical slots, with a reference-slot map derived from their
   storage kinds. This is not a byte layout: pointer width, padding, native
   alignment, object headers and offsets must be established by target C++
   lowering and checked on each target. Do not infer them from the compiler host.

6. Unsupported shapes fail with diagnostics and no partial graph: reference
   assemblies, generic owners, interfaces, additional inheritance, required
   initialization, explicit/sequential/native layouts, static/literal/constant/
   RVA/marshalled/explicit-offset storage and unsupported field signatures.
   Arrays, primitive strings, value types, modifiers, pointers and byrefs require
   later profiles. This graph describes fields, not executable method bodies.

7. Bound the complete graph to 256 types (including the selected base), 4,096
   fields and 65,536 inspected metadata members. Owner chains have a hard ceiling
   of 64; caller limits may tighten it. Field signatures share the decoder's
   4,096-node budget per snapshot; base decoding has a separate budget. Existing
   signature nesting and resolver limits still apply. Caller-constructed graphs
   are validated for identities, closure, ownership, storage and aggregate limits.

## Ownership and rooting contract for subsequent slices

These requirements are not implemented runtime capabilities:

- A runtime instance owns its heap; immutable compiled type descriptors may be
  shared. References cannot cross instances. Objects retain identity until
  reclaimed or their owning instance is destroyed. Raw references do not escape
  through the initial native entry; its result remains Int32.
- An explicit thread context identifies its runtime, pending fault/exception
  and shadow-stack top. Object headers reserve a synchronization word. Static
  initialization and host handles remain separately gated.
- Reference-typed arguments, locals, temporaries and block inputs use explicit
  root slots before any allocating call. The first root-assignment pass follows
  ADR 0002 phase A; every exit, including fault exits, removes its frame. A
  returned reference must reach the caller's rooted slot before another safepoint.
- Allocation, possibly allocating calls and heap-reference stores are explicit
  operations in the later typed IR. Allocation observes only an already-published
  root frame. Field reference maps describe heap edges; they do not substitute
  for method root frames.
- Until collection arrives in milestone 4, any experimental instance heap needs
  bounded allocation, explicit failure and teardown. Teardown is not proof of
  garbage collection. Root-frame and ownership tests must precede claims about
  reference execution; GC stress and tracing tests remain subsequent gates.

ADRs 0001 and 0002 stay proposed: their full runtime/target experiments have not
run. The primitive execution IR, emitter, profile and status ABI are unchanged.

## Alternatives considered

Extending `IrType.Int32` with a bare pointer tag would lose the class identity
needed for typed joins and field access. Instead this slice establishes a typed
field contract; a subsequent slice must integrate references into executable IR
with validation before the emitter can accept them.

Hardcoding ARM64 byte offsets would bind portable descriptors to the development
host. Logical slots leave target offsets to the native compiler. Implementing
allocation immediately would also hide unproven root/lifetime assumptions.

## Validation

Independent metadata fixtures cover detached lifetime, field order/readonly
attributes, primitive object mapping, self/mutual cycles, cross-assembly
namesakes, forwarding, unsupported signatures/layouts and inclusion diagnostics.
Exact and excess type/field/member/depth limits pass. Reduced cases cover a base
signature consuming the last field-signature budget, generic enclosing owners,
foreign-reader identity deduplication and duplicate image-scoped field tokens.
The reserved module token cannot be forged into an instance-class descriptor.
Forged graphs with missing targets, wrong owners, unknown storage, invalid tokens,
duplicate/unrelated types and invalid base shape fail validation. The existing
native Int32 corpus remains the execution evidence; object execution is untested.
