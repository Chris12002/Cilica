# ADR AC0009: Concrete list mutation and snapshots

Accepted 2026-10-06 for bounded AC3.1.

## Decision

Implement List<T>.Insert, RemoveAt, Clear and ToArray in the owned managed library.
Reuse existing capacity growth and typed element loops. Insert checks its inclusive
upper bound before growing, shifts backwards and updates size/version after the
store. RemoveAt checks its exclusive upper bound, shifts forwards, clears the
vacated slot and then updates version. Clear increments version, resets logical
size and clears the old live range. Clearing every admitted element shape avoids
a new reference-containing-type intrinsic, at the cost of also zeroing primitives.
Capacity stays unchanged by removal and clearing. Existing compiler/runtime code
handles Int32, reference and supported inline-value element copies and roots.

ToArray copies only live elements into a new array when nonempty. Value elements
are copied; embedded references retain identity. The pinned List implementation
returns a shared empty array per closed T, distinct from Array.Empty<T>(). Preserve
that public identity with a private lazily populated generic static reference,
using existing instance-owned, traced and quota-charged static storage. Publish
only after allocation succeeds. Private backing storage continues to use the
existing Array.Empty<T> path; its identity is not exposed by this surface.
The lazy cache assumes the existing serialized runtime invocation model.

No compiler/runtime profile, intrinsic request or embedding ABI changes. The
owned library bytes and approved build manifest change. Version updates follow
the pinned algorithm even though enumeration remains a later slice.

## Boundaries discovered by probing

This section records the AC3.1 checkpoint. AC3.2 subsequently resolves the
interface-helper blocker and advances both rejection probes to native validation;
see [ADR AC0010](AC0010-array-interface-mutations.md).

Concrete List methods work. Calling mutation through IList<T>/ICollection<T> in
these closures still reaches the existing array-interface discovery gate: arrays
also implement those contracts, and Insert/RemoveAt/Clear lack admitted array
helpers. ReadOnlyCollection mutation compositions encounter the same gate. Keep
two authored fixtures as precise CIL2001 rejection tests; do not weaken the gate
or claim these interface paths execute. Live read-only views of concrete list
mutations remain supported.

Follow-up AC3 work must supply exact array mutation helper contracts/semantics or
prove a narrower receiver closure, then re-probe these interface cases. CopyTo
requires its own covariance and partial-copy fault review. Equality-based search/
removal, bulk operations, Dictionary/HashSet additions and AC4 enumeration remain
outside this slice.

## Evidence and review

Eleven new authored probes plus public ListRemoveAt and ListToArray provide 3,822
repeated .NET/native comparisons on macOS ARM64 at O0/O2 with ASan/UBSan, forced
collection, frame budgets and cleanup assertions. Coverage includes boundary and
middle shifting, growth, retained capacity, live views, repeated reuse, exact
bounds messages, argument-before-null fault order, snapshot independence, shared
references, inline values and empty identity across closed reference types.

Four dedicated native GC checks prove that RemoveAt and Clear release one object
at a time while the list remains rooted, for both reference elements and references
inside struct elements. The GC harness now prepares exceptions before adding its
manual root; otherwise its nonzero depth skips normal entry preparation. The
initial cleanup assertion caught this harness omission. Production code did not
change to accommodate the test. 61 existing regression checks also pass; the two
advanced public probes are included in that baseline. The ledger has 33 gaps and
14 native controls. No runtime limits or timeouts were increased.

Review checked overlap direction, validation and version order, allocation before
publication, removal of reference slots, value-copy identity and the empty-cache
root lifetime. Historical private observations are retained; no private replay
root was rerun. Full AC3 and saved-game replay remain incomplete. No hosted work,
services or additional agents were used.
