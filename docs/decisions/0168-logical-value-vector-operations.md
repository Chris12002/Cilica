# ADR 0168: Logical value-vector copies and borrows

Accepted 2026-10-06; milestone 6c.53.

Logical `LoadElement` and `StoreElement` now accept exact closed value elements;
`AddressElement` creates a synchronous value borrow. Value catalogs must contain
the array element layout. Array allocation checks the logical element's visibility.
Scalar-only object IR still rejects value element loads/stores before expansion.

Logical catalogs may omit scalar array shapes. The value layout planner validates
any supplied shape against the actual ordered leaves, and lowering always derives
the scalar catalog from that plan. Detached scalar validation still requires a
complete bounded shape. This keeps reference tracing and typed buffer selection
consistent with the original struct layout.

Lowering reads every leaf into a snapshot and writes previously evaluated source
leaves via checked scalar locations. A borrow expands into one carrier per leaf.
For empty structs, one private Int32 array component retains null/bounds checks and
an owner root even though the logical value has no fields. No managed allocation
occurs during the per-element scalar copy. Reference leaves retain normal roots.

Value array IR v2 and value location lowering v4 record this scope. CIL discovery,
array interfaces and managed collection integration remain the next step.

Validation: 34 focused/composed tests pass, including 576 repeated O0/O2 native
outcomes against .NET, ASan/UBSan and forced collection. The new probes exercise
copy independence after borrowed mutation, reference identity/self-cycles, Int64
leaves, empty elements, negative lengths, bounds and full reclamation. Contradictory
scalar shapes fail validation. Review covered empty-value checks, source snapshots,
owner/reference roots, access validation and bounded expansion.
