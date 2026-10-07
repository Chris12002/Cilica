# ADR AC0147: Int32 enum array interfaces

Recorded 2026-10-07 for AC7a.6.

## Evidence and decision

After committed 8ef5456, the unchanged frozen engine reaches the blanket enum
array-interface guard after 386,000 metadata units. Diagnostic-only message
instrumentation identifies IEnumerable<Alchemy.Content.PriorityWindowTiming>.
StateLookup remains eight methods/9,927 units; no engine emission/execution.

Preserve a validated nominal Enum32 identity on closed interface arguments. The
existing enum declaration facts now cover both array elements and interface
arguments, including arguments with no physical enum vector. Detached validation
binds each enum identity to its exact closed-signature key, excludes reference
and inline-value aliases, and checks consistency across all occurrences.

Int32 arrays and Int32-enum arrays implement compatible selected generic array
contracts while keeping distinct runtime descriptors. Select ordinary managed
array helpers using the requested generic element identity. Generic enumeration
therefore boxes the requested interface element; nongeneric enumeration boxes the
actual array element. Detached dispatch validation rejects helper substitutions
that would change either contract. Existing helper grants remain mandatory.

Prepare helper-required vector views only after authenticating selected array
contracts. After stack translation prunes unused fallback views, prune their
array dispatch targets and unreferenced helper facts too. Retain the ordinary
strict flow/catalog and dispatch coverage checks. Enum facts retain bounded
metadata ownership; no assembly is executed in the compiler.

The native copy-compatibility table now includes compatible Int32/Enum32 pairs.
Storage remains the existing four-byte carrier: no unsafe native pointer casts,
new reference fields or new collector operation. The generated table stays within
the existing type bound. Integer CIL v64, object IR v42, object layout v26,
interface plan v14 and native v68 record the metadata and execution extension.
No library/core changes or production/diagnostic budget increases.

## Validation and review

Twelve probes cover reads/writes, aliases through integer and different-enum
interfaces, integer-array receivers, search, copies into another enum array,
generic enum/integer boxes, actual-element nongeneric boxes, cursor states and
live mutation, bounds/null/fixed-size faults, nongeneric Clear and exact empty
cursor caches. Three formerly negative controls also execute. Together they
supply 4,410 repeated O0/O2 native/.NET comparisons with ASan/UBSan and forced GC.
Empty cursors retain exactly two cache roots for the two requested enum types.

Execution found and reduced a missing native enum copy-compatibility pair.
Compilation found stale dispatch entries after unused-vector pruning and a
mismatch between raw/closed metadata readers' optional RuntimeName fields. The
latter now compares the same nongeneric fallback spelling while still rejecting
contradictory non-null names. These paths have focused regressions.

Scalar-box confinement remains deliberately bounded: a box-returning enumerator
combined with an unproven Object-local-to-interface conversion remains diagnostic.
The positive boxing probes retain a statically known array receiver; a negative
keeps the unproven alias case. Narrow/unsigned vector storage and broader array
operations remain outside this slice. No game rule or frozen input changed.

Review checked nominal versus storage identity, generic/nongeneric boxing,
matching selected grants, metadata bounds, helper packet types, post-pruning
coverage, copy fault ordering, root counts and preservation of reference-array
covariance. The 100 selected array/copy/enumeration/confinement checks pass.
Evidence: artifacts/alchemy-enum-interfaces/regressions3.log. Frame and subprocess
limits are unchanged. Full engine/fingerprint execution and saved-game replay
remain unverified.

Final review added bounded null/default argument checks before enum classification
and catalog comparison. Catalog agreement now also compares enum declaration
facts; contradictions cannot be hidden between logical and scalar packets. Eight
malformed argument/fact mutations are checked in both stages. All 29 final
checks pass, including the native enum probes. Four public-baseline checks also
pass: 108 distinct selected checks overall, 46 public controls and one retained
gap. Evidence: final-validation2.log and baseline.log. Local main remains the
ancestor 48ecf14.
