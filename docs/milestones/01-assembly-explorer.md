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
| 1c | Dependency resolution and reachability audit | Planned |
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

Next implementation block: the resolver foundation. Define explicit directory
inputs, assembly-qualified identity and conflict diagnostics, then implement
facade/type-forwarder traversal with cycle detection. Validate it with small
independent assemblies before adding reachability. No implementation of this
block is claimed yet.

Continue 1c in reviewable commits: resolver and fixtures; root/edge expansion
and inclusion explanations; feature reporting and `cilica audit`; local Alchemy
evidence. The [test strategy](../test-strategy.md#slice-1c-dependency-resolution-and-reachability)
defines the cases and how to report an incomplete closure. Reference-only audit
results cannot clear the general-filter gate; slice 1d checks the trial ported
library's implementation closure.

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
