# ADR 0224: Direct rooted substring construction

Status: accepted. Recorded 2026-10-20.

## Decision

Owned String.CopySubstring calls the exact private
StringConstruction.FromSlice(String,Int32,Int32) InternalCall. A new appended
capability authorizes its exact selected-core declaration and image. Application
calls remain rejected. The public Substring overloads retain their existing
range/error order, messages, full-range receiver identity and canonical-empty
shortcuts. This is general library support, with no DiffPlex recognition.

Logical and scalar IR validate an earlier String-compatible source, two earlier
Int32 operands, an exact String result and a fault edge. Allocation/root analysis,
scalar remapping, constructor safety and operand-use analysis recognize the new
operation. It is not admitted into the no-safepoint typed-kernel proof.

Native emission checks nonnull source, ownership/type and overflow-safe unsigned
range bounds before reading payload addresses. A malformed private range uses
IndexOutOfRange; ordinary public argument errors are still raised by the managed
checks before reaching this helper. Empty intrinsic results use the canonical
empty string without null-pointer arithmetic. Nonempty results are fresh copies.

The existing bounded allocator copies source UTF-16 directly into final string
storage. Its allocation observer, quotas, header pool, failure retry and precise
result publication remain unchanged. The caller's real frame retains the source
through collection; this depends on the current non-moving collector. No callback
or safepoint occurs during copying or between allocation and result publication.
One-character results use the existing embedded payload.

The temporary Int32 array and managed copy loop disappear. Reduced quota demand
is intentional: a budget that accommodates source and final result can succeed
without room for an artificial temporary. Genuine allocation failure remains
catchable OutOfMemoryException with the current method/IL origin.

Native profile advances to v116, object operations IR to v80 and integer-object
CIL to v127. Public ABI, object layout, quotas and existing capabilities do not
change.

## Review and evidence

Review checked exact declaration/grant admission, both validators, all operand
remapping, safepoint classification, source/result roots, unsigned range checks,
zero-length pointers, fresh identity, allocator retries, fault origin and shared
header linkage. No outstanding implementation findings.

Forty-eight distinct focused checks pass across substring construction, existing
substring semantics, separator cache, character construction, array-to-string
construction/StringBuilder and embedding strings. They include forced GC,
ASan/UBSan at O0/O2, object/byte quota failures and recovery, private caller/grant
rejection, eight malformed IR variants at both stages and separate native modules.

The new native allocation harness was corrected during review: invalid ranges
may allocate exception objects, and a root in one Context cannot be assumed live
during another Context's collection. Its corrected 13-check class passes.
These were harness assumptions; no production fix was needed.

Both complete DiffPlex outputs match in release and two O1 sanitizer modes, with
diagnostics off/on, forced collection and 512–576 KiB native stacks. The final
uninstrumented comparison reduces line execution from 54.52 to 39.50 µs (27.5%);
character execution remains effectively unchanged. See
[retained measurements and IL/native trace](../performance/substring-20261020.md).
