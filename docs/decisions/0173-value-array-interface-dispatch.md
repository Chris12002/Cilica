# ADR 0173: Invariant value-array interface dispatch

Accepted 2026-10-06; milestone 6c.58.

The kernel passes PlayerId[] as IReadOnlyList<PlayerId>. Record the exact nominal
or closed value identity alongside each closed interface argument. Value arrays
match that identity exactly; reference variance does not apply. Validate argument
identity consistency, reference/value separation and inclusion in logical value
catalogs. Object layout v13 records the added facts.

Use existing selected ArrayInterface<T> helper declarations and exact intrinsic
grants. Their ordinary managed IL performs bounds checks and typed element access.
After scalar lowering, derive helper parameters and return destinations from the
value-array leaf shape. An explicit Empty fact distinguishes the private dummy
Int32 array slot from logical zero-field values; logical layout validation checks
it, scalar validation requires the single dummy slot, and catalog equality retains
it. Do not add native interface shortcuts or trust arbitrary lowered signatures.

Review found redundant fixed-point transitive closure work in the existing deep
interface-chain test. Skip that pass when variance/array processing admits no new
edge; nominal ancestors are already closed. Keep the existing work budget.

Four differential probes exercise nested structs, wide/reference leaves, copy
independence, forced GC, invariant casts, empty elements, write faults and List
read-only views at O0/O2 with ASan/UBSan (1,176 repeated outcomes). Detached helper
packet/empty-shape mutations are rejected. The real kernel advances to switch in
Step. General enumeration, primitive-width/enum array equivalence, public ABI and
untested platform compatibility are not admitted by this decision.
