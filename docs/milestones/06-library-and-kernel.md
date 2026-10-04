# Milestone 6: Required library subset and real turn kernel

Status: in progress; slice 6a complete. Recorded 2026-10-05.

The exit gate is equivalent deterministic command traces from the real kernel
on .NET and Cilica's native runtime. Milestone 5 supplies bounded semantic
building blocks, not the whole library or all semantics used by the kernel.
Keep application rules in the external application checkout; public regression
fixtures and examples must be independent.

## Delivery slices

| Slice | Deliverable and gate | Status |
| --- | --- | --- |
| 6a | Explicit library-contract inventory, independent tests and private kernel dependency evidence | Complete; ADR 0088 |
| 6b | Pin the reference/source profile; define exact framework-to-Cilica identity mapping, trusted intrinsic admission and source provenance | Next; no library source has been imported |
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
