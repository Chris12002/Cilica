# ADR 0038: Interface call translation and executable body discovery

Status: Accepted for the bounded translated IR profile. Native interface
execution remains gated.

## Context

ADR 0037 prepares bodyless interface signatures and exact class mapping facts.
The object translator previously required a body for every call declaration,
which cannot represent an abstract interface entry. Treating the declaration as
a fake executable method would confuse access checks, body reachability and
root assignment. Reading every mapping/slot body's IL would also reject unused
methods whose signatures are needed only to establish a class slot.

## Decision

`object-operations-ir-v5` retains an optional interface plan alongside executable
flow and bindings. Interface `callvirt` uses a plan declaration's signature,
mandatory receiver null check and method-exit fault edge. Interface signatures
support the existing Int32/reference parameters and Int32/reference/void results.
Direct calls, allocations and default/static/generic interface semantics remain
unsupported. Argument evaluation keeps its existing IL order before invocation.

Only exact selected class implementations enter executable body discovery.
Mapping and canonical-slot declarations remain metadata facts unless an actual
call needs them. Selected private final newslots obtain their validated binding
from the interface plan, without weakening cross-owner ordinary access checks or
the conservative public class-slot resolver's MethodImpl restrictions.

A preparation session caches declarations, structural type snapshots and exact
declaration/receiver choices. As translated bodies add calls or receiver types,
the catalog and maps grow until no new body remains. The existing metadata
ledger pays for new inspection once per cached identity/choice. A small
resolver-owned TypeDef flag snapshot classifies call owners without decoding
unused static-owner fields or exposing metadata readers to the compiler.

Executable methods, interface declarations and mapping/slot facts share 256
distinct identities. Class and interface maps share 65,536 target entries. All
existing IL/IR, signature, type and field bounds remain. Empty receiver maps are
valid when no compatible class is included: a null-only call must still translate
and preserve any earlier argument fault before the null-receiver fault.

The object validator checks plan/catalog agreement, executable signature/binding
agreement, selected-body availability and exact call/map usage. Reachability
walks selected implementations while excluding bodyless declarations. Detached
assertions remain structural facts, rather than authenticated assembly metadata.

Phase A root assignment uses its existing logical reference storage for interface
parameters, locals, values and block inputs, with unchanged call/allocation
safepoints and the 1,024-root cap. `ObjectCppEmitter` explicitly rejects interface
catalogs or plans. The native profile and C ABI remain unchanged until lookup,
execution, fault/resource cleanup and root-survival checks are implemented.

## Evidence and consequences

Independently authored .NET fixtures compare translated IR against inherited,
hidden, overridden, reimplemented, explicit and diamond interface calls. Separate
contracts, interface fields, reference/void results, recursion, newly discovered
receiver types/calls and null argument-fault ordering have regressions. A base
slot with unsupported unused IL proves that metadata facts do not imply executable
body inclusion. A bounded test evaluator is test evidence, not a production
runtime or native execution claim. Forged IR, unsupported CIL, owner isolation
and method-budget regressions exercise rejection paths.
Constructor regressions permit interface calls after immediate-base initialization
and reject exposing the unfinished receiver before it.

Interface casts/tests and inferred interface joins remain later work. Mixed
ordinary class virtual calls on MethodImpl owners still encounter the existing
class resolver restriction and need separate integration. Performance tuning
remains deferred until representative applications run. No hosted validation or
additional platform support is implied by this slice.
