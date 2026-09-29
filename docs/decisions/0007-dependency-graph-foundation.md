# ADR 0007: Bounded dependency graphs with inclusion reasons

Status: accepted for ordinary dependency expansion in slice 1c. Recorded 2026-09-29.
Virtual/interface dispatch expansion and the audit command remain outstanding.

## Context

The filter audit must distinguish a discovered implementation from a declaration
whose body is unavailable. A list of resolved names cannot explain why a method
was included, which generic instance was followed or where traversal stopped.
Marking a partial result as a closed graph could incorrectly clear ADR 0004's
general-filter gate.

## Decision

- Expose `AnalyzeReachability` and `AnalyzePublicSurface` on the owning resolver.
  Explicit roots are MethodDef, FieldDef or TypeDef tokens from one owned image.
  A TypeDef root selects all its declared methods and fields, including private
  ones. Public-surface selection includes externally visible types and their
  public/protected methods and fields; it respects enclosing type visibility.
  Properties/events contribute through their accessors, not extra execution nodes.
- Represent constructed types, methods and fields as separate immutable nodes.
  Generic arguments are part of identity. Substitute the caller environment in
  TypeSpec owners and MethodSpec arguments before expanding the target body;
  declaration-signature matching still uses ADR 0006's open target context.
- Follow direct calls, constructors, field/type/token operands, method addresses,
  signatures, locals (including pinned locals), catch types, bases, interfaces
  and generic constraints. Interface relationships are metadata dependencies;
  they do not select implementing methods.
- Include all fields of included types for layout dependencies. Conservatively
  include each included type's initializer, and module initializers for assemblies
  containing included methods. This may over-include initializers that a particular
  execution would not trigger. It is not an implementation of initialization timing.
- Scan every instruction in an included method, including unreachable blocks.
  The graph is method-level and conservative, not a path-sensitive execution
  analysis. Reuse the existing IL structural validator; malformed bodies leave
  diagnostics alongside any dependencies that could still be recovered.
- Sort/deduplicate root tokens and expand breadth-first. Traverse metadata rows,
  signature children and IL offsets in their defined order. Every edge records
  its reason and optional IL offset. Every non-root node retains its first
  incoming edge, forming a deterministic explanation back to an explicit root.
  Keep additional edges, including cycles, without cycling the first reasons.
- Return issues with the affected node and IL offset, rather than discarding
  the partial graph. Empty roots, open generic environments, reference-only and
  absent/native/runtime/abstract bodies, unresolved dependencies and unsupported
  dispatch remain visible. `HasUnresolvedDependencies` means issues were found;
  its absence is not an executable-compatibility or filter-gate certificate.
- Use `CIL3012` for known incomplete analysis and `CIL3013` for graph limits.
  Preserve existing binding and IL diagnostic identifiers. A stopped expansion
  never introduces a node without its inclusion reason.

## Bounds and current limits

Default limits are 10,000 nodes, 50,000 edges, 128 instances per method definition,
1 MiB of IL per method instance and 16 MiB in aggregate. The caller can explicitly
configure positive values. Structural validation additionally caps the product
of instruction count and exception-region count (at least one) at one million.
Signature decoding/substitution retains ADR 0006's bounds. Exceeding a limit
produces an issue with the path to the stopped expansion; it never silently
truncates a supposedly complete graph.

Virtual/interface `callvirt`, `ldvirtftn`, constrained operations, indirect calls
and `jmp` are recorded as incomplete analysis. The declaration is retained where
it can be resolved. Selecting overrides, explicit interface implementations,
generic/default interface dispatch and implementations discovered after a call
site belongs to the next fixed-point expansion block.

This is an ordinary metadata dependency graph. It does not yet classify dynamic
reflection/assembly-loading patterns, generated runtime helpers or feature use,
and it does not perform access checks or full managed type verification.
Primitives do not implicitly probe an installed runtime. Profile mapping and
runtime-provided implementations remain separate work. No audit CLI or Alchemy
conclusion is established by this block.

## Validation and alternatives

Independent assembly graphs assert exact direct-call sets, exclusion of
disconnected methods, constructor/field/address edges, local/catch dependencies,
initializers, recursive calls, generic context swaps and bounded generic growth.
Tests cover public versus explicit type roots, deterministic root order and
copied inputs, incomplete dependencies, graph/body limits and malformed bodies.
Every returned node's first-reason chain is checked back to a root. An authored
C#/.NET probe also supplies real compiler output and an expected call path.

Failing the whole operation on the first missing dependency would hide useful
evidence. Continuing silently would hide uncertainty. A partial graph with
located issues preserves both. Including every method in every referenced
assembly was rejected: disconnected-method tests ensure the graph remains useful
for deciding which library code matters.
