# ADR 0089: Explicit library type binding foundation

Status: accepted for slice 6b.1 metadata planning. Recorded 2026-10-05.

## Context

The strict resolver intentionally distinguishes reference declarations from
implementation images, including different bytes with the same assembly identity.
Milestone 6 needs explicit correspondence to Cilica's future library. Replacing
assemblies by short name or changing the resolver globally would erase evidence
and could give application lookalikes access to runtime operations.

## Decision

Add `CreateLibraryBindingPlan` to validate caller-selected pairs of resolved
TypeDefs. A pair identifies a reference declaration and an implementation-image
definition using full assembly identity, content SHA-256 and token. Both images
must belong to the session, and neither selection can override a conflicting
reference catalogue. Different contents with the same identity are rejected.
Byte-identical duplicates are interchangeable. Repeated identical pairs collapse;
multiple destinations for one source are rejected. Multiple contract images may
map to one canonical destination.

The plan checks exact namespace and nesting/name segments, visibility,
class/interface/abstract/sealed flags, generic arity and parameter attributes.
Nested owners and immediate base signatures must correspond through explicit
mappings. Nominal/dependent generic constraints remain unsupported. Source and
destination roles prevent redirect chains and cycles: a reference-only image
cannot also supply an implementation destination. Reference markers describe
roles; they do not authenticate the publisher or grant compiler trust.

`ResolveLibraryType` performs ordinary strict TypeDef/TypeRef resolution, including
forwarders, before applying the plan. An unmapped type from a selected source
image is an error; it cannot fall back to the reference declaration. Changed
bytes for either a pinned contract or implementation identity are errors.
Unselected user types keep their original identities. Explicit image selection
does not add reference search paths, relax versions/keys/cultures, or probe the
machine's installed runtime.

`ResolveLibrarySignatureType` applies the same correspondence to structural
TypeSpecs. It retains class/value encoding, generic arguments and free variables,
array shape/bounds, modifiers, pointers, byrefs and function-pointer signatures.
This is type projection only; it does not admit those operations for execution.
Library fields, interfaces, methods, access rules and constructed-call constraints
still need their own binding and verification. The ordinary resolver, audit,
primitive mappings and compiler/emitter entry points remain unchanged.

The profile is `explicit-library-type-bindings-v1`. Plans are session-owned and
cannot be constructed by deserializing evidence. Their detached `Bindings` record
both image identities/hashes/tokens, structural names and arity, deterministically
ordered and safe to retain after disposal. Creation is atomic, with at most 1,024
input pairs including duplicates. A caller-owned metadata ledger (at most 65,536
items) bounds selection/header/projection work; type nesting is limited to 64.
Existing signature decoding limits also apply. `CIL3014` identifies invalid
binding plans/images, `CIL3015` an unmapped selected contract type; existing
resolution and work-limit diagnostics remain intact.

## Reference, source and trust baseline

[library-baseline.json](../library-baseline.json) records the inspected .NET
Standard 2.1 declaration image and local .NET 10.0.11 oracle hash. The upstream
source pin is `dotnet/runtime` commit
`79d0c463f1b55624c874a11585f7e47731e8d675`, the
[v10.0.11 tag](https://github.com/dotnet/runtime/releases/tag/v10.0.11).
The installed runtime's VMR revision is different because it identifies the
combined repository; its [source manifest](https://raw.githubusercontent.com/dotnet/dotnet/e2f47b0110ed922f21a1522da67279133ce28f32/src/source-manifest.json)
maps the runtime subtree to that same upstream commit. This is a planning record,
not an automatically loaded resolver policy or evidence that all APIs work.

No shared library source or trusted implementation artifact has been imported.
Before imports, record each upstream path/commit/hash, local path/hash,
modification and reason; retain applicable license headers and notices. Build
provenance must tie effective source inputs and compiler options to the resulting
assembly identity and hash. A source tag alone cannot authenticate a binary.

Future intrinsic admission must use an explicitly supplied, independently trusted
Cilica build manifest, matching the output hash, exact declaring type/member and
signature, intrinsic capability and compiler/runtime profile. Input attributes,
assembly names, strong-name tokens, reference markers, or a manifest supplied by
the input itself grant nothing. The manifest validator, concrete grants and
compiler integration remain separate work. Existing bounded intrinsic behavior
is unchanged; this slice introduces no new unsafe or interior-reference access.
ADR 0003 remains proposed pending trial-library implementation evidence.

## Validation and review

Twenty-two new cases cover explicit type and signature projection, forwarding,
user namesakes, closed/open generic arguments, vectors/multidimensional arrays,
required modifiers, pointers/byrefs/function pointers, nested/base correspondence,
role/header/constraint rejection, catalogue conflicts, altered source and target
bytes, duplicate/alias/conflicting mappings, ownership/disposal, detached evidence,
ordering/culture and exact/short work budgets. The focused binding/resolution/audit
run passes 173 tests in 0.917 seconds. The separate inspector correction passes
56 tests, giving 229 distinct checks this turn. Builds have zero warnings/errors.

A local metadata-only probe maps `System.Object`, `System.ValueType` and
`System.IDisposable` from the pinned reference image to independently authored
implementation-shaped declarations. All three mappings pass and ordinary
resolution remains unchanged. These declarations are not a shipped bootstrap
library and do not provide executable behavior. No input assembly was executed.

Review checked scope isolation, image hashes on both sides, structural identity,
atomic failure, resource bounds and the absence of implicit trust. It added
regressions for changed destination bytes retaining the same assembly identity,
including a changed input whose TypeRef still resolves to an approved definition.
No actionable findings remain within this type-correspondence profile. Member
binding, provenance-backed intrinsic admission, library imports and kernel/native
conformance remain subsequent gates; no hosted work was used.
