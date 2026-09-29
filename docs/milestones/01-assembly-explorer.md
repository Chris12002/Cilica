# Milestone 1: Assembly explorer and dependency audit

Status: in progress (slices 1a and 1b complete). Plan recorded 2026-09-28.

The explorer reads .NET assemblies strictly as data and never loads or runs
them. It produces deterministic output and actionable diagnostics. It also
delivers the dependency audit that validates
[ADR 0003](../decisions/0003-class-library-strategy.md) and sizes later
milestones against Alchemy's offline assemblies.

Exit evidence (specification section 16): stable metadata, dependency and IL
output, plus diagnostics for malformed input.

## Slices

| Slice | Deliverable | Status |
| --- | --- | --- |
| 1a | Foundation and metadata inspection | Complete |
| 1b | IL decoding and validation | Complete |
| 1c | Dependency resolution and reachability audit | In progress: binding, dependency graphs and bounded dispatch implemented |
| 1d | ADR 0003 validation against a trial library build | Planned |

Each slice is reviewed and committed separately.

### 1a: Foundation and metadata inspection

- Pin the .NET SDK in `global.json` (10.0.400, patch roll-forward only).
- Create `src/Cilica.Metadata` (reader library, built on System.Reflection.Metadata),
  `src/Cilica.Cli` (the `cilica` command) and `tests/Cilica.Metadata.Tests`.
- `cilica inspect <assembly>` reports assembly identity, target framework,
  references, types, members, signatures, custom attributes, generic
  parameters and type forwarders, as text or JSON.
- Output is deterministic: entries sorted by name, no absolute paths, fixed
  line endings. Fields keep declaration order, because it determines layout.
- Malformed input produces stable diagnostics instead of crashes. Diagnostic
  identifier ranges:
  - `CIL1xxx`: input and metadata problems.
  - `CIL2xxx`: unsupported semantics.
  - `CIL3xxx`: resolution failures.
- A banned-API analyzer fails the build if compiler code calls APIs that load
  or execute assemblies, such as `Assembly.Load*` or `Activator`.

Outcome:

- `cilica inspect` reports identity, target framework, image flags, entry
  point, attributes, references, exported types (including nested forwarders)
  and every type's kind, visibility, flags, layout, base type, interfaces,
  generic parameters, fields (with literal values and explicit offsets),
  methods (with parameter names, defaults, calling conventions, method
  implementation flags and platform-invoke targets), properties, events and
  explicit overrides. Text and JSON (`cilica.inspect` format version 1) are
  supported.
- Malformed input produces `CIL1001` to `CIL1008` diagnostics with a
  placeholder in place of the damaged entity. A seeded corruption test runs 500
  randomly damaged images without an exception.
- The banned-API analyzer rejects assembly loading and invocation APIs in
  compiler projects. A test checks the built binaries for references to them.
- Golden files are identical in Debug and Release builds.
- Smoke test: `Alchemy.Engine.dll` (922 types) inspects with no diagnostics in
  about one second, including process startup.

Known limitations, planned for later slices:

- Attribute arguments whose type is an enum from another assembly (for example
  `AttributeUsage`) are shown as raw blobs until slice 1c resolves references.
- Parameter attributes, such as `ParamArray`, are not reported yet.
- The module version identifier (MVID) is omitted. It changes whenever the
  compiled content changes, which would churn golden files without adding
  information.

### 1b: IL decoding and validation

- `cilica il <assembly> [--method <name>]` shows method bodies, basic blocks and
  exception regions.
- Validates branch targets and evaluation-stack consistency for the supported
  profile.
- Classifies exception filters as type-test (stage A in
  [ADR 0004](../decisions/0004-exception-handling.md)) or general.

Outcome:

- `cilica il` decodes every ECMA-335 instruction and reports locals, exception
  regions, basic blocks with successors and entry stack depths, and operands
  formatted with the caller's generic parameter names. Text and JSON
  (`cilica.il` format version 1) are supported.
- Validation covers the structural rules the compiler depends on: branch
  targets on instruction boundaries; exception regions that are well formed
  and properly nested; control entering protected blocks only at their start
  and leaving only through `leave`, `endfinally` or `endfilter`; no `ret`
  inside a region; argument, local and token operands; and evaluation-stack
  depth (underflow, joins, declared maximum, empty on entry to a protected
  block). Failures produce `CIL1010` to `CIL1014`.
- Stack checking is by depth only. Type-level verification belongs to IR
  construction in milestone 2.
- Filters are classified as `type-test` when they only test the exception
  object's type, use locals confined to the filter and its handler, and have
  no control-flow cycles; otherwise `general`.
- Evidence:
  - No diagnostics across about 67,000 methods of real compiler output,
    including System.Private.CoreLib, System.Private.Xml, System.Text.Json and
    Alchemy.Engine. System.Private.CoreLib (39,317 methods) takes about 1.2
    seconds.
  - Alchemy's three filters are all `type-test`, so ADR 0004 stage A covers
    them. System.Private.CoreLib contains 20 general and 3 type-test filters;
    slice 1c's audit determines whether any general filter is reachable.
  - Synthetic IL tests cover each rule. Corruption tests passed 50,000
    iterations each for metadata and method bodies.
- Defects found and fixed, each with a reduced regression:
  - The code size included the body header and exception table, so a region
    ending at the last IL byte was rejected (found in System.Private.CoreLib).
  - A token with its high bit set raised `InvalidCastException`.
  - System.Reflection.Metadata raised `OverflowException` for a huge stream
    count; it is now treated as malformed metadata.
  - Review follow-up: cyclic filters were incorrectly classified as
    `type-test`; classification now checks for cycles, while accepting
    acyclic backward branches.
  - Branches and exception-region boundaries could split instruction prefix
    sequences; they now use the first prefix as the instruction boundary.
  - `rethrow` outside a catch handler was accepted; validation now checks its
    innermost handler/filter context, including nested exception regions.
  - Corrupt entry-point tokens could escape metadata inspection as
    `ArgumentException`; invalid tags and method rows now produce `CIL1006`.

### 1c: Dependency resolution and reachability audit

- Resolves references only from explicitly supplied directories, such as the
  pinned reference packs, with no implicit probing. Handles facades and type
  forwarders.
- Computes reachability from chosen roots (entry types or an assembly's public
  surface), with conservative expansion of virtual and interface calls.
- `cilica audit` reports external framework members reached; feature flags
  (filters by kind, `unsafe`, threading, async, `float`/`decimal`, reflection,
  generic virtual methods); and `why-included` reachability chains.
- Run locally against Alchemy's offline assemblies.

Resolver foundation implemented in `Cilica.Metadata.Resolution`
([ADR 0005](../decisions/0005-explicit-assembly-resolution.md)):

- A disposable session snapshots explicit reference directories. Binding uses
  manifest identity and rejects conflicting content; no implicit probing,
  version substitution or framework mapping occurs.
- Type definitions/references, facades and nested/chained forwarding resolve
  with cycle/limit diagnostics. Reference markers and content hashes are
  retained for later audit evidence.
- Independent fixtures cover positive/negative binding and forwarding cases,
  including compiled C# forwarders and seeded malformed-metadata input.

Structural declaration binding implemented
([ADR 0006](../decisions/0006-structural-member-binding.md)):

- Bounded TypeSpec, method and field signatures retain structural identity.
  MemberRefs match before generic instantiation; MethodSpecs preserve caller
  variables. Generic base lookup resolves inherited declarations.
- Interface declarations and explicit implementation operands resolve
  separately. ADR 0008 adds bounded conservative dispatch expansion.
- Synthetic cases and authored C#/.NET probes cover overloads, inherited
  generics, explicit interfaces, assembly scope and malformed signatures.
- Unsupported signature/owner forms fail explicitly; successful binding does
  not establish generic constraint validity or executable compatibility.

Ordinary dependency graphs implemented
([ADR 0007](../decisions/0007-dependency-graph-foundation.md)):

- Explicit member/type and public-surface roots feed deterministic breadth-first
  expansion. Nodes retain generic instances and reasons back to roots.
- Direct calls, constructors, field/type operands, method addresses, signatures,
  locals, catch types, bases, interfaces and constraints contribute dependencies.
  Type/module initializers are conservatively included.
- Missing dependencies/bodies, reference-only implementations, open generics,
  unsupported dispatch and graph limits remain explicit issues. Tests include
  exact reachable sets, disconnected methods, cycles and malformed input.

Bounded conservative dispatch implemented
([ADR 0008](../decisions/0008-conservative-dispatch-expansion.md)):

- Revisit call-site/receiver pairs until ordinary and dispatch expansion reach
  a fixed point. Include ordinary/generic overrides and explicit/inherited
  interface bodies, preserving receiver evidence on dispatch edges.
- Respect newslot boundaries, exclude unrelated receivers and retain explicit
  issues for unsupported dispatch and receiver forms. Declaration-only abstract
  methods remain distinct from methods whose bodies are required.
- Tests include a receiver discovered inside another dispatched body, generic
  substitutions, negative slot/interface cases and authored C#/.NET evidence.

Feature reporting and `cilica audit` implemented
([ADR 0009](../decisions/0009-audit-evidence-contract.md)):

- Explicit token/type or public-surface selection, repeatable reference directories,
  deterministic text/JSON reports and existing CLI exit-code conventions.
- Located feature observations, external member inventory, scoped graph identities,
  first-reason chains, dispatch receiver evidence and catalogue content hashes.
- Missing bodies, reference stubs, dynamic reflection and exhausted budgets remain
  incomplete. General filters can be observed without claiming closure completeness.
- Independent fixtures cover reachable/unreachable filters, every feature category,
  CLI contracts, report determinism, partial results and seeded malformed input.

Initial local Alchemy evidence (2026-09-29; private reports stay in `artifacts/`):

| Explicit catalogue, public-surface roots | Nodes / edges | Filter observations | Outcome |
| --- | --- | --- | --- |
| Application dependencies + .NET Standard 2.1 reference pack | 9,562 / 50,000 | 3 type-test; no general observed | Incomplete: edge limit, reference bodies and dispatch gaps |
| Application dependencies + .NET 10.0.11 implementation images, after ref-field fix | 10,000 / 42,458 | 3 type-test; no general observed | Incomplete: node limit, native/runtime bodies and dispatch gaps |

Both runs selected 2,268 public-surface roots. Neither establishes that a general
filter is absent; `generalFilterStatus` is `unresolved`. A Microsoft runtime
catalogue is diagnostic input, not the proposed Cilica ported library. Repeated
no-target issues include dispatch sites left unprocessed when the global budget
stops expansion; they are not counts of distinct runtime defects.

The implementation run exposed a decoder defect: modern managed-reference fields
in Span/ReadOnlySpan were rejected as invalid signatures. The bounded field parser
now preserves these byref forms; reduced and authored C# tests cover binding and
aliasing. All six observed signature errors disappear on the targeted rerun.
Owner/lifetime/layout verification is still future work.

Closed named constrained instance-call expansion is now implemented
([ADR 0010](../decisions/0010-constrained-instance-dispatch.md)):

- Bind prefixes in the caller's generic context and restrict candidate receivers
  to the selected constraint. Retain constraint and receiver IDs on audit edges.
- Sealed constraints check exactly one receiver; reference constraints retain
  included subclasses, including types discovered by later dispatch expansion.
- Invalid or unresolved prefixes never fall back to ordinary dispatch. Static
  interface, primitive, array and open-generic constraints retain explicit gaps.
- Reduced fixtures and an authored C#/.NET probe cover value mutation, reference
  overrides, generic and explicit implementations, unsupported forms and bounds.

The v2 local implementation-image rerun retains 10,000 nodes / 42,458 edges
and three type-test filters. It records 87 dispatch declarations with resolved
named constraints and 4,738 issues, but **no constrained target expansion**:
ordinary discovery still reaches the node limit before the dispatch phase.
This is better constraint evidence, not a more complete filter closure. Keep
that phase starvation visible when assessing the default budgets; fewer repeated
diagnostics is not proof of additional reachable bodies. The filter gate remains
`unresolved`.

Explicit primitive runtime mapping and string receiver accounting are implemented
([ADR 0011](../decisions/0011-explicit-runtime-type-mapping.md)):

- Select one library image through `--runtime-library`, with strict catalogue
  identity checks and ordinary type forwarding. Never discover the host runtime.
- Resolve primitive constraints and string literals/signatures to named receiver
  definitions. Reports retain the selected image, actual mapping tokens and hashes.
- Preserve array, reference-body and mapping failures as issues. No execution
  layout, boxing transformation or framework retargeting is inferred.
- Independent fixtures cover mapped targets, string sources, conflicts, forwarding,
  ownership and report contracts. A compiled .NET probe validates constraint and
  declaration evidence while preserving its real-library budget limit.

The v3 local implementation-image audit selected 2,294 roots, recorded six runtime
mappings, 10,000 nodes / 44,288 edges, three type-test filters and 4,755 issues.
It still reaches the node limit before any conservative dispatch target expansion;
`generalFilterStatus` remains `unresolved`. The five application images changed
since the v2 baseline, so these counts are not a controlled before/after comparison.
The private run manifest records the exact input and compiler hashes.

Next implementation block: schedule ordinary dependency discovery and dispatch
fairly within existing budgets, with reduced regression graphs. Then define array
receiver/interface handling. Keep the public-surface baseline, and record any
narrower export-root contract explicitly.
Slice 1c remains incomplete while required dispatch/receiver dependencies are
unresolved. The [test strategy](../test-strategy.md#slice-1c-dependency-resolution-and-reachability)
defines incomplete-closure reporting. Slice 1d must still audit the trial ported
library's implementation closure before the general-filter gate can be settled.

### 1d: ADR 0003 validation

A trial Roslyn build of ported dotnet/runtime shared sources, audited against
Alchemy's reachable closure. It reports internal calls, intrinsics, `Unsafe`
operations, interior references and generic instantiations the closure needs.

## Testing

The repository-wide [test strategy](../test-strategy.md) defines the evidence
required at each layer and the local execution/resource policy.

- xUnit v3 tests, with golden files maintained by a small in-repository helper.
- C# fixture assemblies compiled during the test build. Fixtures are
  independent of Alchemy.
- Malformed-input fixtures derived from valid assemblies by targeted
  corruption.
- Structural rules have paired accepted/rejected examples and interaction
  cases (for example, prefix sequences at exception boundaries and nested
  handlers). Corruption tests cover robustness, not proof of IL validity.
- Determinism tests: repeated runs, and runs from different paths, produce
  byte-identical output.
- Local commands only; no hosted CI.

## Private data

Audit reports about Alchemy are written to the ignored `artifacts/` directory
and never committed. Cilica is intended to be open source; Alchemy is private.
