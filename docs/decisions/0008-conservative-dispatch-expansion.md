# ADR 0008: Conservative dispatch over included named types

Status: accepted for the bounded dispatch profile in slice 1c. Recorded 2026-09-29.
This extends ADR 0007; it does not establish executable compatibility.

## Context

A virtual or interface operand identifies a declaration. The body executed can
belong to a receiver type discovered much later, including inside another
dispatch target. A one-pass walk can therefore omit required implementations.
Matching only method names would also confuse fresh virtual slots and renamed
explicit interface implementations.

## Decision

- Record virtual `callvirt` and `ldvirtftn` declarations separately from their
  possible implementations. Ordinary nonvirtual calls keep ordinary call edges.
  A declaration-only abstract method does not require a body. If later reached
  as a direct call or explicit root, promote it to a body requirement and retain
  the missing-body diagnostic.
- Use included, constructed, nonabstract named types as candidate receivers.
  Drain ordinary dependencies, evaluate unseen call-site/receiver pairs, then
  repeat whenever new targets add work. Types discovered by dispatched bodies
  participate in subsequent passes. Pair processing follows stable site and
  type-node order and stops at a fixed point or explicit limit.
- For ordinary class slots, retain the operand body and compatible reuse-slot
  overrides below its declaring type. A matching `newslot` declaration starts
  a separate slot; it and subsequent overrides of that new slot do not replace
  the original slot. A final slot cannot be overridden. Compare substituted
  owner signatures with method generic variables still open, so instantiation
  cannot collapse distinct generic overloads. These distinctions follow
  [ECMA-335, II.10.3](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
- Follow exact constructed interface relationships through bases and inherited
  interfaces. Match MethodImpl declarations by resolved owner and token, then
  include their separately named bodies. Carry both owner and method arguments
  into inherited and explicit implementations. Public instance methods with
  matching signatures are also retained conservatively, including inherited
  methods and their ordinary virtual overrides.
- This is an over-approximation: base bodies and public interface candidates may
  remain included even when one concrete execution would choose another body.
  It is not the runtime's exact interface-map selection algorithm. Same-named
  methods on unrelated receiver types remain excluded.
- Every `ConservativeDispatch` edge retains the caller, IL offset and receiver
  type-node ID that justified the target. `DispatchDeclaration` edges identify
  the operand separately. Existing first-reason chains still lead to roots;
  additional receiver evidence can explain why the implementation is relevant.
- Missing concrete targets and unsupported relationships remain located issues.
  Interface cycles, duplicate explicit mappings, invalid bodies and attempts to
  override final methods are diagnosed rather than silently selected.

## Bounds and limitations

The receiver universe is the graph's included named types. It is not every type
in every catalogue assembly, and is not proof that external callers, reflection
or runtime-generated code cannot introduce other receivers. Public-surface and
open generic roots retain their existing limitations. The future audit must
state its roots, receiver universe, profile and unresolved dynamic edges.

Constrained calls, indirect calls and `jmp` remain incomplete. Variant interface
conversions, default interface dispatch without a class implementation, explicit
class MethodImpl slot remapping, differing/covariant return signatures and private
newslot hiding require later profiles. Unsupported cases do not clear the audit
gate merely because some other receiver produced a target.

Arrays and strings can produce runtime receiver types without an ordinary named
type node. When reached array signatures, `newarr`, `ldstr` or string input
parameters expose that limitation, dispatch sites retain an incompleteness
issue. Primitive/runtime type mapping and a complete receiver analysis remain
separate work. This deliberately errs toward extra issues.

Existing graph/body/signature bounds still apply. The default additional bound
is 100,000 receiver/call-site pairs, configurable through `ReachabilityLimits`.
Dispatch scans also stop after one million metadata work steps. Base ancestry
uses the existing 64-level bound; interface ancestry has a 256-entry work bound
and checks cycles independently of repeated diamond paths. Bounds produce
diagnostics and preserve the partial graph.

## Validation and alternatives

Fixtures cover ordinary and generic overrides, newslot boundaries, renamed and
inherited explicit implementations, generic owner/method arguments, interface
inheritance, unrelated-method exclusion, late receiver discovery and a second
dispatch pass that reveals another body. Negative cases cover default/variant
interfaces, cycles, class remapping, synthetic receiver gaps and pair limits.
Promotion of an abstract declaration to a direct body requirement has a
regression. An authored C# fixture executes on .NET and supplies compiler output
for override and explicit-interface target checks; the resolver reads its input
as data using explicitly supplied framework images.

Including all catalogue methods would conceal missing selection rules and make
the filter audit unhelpful. Selecting a single apparent implementation would
understate uncertainty. Bounded conservative targets with receiver evidence and
explicit gaps provide useful progress without claiming exact runtime dispatch.
