# Milestone 1: Assembly explorer and dependency audit

Status: in progress. Plan recorded 2026-09-28.

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
| 1a | Foundation and metadata inspection | In progress |
| 1b | IL decoding and validation | Planned |
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
  line endings.
- Malformed input produces stable diagnostics instead of crashes. Diagnostic
  identifier ranges:
  - `CIL1xxx`: input and metadata problems.
  - `CIL2xxx`: unsupported semantics.
  - `CIL3xxx`: resolution failures.
- A banned-API analyzer fails the build if compiler code calls APIs that load
  or execute assemblies, such as `Assembly.Load*` or `Activator`.

### 1b: IL decoding and validation

- `cilica il <assembly> [--method <name>]` shows method bodies, basic blocks and
  exception regions.
- Validates branch targets and evaluation-stack consistency for the supported
  profile.
- Classifies exception filters as type-test (stage A in
  [ADR 0004](../decisions/0004-exception-handling.md)) or general.

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

### 1d: ADR 0003 validation

A trial Roslyn build of ported dotnet/runtime shared sources, audited against
Alchemy's reachable closure. It reports internal calls, intrinsics, `Unsafe`
operations, interior references and generic instantiations the closure needs.

## Testing

- xUnit v3 tests, with golden files maintained by a small in-repository helper.
- C# fixture assemblies compiled during the test build. Fixtures are
  independent of Alchemy.
- Malformed-input fixtures derived from valid assemblies by targeted
  corruption.
- Determinism tests: repeated runs, and runs from different paths, produce
  byte-identical output.
- Local commands only; no hosted CI.

## Private data

Audit reports about Alchemy are written to the ignored `artifacts/` directory
and never committed. Cilica is intended to be open source; Alchemy is private.
