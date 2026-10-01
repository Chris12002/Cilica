# ADR 0009: Audits report bounded evidence and explicit uncertainty

Status: accepted for the slice 1c metadata audit. Recorded 2026-09-29.

## Context

ADR 0004's scheduling decision depends on reachable general exception filters.
Counting every filter in an assembly overstates reachability; ignoring missing
implementations understates it. The audit needs reproducible roots, binding
inputs, inclusion reasons and limitations, rather than a compatibility verdict.

## Decision

`cilica audit` requires either explicit roots or `--public-surface`. Repeatable
`--root` takes hexadecimal TypeDef, MethodDef or FieldDef tokens. Repeatable
`--root-type` takes exact input metadata names, including namespace, `/` for
nesting and backtick arity. These two explicit selectors can be combined. A type
root includes all declared members, including private ones. Public-surface
selection follows ADR 0007. There is no implicit entry-point or root discovery.
`--reference` is repeatable and uses ADR 0005's explicit directory snapshots.

The resolver collects observations during the existing bounded dependency walk,
not a second assembly-wide scan. Bodies in reference assemblies are never used
as filter evidence. The existing structural control-flow analyzer classifies
filters; its diagnostics remain in the report. Observations do not override
malformed-body, resolution or budget diagnostics.

Feature keys and evidence sources:

| Feature | Observation |
| --- | --- |
| `filter-type-test`, `filter-general` | Structurally analyzed reachable filter regions, with filter IL offset and region index |
| `unsafe` | Pointer/function-pointer/pinned signatures; memory, indirect-call and typed-reference instructions; named Unsafe/Marshal/MemoryMarshal helpers |
| `floating-point` | Single/Double signatures and named types; explicit floating-point load/store/conversion instructions |
| `decimal` | Named System.Decimal type |
| `threading` | Types in System.Threading and child namespaces |
| `async` | Task namespace types and await/async compiler-services type names |
| `reflection` | Reflection namespace, System.Type/Activator and related member names |
| `generic-virtual` | Generic method declaration with the Virtual flag |

These are review signals, not exhaustive semantic detection. Some safe managed
code produces indirect memory instructions. Task usage does not establish thread
creation. Name matching is deliberately conservative and is not framework type
authentication. Reflection-related member expansion adds `CIL3012`, since ordinary
IL dependencies cannot establish its dynamic targets. Absence of a signal is not
proof that a feature is absent. Existing dynamic dispatch limitations still apply.

The detached report records input name, sorted requested selectors, profile,
limits, all session image identities/content hashes/reference markers, graph
nodes with scoped tokens and structural instantiations, roots, edges, features
and located issues. The command's session consists of the explicit catalogue
plus input and any explicitly selected runtime-library image; the catalogue includes
forwarding images even without graph nodes.
Identical image snapshots are deduplicated. `isReferenceAssembly: false` means
no reference marker was found, not proof of a valid executable implementation.

Every external method/field node has `isExternalMember: true`; the input identity
and content hash define externality. This includes framework and application
libraries without pretending a namespace establishes framework provenance.
`firstReason` indexes an incoming edge. Following its `from` node repeatedly
reaches a selected root. Dispatch edges also identify the included receiver type.
ADR 0010 adds nullable `constraintType` evidence to declaration/dispatch edges.
Text presents the same chain as node/edge references, avoiding quadratic output
from repeating long paths for every observation. Names/control characters are
escaped. No absolute paths or timestamps are included.

The JSON contract is `cilica.audit`, format version 1; readers should ignore
unknown additive fields. ADR 0010 advances the analysis profile to
`explicit-metadata-closure-v2` for closed named constrained instance calls.
[ADR 0011](0011-explicit-runtime-type-mapping.md) advances it to
`explicit-metadata-closure-v3` for selected-library primitive/string mapping.
Additive `runtimeLibrary` and `runtimeMappings` fields identify the selected image
and the actual mapped definitions through assembly indices and scoped tokens.
[ADR 0012](0012-interleaved-reachability-scheduling.md) advances the current profile
to `explicit-metadata-closure-v4`: ordinary and dispatch work interleave, changing
evidence order and bounded partial results without changing the JSON fields.
[ADR 0013](0013-structural-array-receivers.md) advances the current profile to
`explicit-metadata-closure-v5`, adding structural `array-receiver` nodes. Their
assembly/token identifies the selected array base; instantiation records the
original array shape. Dispatch receiver IDs can identify either named type or
array-receiver nodes. Existing format-version-1 fields remain unchanged.
Successful analysis means `bounded-graph-analyzed`; any issue means `incomplete`. General-filter status is:

- `observed`: at least one general-filter observation, possibly alongside issues.
- `unresolved`: no general filter observed, but analysis has issues.
- `not-observed-in-bounded-graph`: no observation and no reported issue in this
  profile. The included named receiver universe is still not a proof about
  external/dynamic receiver inputs.

None clears ADR 0004. The trial ported-library implementation closure remains
necessary. Exit codes follow the other commands: 0 for the analyzed bounded
graph (even with feature observations), 1 for reported issues, 2 for command
usage errors and 3 for unexpected internal failures. Reports with issues still
write to stdout or `--output`, in text or JSON.

## Alternatives and consequences

A whole-assembly feature scan would lose the distinction between reachable and
unreachable filters. Treating unresolved bodies as feature-free would give false
confidence. Runtime probing would make machine installations silently change
the audit. We instead preserve incomplete graphs and explicit catalogue evidence.

The profile is useful for finding required work, not deciding executability.
Framework remapping, exact receiver closure, reflection targets, typed IL
verification and ported-library evidence remain separate work. CLI limits use
the documented defaults; library callers can provide `ReachabilityLimits`.

## Validation and review

Independent assemblies exercise reachable/unreachable filters, missing and
reference-only bodies, forwarding/external members, root selection, generic
virtuals, feature signals, reflection gaps and budget exhaustion. Tests check
CLI exit/output behavior and explicit reference directories. Reviewed text/JSON
snapshots survive resolver disposal and remain identical across paths, cultures
and root ordering. Seeded input corruption must not become an internal CLI error.
Review checked scoped identities, partial-report preservation, reader ownership,
unsupported semantics and escaped output. No input assembly is executed.
