# ADR 0010: Constrained instance-call dependency expansion

Status: accepted for closed named constraints in slice 1c. Recorded 2026-09-29.
This extends ADR 0008's metadata analysis, not runtime execution support.
[ADR 0011](0011-explicit-runtime-type-mapping.md) subsequently adds explicitly
selected primitive mappings; primitive gaps below describe the original v2 profile.

## Context

The initial application audit encountered `constrained.` calls. Discarding the
prefix and processing the following call as ordinary virtual dispatch can add
unrelated receivers and conceal unsupported cases. Always reporting a gap also
prevents following cases whose named receiver and implementation are available.

The runtime distinguishes dereferencing a reference-type receiver, passing a
value-type receiver by reference to its implementation, and boxing when an
inherited implementation requires it. See the
[constrained. instruction semantics](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.constrained?view=net-10.0).
Modern static interface `constrained. call/ldftn` forms and default interface
implementations extend the older rules; see the
[ECMA-335 addendum](https://github.com/dotnet/runtime/blob/main/docs/design/specs/Ecma-335-Augments.md#static-interface-methods).

## Decision

- Associate each constrained prefix with the following operation in its prefix
  sequence before walking dependencies. Permit the instance `callvirt` form,
  optionally combined with `tail.`. Diagnose duplicate constraints, wrong
  following opcodes and static `callvirt` operands. Other prefix combinations
  retain an explicit profile gap. This is not full prefix/type verification.
- Decode the constraint in the caller's generic environment. Preserve its type
  dependency at the prefix offset with a `ConstrainedType` edge. Only a closed
  named type can establish a dispatch site in this profile. Open variables,
  primitives, arrays and other signature forms require additional mapping and
  remain `CIL3012` gaps. Never fall back to unconstrained dispatch when constraint
  decoding or selection fails.
- Keep the operand declaration separate, at the call offset. Dispatch declaration
  and target edges carry a nullable `constraintType` node ID; target edges also
  retain `receiverType`. Tokens and constructed type arguments remain scoped to
  their owning image. This is an additive field in `cilica.audit` format version 1.
  The analysis profile becomes `explicit-metadata-closure-v2`, distinguishing it
  from the unconstrained-only v1 profile in earlier private audit evidence.
- A sealed constraint checks exactly its named receiver. This covers normal
  metadata for concrete value types and sealed reference types without guessing
  a core-library identity from a type name. A nonsealed class constraint permits
  included receivers with that exact constructed type in their ancestry; an
  interface constraint permits included implementors through the existing bounded
  interface analysis. New receivers discovered by dispatched bodies participate
  in subsequent passes. Existing variance and remapping limitations still apply.
- Apply the existing conservative interface/virtual target rules only after the
  constraint matches. Nonvirtual instance operands retain their declared body;
  same-named virtual methods do not replace them. Base bodies may remain included
  alongside overrides, preserving a conservative body closure for inherited
  implementations. The audit does not select or lower a boxing transformation.
- A sealed named constraint already accounts for its only named receiver, so an
  unrelated array/string occurrence does not make that site's receiver universe
  incomplete. Other sites keep ADR 0008's conservative synthetic-receiver gap.
- Static interface `constrained. call/ldftn`, default interface dispatch without
  a class body, primitive/runtime mapping and array synthesis remain explicit
  gaps. A known declaration or some other successful site cannot clear them.

## Bounds and limitations

Reuse existing graph, signature, ancestry, dispatch-pair and metadata-work limits.
A sealed constraint uses its node directly and spends one receiver/site pair,
avoiding an unbudgeted whole-graph scan for every exact constraint. Nonsealed
constraints retain the existing bounded pair enumeration. The receiver universe
still excludes unknown externally supplied types.

This is body reachability, not exact runtime dispatch or full IL verification.
Metadata class/value-kind consistency, sealed inheritance validity, receiver
stack types, boxing, GC roots and alias lifetimes are not established here. In
particular a named metadata type does not authenticate a framework identity.
No compiler input is executed. ADR 0004 remains unresolved without the required
ported-library closure evidence.

## Validation and alternatives

Reduced fixtures cover exact receiver exclusion, reference subclasses discovered
late, interface constraints, generic receiver/method arguments, explicit private
implementations, inherited bodies, nonvirtual operands, one-pair bounds, malformed
prefixes, static interface gaps, primitive/array/open constraints and report
serialization. Negative cases keep partial evidence without unconstrained fallback.

An authored C# probe runs on .NET: two constrained calls mutate the same struct,
and a reference receiver chooses a derived override. Metadata analysis independently
finds those scoped bodies with constraint/receiver evidence. This demonstrates the
expected reference behavior and binding; it is not Cilica execution evidence.

Dropping the prefix was rejected because it erases selection information. Building
an execution-level boxing classifier here would require runtime type mapping and
verified layouts that the metadata frontend does not yet have. Conservative body
closure with retained constraints is the appropriate scope for this slice.
