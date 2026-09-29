# ADR 0006: Structural signatures and declaration binding

Status: accepted for the bounded slice 1c binding profile. Recorded 2026-09-29.
This is metadata resolution, not executable or full CLI verification support.

## Context

Reachability must identify the declaration behind each metadata operand before
following its dependencies. Formatted names lose distinctions such as assembly
scope, generic variables, array shapes and custom modifiers. Instantiating a
generic signature before matching can also collapse distinct overloads.

For example, `C<T>.M(T)` and `C<T>.M(int)` remain distinct declarations when the
owner is `C<int>`. The MemberRef retains the original member signature. This
follows [ECMA-335, II.9.8](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).

## Decision

- Use immutable structural signatures. Named types contain the resolved image
  and TypeDef handle; equality uses manifest identity, content hash and token so
  byte-identical snapshots remain equivalent across paths. Reader ownership is
  still checked separately. Type and method variables retain separate indices.
  Sequences compare their elements, rather than their backing array identity.
  Primitive codes, class/value-type encodings (unspecified for bare tokens), generic arguments, pointer/byref
  forms, vector versus multidimensional arrays and their shapes, required and
  optional modifiers, function pointers and calling conventions remain distinct.
- `ResolveSignatureType` decodes TypeSpecs and resolves TypeDef/TypeRef operands.
  `ResolveMethod` handles MethodDef, MemberRef and MethodSpec; `ResolveField`
  handles FieldDef and MemberRef. Existing `ResolveType` continues to return a
  definition rather than silently discarding TypeSpec arguments.
- Match MemberRefs in the owner's open generic context. Walk base declarations
  with the corresponding base-type substitution, diagnose multiple matches,
  and never inherit constructors. Return the definition handle, constructed
  declaring type and instantiated signature. Direct definition operands return
  their open declaring context.
- Keep substitutions simultaneous: arguments inserted for a target's `!n` or
  `!!n` are opaque caller expressions. Replacing method variables inside an
  already substituted owner would accidentally capture caller variables.
  MethodSpecs therefore instantiate the original declaration signature.
- Resolve interface declarations and the individual declaration/body operands
  in MethodImpl records. [ADR 0008](0008-conservative-dispatch-expansion.md) now
  adds bounded conservative implementation expansion. Variance, default interface
  methods and full override-map validation remain outside that profile. An
  interface declaration alone is not an execution target.
- Parse blobs with bounded recursion: 64 depth levels, 4096 decoded nodes, and
  count checks before collection construction; array rank is limited to 1–32.
  Check expanded type signatures
  against the same depth/node bounds. Base lookup permits at most 64 types and
  rejects a repeated definition, even if its arguments keep changing. These
  are implementation limits, not CLI validity claims.
- Use a small bounded blob parser over `BlobReader`. The framework signature
  decoder descends nested blob types before calling a provider, so a provider
  alone cannot enforce the host stack bound. Trailing bytes, invalid counts,
  invalid generic indices, arity mismatches and contradictory method instance
  flags are diagnosed. This does not replace later type-level verification.
- Preserve existing resolution diagnostics for missing dependencies. New
  `CIL3009`, `CIL3010` and `CIL3011` identify invalid/over-limit signatures,
  missing members and ambiguous members. Diagnostics locate the requesting
  metadata token and image; missing-member messages also identify the owner.

## Current limits

Varargs, global/module members, array intrinsic members, generic function
pointers and CLASS/VALUETYPE/modifier tokens that themselves reference TypeSpecs
produce explicit `CIL3008` failures. Pinned local signatures are outside these
public signature APIs; the graph walker now decodes pinned locals under
[ADR 0007](0007-dependency-graph-foundation.md). The profile does not normalize primitive encodings against named framework
types or implement runtime type equivalence. Framework mapping remains ADR 0003
work; modifier matching is strict and does not erase optional modifiers.

Free variables in TypeSpecs and MethodSpec arguments remain in the caller's
context. The graph walker now supplies its enclosing generic environment when
expanding these results, and flags remaining open variables. Access checks, generic constraints,
full class/value-type validation and managed stack typing are not established by
successful declaration binding. Reference-assembly methods remain declarations,
not evidence of implementation bodies.

## Validation and alternatives

Independent metadata graphs exercise generic overload collapse, simultaneous
substitution, inherited methods and fields, ambiguity, base cycles, distinct
assembly scopes, signature distinctions, malformed blobs and nesting limits.
Small authored C# cases execute on .NET and provide compiler-produced tokens for
overload, inherited generic and explicit-interface declaration/body checks.
Seeded signature corruption tests exercise diagnostic containment.

String-based comparison and substitute-first lookup were rejected because they
can select the wrong declaration. Reusing reflection loading was rejected
because assembly inputs must remain data. Runtime dispatch is deliberately a
separate operation: folding it into declaration lookup would conceal incomplete
call graphs. This block does not add `cilica audit` or resolve the general-filter
gate for Alchemy.
