# ADR 0167: Checked scalar borrows of value-vector elements

Accepted 2026-10-06; milestone 6c.52.

Add scalar backend `AddressArraySlot` with exact array identity, element index and
ordered component index. Descriptor component tables map each leaf to its typed
buffer and reference predicate. Validation rejects wrong carriers, scalar kinds,
component indices, forward operands, missing fault edges and readonly writes.
Logical value IR cannot use this backend operation directly.

A private location retains the array owner, exact descriptor, element and component.
Its owner gets a normal root slot. Creation checks null before bounds; later use
revalidates ownership, descriptor, buffer shape, bounds and location kind. Reference
writes retain the existing destination predicate and same-instance ownership checks.
Read-only carriers cannot be written; expired and foreign carriers fail closed.
No raw native address crosses the IR or private call boundary.

Scalar location IR v2 and native v35 record the new carrier shape. Catalog equality
also compares value-array scalar layouts so interface and executable facts cannot
silently disagree. Logical copies, CIL and library integration remain pending.

Validation: 12 focused/composed tests pass. New probes compare 48 cases against
.NET at O0/O2 with three repetitions (288 outcomes), ASan/UBSan and collection
between borrowing and loading. Native carrier checks cover Int32, Int64, references,
readonly/foreign/expired carriers, bad indices/components, null-before-bounds and
full reclamation. Existing scalar/frame borrows and value/array composition pass.
Review checked typed-buffer indexing, descriptor authenticity, reference predicates,
owner roots, fault mapping and frame cleanup.
