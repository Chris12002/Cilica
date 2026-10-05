# ADR 0166: Closed value-vector scalar storage

Accepted 2026-10-06; milestone 6c.51.

The concrete kernel adapter constructs `PlayerId[]`. Its elements must remain
inline values, with copies and borrows, rather than become object references.
This first slice supplies the detached storage contract and private runtime.

Add a Value array element identity containing the exact closed nominal type.
A separate bounded catalog fact lists its ordered scalar leaves. Validate one
nonempty shape per value element, known reference leaves, distinct reference/value
identities and at most 4,096 aggregate leaves. Empty values will use one private
Int32 slot when logical lowering is added. Value arrays are invariant; existing
reference covariance and generic reference-array conversions do not apply.

The runtime uses independently typed Int32, Int64 and reference buffers. This
preserves C++ scalar object provenance for future borrows. Allocation checks
length and byte multiplication, charges all buffers plus the header, zeroes every
slot and publishes only fully allocated objects. Failure cleans up partial buffers
before collection/retry. Dynamic tracing visits all reference slots across all
elements. Array shape/charged bytes are checked before traversal. Existing
per-instance empty-array caching works by exact descriptor identity.

Object layout v12 and native v34 record the storage change. CIL discovery and
value element read/write/borrow lowering remain separate slices. Existing scalar
LoadElement/StoreElement reject this new storage kind. No public layout or ABI is
promised.

Validation: detached malformed-shape and covariance rejection; native O0/O2
ASan/UBSan checks for zeroing, exact/short heap quotas, negative lengths, forced
collection with every reference slot populated, malformed length rejection,
empty identity and full reclamation. Existing array conformance is also checked.
Review covered overflow, partial allocation cleanup, collector ownership, typed
buffers and catalog closure traversal.
