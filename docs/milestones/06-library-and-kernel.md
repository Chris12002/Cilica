# Milestone 6: Required library subset and real turn kernel

Status: in progress; slices 6a, 6b.1 and 6b.2 complete. Recorded 2026-10-05.

The exit gate is equivalent deterministic command traces from the real kernel
on .NET and Cilica's native runtime. Milestone 5 supplies bounded semantic
building blocks, not the whole library or all semantics used by the kernel.
Keep application rules in the external application checkout; public regression
fixtures and examples must be independent.

## Delivery slices

| Slice | Deliverable and gate | Status |
| --- | --- | --- |
| 6a | Explicit library-contract inventory, independent tests and private kernel dependency evidence | Complete; ADR 0088 |
| 6b.1 | Pin the reference/source baseline and validate explicit type correspondence | Complete for metadata plans; ADR 0089; no library source imported |
| 6b.2 | Apply the plan to structural member binding and preserve source/implementation evidence | Complete for metadata plans; ADR 0090; ordinary compiler binding is unchanged |
| 6b.3 | Validate build provenance and exact intrinsic grants at the compiler boundary | Planned; mapping plans and input names/attributes grant no new privileges |
| 6c | Extend compiler semantics needed by the first concrete kernel path: scalar/enum cases, values stored in classes, value instance calls and closed generic interface/instance calls | Planned; split into individually reviewed and differentially tested slices |
| 6d | Implement the required library members, beginning with list/read-only collection operations and their runtime prerequisites | Planned; expand to equality, dictionaries/sets, strings and formatting only as concrete roots require |
| 6e | Compile the unmodified kernel with a concrete adapter and run bounded deterministic command transcripts against .NET | Planned; compare outputs, state, ordering and failures |
| 6f | Audit the trial ported implementation closure, resolve the general-filter gate, run composed collection/exception stress and complete the milestone review | Planned; must precede milestone completion |

These slices are capability gates, not time estimates. Detailed requirements can
change when a concrete adapter supplies generic arguments and implementations.
Framework identity rules precede library imports; implementation-closure auditing
starts with the first trial library build, not only at the final review.
If required ported code reaches general filters, ADR 0004 stage B moves forward
before this milestone can close.

## Slice 6a outcome

`cilica audit --contract-only <assembly>` inventories application code while
deferring explicitly selected library bodies, layout fields and initializers.
It records exact identities/hashes, declarations, signatures, instantiations and
inclusion reasons. Library ancestry and direct application dispatch remain
visible. The report uses `explicit-library-contracts-v1`; no-issue status is
`contract-inventory`, never an implementation or compatibility verdict.
General-filter absence stays unresolved across library boundaries.

The existing external Release assembly snapshot produced the following private
inventories with the same default budgets and an explicit .NET 10.0.11 CoreLib
boundary. The source checkout revision and image hashes are recorded separately
in ignored artifacts; this checkpoint did not rebuild the application or claim
that its existing binaries matched that revision.

| Root selection | Nodes | Edges | Deferred nodes | Analysis issues | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Kernel public surface | 732 | 3,808 | 302 | 579 | Incomplete: open generic environments, array helpers, interface/variance and dynamic-target gaps |
| Priority-state types | 153 | 777 | 96 | 0 | Bounded contract inventory; implementation and filter closure unresolved |

Neither inventory hit the node/edge limits. The smaller selection points to
list/read-only collection members, value IDs, generic interfaces and canonical
output dependencies. It is a conservative planning input, not a minimum member
set or an executable kernel trace. Private reports and reproduction notes live
under ignored `artifacts/`; no application source or member inventory is copied
into the public repository.

Fifteen independent boundary tests and related metadata/audit regressions pass
in a 203-check focused local run; 19 additional CLI checks also pass (222 total).
Review covered exact image admission, retained
application calls/signatures, deferred implementation uncertainty, partial-report
bounds and unchanged ordinary reports. No actionable findings remain in this
slice. No hosted CI, service, push or additional agent was used.

## Reference-profile inspection correction

During slice 6b reference-profile inspection, the inspector incorrectly decoded
locally defined `System.Type` attribute arguments as enums and printed null type
arguments as `typeof()`. A reduced fixture reproduced both defects. The correction
recognizes top-level local `System.Type` for attribute decoding and preserves null
type values, without granting compiler trust. Five regressions and the existing
inspection/CLI/corruption/no-execution checks pass (56 tests). The pinned .NET
Standard 2.1 reference image now inspects with zero diagnostics, down from 107
incorrect malformed-attribute reports. This changes inspection only.

## Slice 6b.1 outcome

The compiler-library metadata API now validates an explicit reference-to-
implementation type plan and projects TypeDef/TypeRef/TypeSpec identities through
it. It retains exact image hashes, names/nesting, generic arguments and structural
signature wrappers; unmapped selected types, changed image bytes and incompatible
headers/base types are diagnostic. Ordinary resolution and compiler behavior
remain unchanged. The plan grants no intrinsic permissions.

The [baseline record](../library-baseline.json) pins .NET Standard 2.1 declarations
and the .NET 10.0.11 source/oracle relationship. Imported-source and trusted-artifact
lists are empty. Twenty-two independent plan cases pass in a 173-test focused
binding/resolution/audit run; combined with the inspection correction, 229 distinct
checks pass. A local metadata-only probe also maps three real reference types to
independent implementation-shaped declarations. Review added changed-target-hash
coverage. No native output changed or hosted work ran.

See [ADR 0089](../decisions/0089-library-type-binding-foundation.md).

## Slice 6b.2 outcome

Explicit method/field binding now resolves the reference declaration first, maps
its owner and structural signature, and finds one compatible implementation.
Generic overloads are matched before substitution. Results retain original and
implementation identities and constructed signatures; missing or ambiguous
members, changed access/storage/dispatch flags and changed literal values fail.
Ordinary compiler binding remains unchanged; no intrinsic permission is granted.
Eighteen new cases and related regressions pass in a 112-test focused local run.
The slice review found no remaining actionable issue. See
[ADR 0090](../decisions/0090-library-member-binding.md). Compiler integration,
provenance validation and actual library behavior remain required next gates.

## Remaining completion evidence

- Pin imported source commits, licenses, local patches, reference identities
  and oracle runtime; never trust a library solely by namespace or type name.
- Differentially test each admitted member and semantic extension, including
  mutation, exception order, collection across allocations and ownership.
- Supply concrete exported roots and a real adapter so open public-surface
  declarations are not mistaken for an executable closure.
- Match repeated command transcripts, rejected commands, deterministic ordering
  and canonical state output on .NET and local native O0/O2 sanitizer builds.
- Audit the required ported-library implementations and resolve every gap needed
  by those roots, including the general-filter decision.
- Keep unsupported members actionable, update compatibility claims and review
  composed behavior before marking milestone 6 complete. Public embedding,
  mobile and browser evidence belong to later milestones.
