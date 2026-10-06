# ADR AC0031: Static inline-value copies

Accepted 2026-10-06 for bounded AC5b.5.

## Decision

Admit whole ldsfld/stsfld copies of closed inline values. The logical static
FieldDef retains its value type, access flags and lexical declaration. Existing
value layout plans expand it into ordered integer/reference components, using the
same FieldDef plus a leaf component number. Empty values retain one private zero
slot so storage preparation and fault behavior are not silently erased.

Each reference component is an ordinary per-instance traced static root. RHS
values are computed and rooted before static allocation; all leaves are snapshots.
After initial table preparation, component copies introduce no allocations or user
callbacks. Existing beforefieldinit, readonly, failed-initializer and isolation
rules remain in force. ldsflda is still rejected: this slice introduces copies,
not a static borrow carrier or indirect mutation. Strict type initialization and
static structs containing otherwise unsupported layouts remain outside the profile.

Logical validation requires unexpanded identities and known non-box payloads.
Scalar validation recomputes the exact expansion from retained logical plans;
missing, reordered, retyped or reattributed components cannot enter emission.
Static and instance declarations share field-count/collision checks. Value catalog
reachability and reference catalog reachability remain separate. Layout profile is
v17; native v42's existing scalar/reference table implements the expanded storage.

## Evidence and review

Six authored copy probes, the former static-inline rejection and a failed-initializer
probe pass 2,352 repeated O0/O2 native/.NET comparisons. They cover nested wide
fields, references, closed generics, arrays, empty values, readonly initialization
and fault caching. Four native GC scenarios (two per optimization) prove retained
reference leaves, release on overwrite/clear, independent instances and references
surviving a failed initializer until explicitly cleared. Eight detached-packet
mutations and the static-address negative check pass. Ninety existing static,
initializer, value-layout and heap-value checks and all 21 public baseline checks
pass with ASan/UBSan, forced GC and unchanged frame/process budgets.

The initial run exposed a validator traversing static value identities as reference
types; its traversal now admits only reference storage. This was fixed and rerun,
not waived. Review checked snapshot order, rooting during table allocation, exact
component/declaration matching, scalar widths, empty storage, quotas and exception
handling. Evidence: artifacts/alchemy-static-values.

Frozen EngineStep now passes Rejections.MatchConcluded and reaches the existing
nonpublic generic-argument gate at Step IL_055f. StateLookup still reaches the
unchanged 256-type catalog limit. No game code changed. These are deeper compiler
probes, not full execution or saved-game equivalence. Public counts stay 18 gaps
and 29 controls.
