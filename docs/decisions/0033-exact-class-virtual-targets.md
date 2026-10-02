# ADR 0033: Exact class virtual target resolution

Status: accepted for metadata resolution; native class integration is recorded in ADR 0034.
Recorded 2026-10-02.

## Context

The existing reachability walker retains a conservative set of possible virtual
implementations. That is useful audit evidence but is not an executable dispatch
table: a native call must select one implementation for its actual receiver.
Connecting conservative candidates directly to calls would lose slot semantics.
Separate exact class resolution from closure expansion and native emission, then
add interface mapping under its own contract.

## Decision

Add `AssemblyResolver.ResolveClassVirtualTarget` with an exact declaration
MethodDef, an explicitly resolved receiver type and an optional caller-owned
`MemberResolutionBudget`. `class-virtual-target-v1` returns the declaration,
canonical slot introduction, receiver and one effective implementation. Results
retain resolver-owned metadata identities; this is not a detached execution IR.
Reader ownership is checked before comparing equal content identities.

For this checkpoint, require public nongeneric instance virtual declarations
and concrete nongeneric class ancestors. Walk the hierarchy iteratively, then
process matching names and structural signatures from base to derived. A
`newslot` declaration starts an independent slot chain. A reuse-slot declaration
replaces the nearest matching chain, or introduces a slot if no chain exists.
Selecting an older base declaration continues using its original chain after a
derived class introduces a new slot. A nonvirtual method hiding the same name
does not override a virtual slot. Overloads with different parameter signatures
remain distinct; a matching parameter signature with a different virtual return
type is rejected pending covariance support.

These rules follow ECMA-335 II.10.3.1 and II.15.4.2.3 in the
[sixth edition](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
The resolver identifies the original slot even when the call operand names an
override instead of the slot introduction. Reject attempts to override a final
method; introducing a distinct new slot after a final method remains valid.

Reject unrelated receivers, interface/abstract/generic hierarchies, generic
enclosing owners, MethodImpl rows anywhere in the hierarchy, differing return
signatures, duplicate matching declarations, sealed ancestors and cyclic or
excessive hierarchies. MethodImpl rejection is deliberately conservative: even
a differently named body can remap a slot, so name matching cannot ignore it.
Multiple same-signature declarations are rejected even if one is nonvirtual.
Module definitions are not class receivers or ancestors.

Limit class ancestry to 256 types and declaring-owner walks to 64 levels.
Method binding, type/owner visits and method scans consume the caller's existing
metadata ledger, defaulting to 65,536 units for a standalone request. The future
compiler integration must supply its shared ledger across all dispatch pairs.
Each request recomputes its bounded result; caching can follow actual closure
integration without weakening shared accounting.

This operation neither reads/verifies IL bodies nor executes inspected code.
Successful target selection does not establish body storage support, access at
the call site, null/argument order, rooting or native execution. Reference-only
and runtime-provided method metadata can be resolved structurally; executable
translation must still apply its existing body/profile checks. The conservative
reachability API and its evidence contract are unchanged.

## Validation and review

An authored C# hierarchy supplies 41 valid receiver/declaration pairs. A test-only
.NET `callvirt` oracle agrees with a direct call to every selected body; reflected
base definitions agree with canonical slot identities. It covers multi-level and
sealed overrides, inherited implementations, new virtual slots, nonvirtual
hiding, overloads and virtual property getters. Unrelated pairs are rejected.
Only these trusted fixtures execute in the test oracle; the resolver handles
their files as data.

Raw metadata cases cover reuse-slot introduction, hidden chains, final/newslot
distinctions, duplicate/invalid/unsupported declarations, abstract/interface and
generic shapes, MethodImpl remapping, cycles, exact/excess hierarchy depth,
missing/nil tokens and invalid module ancestry. Cross-image equal names and
tokens retain distinct slot/body identities. Foreign resolver sessions fail
before equal-content comparisons. Exact, one-unit-short and exhausted shared
budgets verify bounded work and absence of partial results.

Review checked slot-chain separation, inherited slot identity, final-method
handling, duplicate declarations, reader ownership, signature matching and
iterative resource bounds. A fixture name initially collided with an older test
helper; the authored types now use distinct names. The generated native ABI
comment also now lists the already-implemented invalid-cast status 7.

Final local validation: zero build warnings/errors; all 114 focused exact-target,
conservative/constrained dispatch, scheduling and member-binding tests pass in
0.5 seconds. Native lowering has no behavioral changes in this slice, so its
full execution corpus was not rerun. Local main and cached origin/main remain
ancestors; no push, hosted execution, additional agent or service was started.

## Follow-up

Implemented by [ADR 0034](0034-native-class-virtual-dispatch.md): exact choices now
feed the object translator's fixed-point method closure and a validated detached
dispatch map. Newly discovered override bodies can allocate
new receiver classes, so closure expansion must revisit affected virtual sites.
Native lowering must preserve direct `call` semantics separately from virtual
`callvirt`, check the receiver after argument evaluation, and retain rooted
arguments/results across allocating overrides. Validate native dispatch, faults,
recursion and frame budgets at O0/O2 before claiming executable support.
Interface maps, abstract classes and explicit remapping remain separate work.
Performance tuning remains deferred under ADR 0029.
