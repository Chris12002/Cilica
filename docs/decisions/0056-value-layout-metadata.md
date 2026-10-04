# ADR 0056: Metadata discovery of inline values

Status: Accepted for metadata-backed layout catalogs. Recorded 2026-10-04.

`ValueModelBuilder.BuildCatalog` resolves the selected library's System.ValueType
through the existing explicit resolver, including forwarders. Its definition must
be a fieldless abstract nongeneric direct child of the selected Object. Structs
must derive directly from that exact identity; same-named types from another
image cannot substitute for it. Every explicit root is ownership-checked before
deduplication. No input assembly is loaded or executed.

Read sealed nongeneric structs with automatic/default sequential layout, Int32,
supported reference storage, or named inline value fields. Reference fields reuse
the existing class/interface/vector/String catalog and its intrinsic String
validation. The current reference-class builder still rejects inline value fields
inside classes. Enums, generic values, value arrays, interfaces on structs, native
packing/size/offsets, byref/pointer fields and type/module initialization require
later lowering. This operation describes storage, not permission to execute
methods, take addresses or box a value.

Cached type snapshots and selected String validation share a single 65,536-unit
metadata ledger. Values and reference layouts share the type and declared-field
bounds from ADR 0055. Validate the complete detached result before returning it;
unsupported input yields diagnostics and no partial catalog. Field errors retain
the inclusion path. Resolver errors and existing reference-layout diagnostics
keep their original identities and location evidence.

Review checked resolver ownership, canonical base identities, CLASS/VALUETYPE
signature mismatches, cumulative work/field budgets, String shell accounting and
error handling after inline-cycle detection. All 128 selected local layout,
metadata and no-assembly-execution checks pass, including authored nested structs,
runtime forwarding, same-name spoof rejection and exact/excess shared limits.
Native struct execution remains unimplemented. No hosted work or extra agents ran.
