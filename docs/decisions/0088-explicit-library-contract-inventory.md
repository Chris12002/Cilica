# ADR 0088: Explicit library boundaries for dependency inventories

Status: accepted for metadata planning only. Recorded 2026-10-05.

## Context

Milestone 6 needs a member inventory for the real turn kernel before choosing
the first library implementation slice. Expanding Microsoft's shipped CoreLib
bodies reaches runtime internals, exceeds bounded analysis limits and does not
describe the Cilica library proposed in ADR 0003. Simply ignoring unresolved
dependencies would conceal work and incorrectly clear the filter gate.

## Decision

Add repeatable `audit --contract-only <assembly>` and optional
`contractAssemblies` arguments to the audit/reachability APIs. Each selected
image must belong to the resolver; its full assembly identity **and content
SHA-256** define the boundary. Reject the input image itself. Deduplicate and
sort selections, with at most 256 distinct images in the API and 256 selections
in the CLI. Opening a boundary does not add reference resolution authority:
dependencies still come from explicit `--reference` directories. A facade
boundary does not automatically select its forwarded implementation images.

Retain reached library declarations, constructed type/method arguments,
signatures, ancestry and generic constraints. Preserve incoming dependency
edges and IL offsets. Explicit field accesses retain their field declarations
and signatures. Defer method bodies, enumeration of layout fields, and type or
module initializers owned by selected images. Keep the existing receiver/dispatch
analysis, including application bodies reached through library interfaces.
Unselected application dependencies continue through ordinary expansion.

Reports use the separate `explicit-library-contracts-v1` profile and scope.
JSON v1 adds `contractAssemblies` (assembly IDs) and `deferredNodes` (node IDs);
text marks selected assemblies and deferred nodes. Declarations retain existing
tokens, exact instantiations, image hashes and inclusion reasons. Ordinary
`explicit-metadata-closure-v5` reports remain byte-for-byte unchanged.

With no analysis issues the status is `contract-inventory`; with issues it is
`incomplete`. Both remain implementation-incomplete. `GeneralFilterStatus` is
`observed` when scanned application/unselected bodies contain a general filter,
otherwise `unresolved`, including when no selected boundary was reached.
`ReachabilityReport.HasUnresolvedDependencies` remains true for an inventory.
CLI exit 0 means the bounded inventory completed without analysis issues, never
execution support. Existing exit 1/2/3 behavior remains unchanged.

## Consequences

This can inventory reference-only or runtime-provided library declarations
without pretending their bodies exist. It does not grant intrinsic trust,
remap framework identities, verify library behavior, discover callbacks hidden
inside deferred bodies, or establish an implementation closure. Unsupported
signatures, open generics, array helpers, variant dispatch, missing bindings and
resource limits still produce issues. Reports can contain conservative extras
and omit dependencies behind the declared boundary.

ADR 0004 and milestone 1d still require the **trial ported library's actual
implementation closure**. General filters remain unresolved until then.
Compiler/emitter profiles do not accept this inventory as executable evidence.
No Microsoft runtime binary is linked into generated programs.

## Validation and review

Independent synthetic assemblies cover exact identity/hash selection, reference
stubs and missing bodies, deferred layout/initializers, retained signature types
and closed method instances, explicit field access, application interface
dispatch, missing ancestry, explicit reference authority, foreign resolver/input
rejection, unused boundaries, budget rollback and deterministic detached reports.
Existing ordinary report snapshots, seeded corruption, reachability, dispatch,
runtime mapping and array receiver checks also pass: 203 local tests in 14.652
seconds on .NET 10.0.11. Nineteen additional CLI regression tests pass in 0.105
seconds (222 distinct checks total). Build: zero warnings/errors.

Review checked declaration binding before deferral, callback uncertainty, image
ownership, valid partial-report IDs, unchanged ordinary output and private
application evidence. No input assembly was executed. Native emission is
unchanged; no native or hosted tests were needed for this metadata slice.
