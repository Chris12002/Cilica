# ADR 0035: Exact nongeneric interface method targets

Status: accepted for metadata resolution; interface IR/native lowering is recorded
in ADRs 0038 and 0039.
Recorded 2026-10-02.

The class resolver's blanket MethodImpl rejection described below is superseded by
[ADR 0041](0041-mixed-class-interface-dispatch.md), which shares this interface
mapping validation with class-slot resolution.

## Context

The conservative audit walker retains possible interface bodies, while the class
backend requires one exact implementation per receiver. Interface dispatch cannot
be replaced with a nearest-name class search: inherited mappings survive hiding,
and reimplementation can select a different body. Establish an exact metadata
contract before expanding interface reference flow or native execution.

## Decision

Add `AssemblyResolver.ResolveInterfaceMethodTarget` with an interface MethodDef,
resolver-owned concrete class receiver and optional shared `MemberResolutionBudget`.
`interface-method-target-v1` returns the declaration, receiver, initially mapped
class method, its canonical class slot when virtual, effective implementation and
whether the mapping was explicit. Identities include their owning input images.
Foreign resolver readers fail before equal-content comparisons.

The profile permits public nongeneric interfaces whose methods are public abstract
instance declarations, and concrete nongeneric class ancestors. Follow declared
interfaces and their transitive requirements, accepting shared diamond ancestors
and rejecting cycles. Build interface closure iteratively in parent-first order;
queue each distinct interface once. Bound the graph and each direct interface list
to 256 types, class ancestry to 256 types and declaring owners to 64 levels.
Type/owner visits, interface rows, member scans and MethodImpl bindings consume
the caller's existing metadata ledger, with 65,536 units for standalone requests.
All transitive sets fit within the bounded graph. No new configurable budget or
unbounded recursive walk is introduced.

Retain inherited mappings unless the derived class supplies an explicit mapping
or lists the contract again, including through a derived interface. At an initial
implementation/reimplementation, search the class and then its ancestors, preferring
an exact MethodImpl declaration/body mapping on each owner over a public matching
instance method. A new public method in a class that merely inherits the contract
does not replace its mapping. For a mapped virtual method, reuse the exact class
slot walk from ADR 0033 to select overrides without crossing a hidden newslot.
An inherited nonvirtual public method retains its direct body and has no class slot.

The selection follows nongeneric interface mapping/inheritance/reimplementation
rules in [C# sections 19.6.5–19.6.7](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/interfaces#1965-interface-mapping).
Explicit MethodImpl bodies are checked against
[ECMA-335 II.10.3.2 and II.12.2](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
Body/declaration signatures must match exactly; a body belongs to the mapping's
class and is either a public ordinary virtual method or a private final newslot
virtual method, covering ordinary C# explicit implementations. Reject duplicate
rows and mappings to contracts the class does not implement. Class virtual-slot
remapping remains unsupported. Validate all MethodImpl rows in the receiver chain
before permitting the shared class-slot walk to consume it.

Refactor the existing class hierarchy/slot walker internally, preserving
`ResolveClassVirtualTarget`'s public contract and its blanket MethodImpl rejection.
Only the new interface operation admits validated interface mappings and private
final slots. The conservative audit APIs retain their existing evidence contract.

Reject generic/enclosing-generic, abstract-class and interface receivers, private
contracts, default/static interface members, interface MethodImpl remapping, class
bases on interfaces, instance fields, duplicate interface members/edges, sealed
class ancestors, signature covariance and missing implementations. This is a
bounded target-selection operation, not a complete CLI verifier. It does not read
or verify IL bodies, authenticate executable access, allocate objects or invoke
inspected code. Reference-only/runtime metadata remains inspection data; native
translation must perform its existing body and storage checks separately.

## Validation and review

An independently authored C# hierarchy supplies 47 compatible receiver/declaration
pairs. Selected MethodDef identities match .NET `GetInterfaceMap`; trusted test-only
`callvirt` calls agree with direct calls to each selected body, including private
explicit methods. Reflected base definitions agree with virtual slot identities.
The oracle covers two same-signature contracts, overloads, property getters,
overrides, inherited mappings, virtual/nonvirtual hiding, reimplementation,
re-explicit implementation, inherited nonvirtual public bodies and diamonds.
Unrelated receiver/contract pairs are rejected.

Raw metadata checks selection paths and malformed/unsupported shapes, explicit
body owners/signatures, duplicate mappings/declarations/edges, generic enclosing
owners and unsupported class/interface remapping. Exact/one-unit-short/exhausted
shared budgets and exact/excess graph depth/breadth verify resource boundaries.
Equal names and tokens across images remain distinct contracts and bodies.
Foreign reader tests preserve session ownership. Compiler packages execute none
of these inspected assemblies.

Review checked inherited mapping stability, fresh reimplementation searches,
explicit precedence, class-slot reuse, MethodImpl ownership/signatures, cycle
detection, queue/set bounds and API failure paths. It added duplicate interface
member rejection and deduplicated work scheduling before queue insertion. Tests
cover both; no remaining issue was identified within this metadata profile.

Final local validation: zero build warnings/errors; all 186 targeted interface,
class, conservative/constrained dispatch, scheduling, member-binding and compiler
dispatch-map regressions pass after review. Both existing O0/O2 native class
dispatch checks also pass, including sanitizers and stack reports, in the wider
188-case run. Local main and cached origin/main remain ancestors. No push,
hosted execution, additional agent or service was started.

## Next slice

Expand detached catalogs to retain interface identities and declared edges without
assigning interface instance storage. Add validated class-to-interface and
interface-to-parent reference flow, casts/tests, declarations without IL bodies
and exact implementation maps. Wire those maps into existing fixed-point body
discovery and typed native lookup while preserving argument order, root slots,
heap frame budgets and cleanup. A selected private explicit body is reachable
through its validated interface map; it does not become publicly callable.

The current object compiler and native backend still reject interface operations.
Their class API also continues rejecting classes with MethodImpl rows; integration
must validate interface mappings before relaxing that boundary. Native O0/O2
interface differential execution, ownership, roots and resource faults are still
required. Default bodies, generics, abstract classes and class MethodImpl remapping
remain separate work. Performance tuning stays deferred under ADR 0029.
