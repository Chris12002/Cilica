# ADR AC0011: Dictionary removal, clearing and reusable slots

Accepted 2026-10-06 for bounded AC3.3.

## Decision

Implement both Dictionary<TKey,TValue>.Remove overloads and Clear as owned managed
code. Retain the existing exact key-operation grants, prime buckets and private
reference entries. Count is the allocated slot count minus the free count.
Removal unlinks the matching bucket-chain node, copies the out value, clears both
key and value payloads, and links the empty slot through the pinned -3 - freeList
encoding. Insert consumes free slots before growing and reuses the existing entry
object. Resize rebuilds chains only for live slots. Clear resets buckets and
entry references while retaining the backing arrays, then resets all counters.

Remove hashes its own key parameter by reference, like the pinned Remove and
TryInsert, rather than reusing Find's by-value hash helper. This preserves mutable
value-key callbacks and the value used in the subsequent equality comparison.
The empty dictionary still validates null keys but skips hashing. Out values are
written only on success or normal not-found return, so a throwing hash/equality
callback leaves the caller's previous out storage intact. No enumeration version
claim is made; enumeration remains a later slice.

All element clearing uses existing typed field writes, including references inside
values. Clearing primitive payloads as well is harmless for the admitted public
surface. Entry slot order and free-list reuse prepare for pinned enumeration
behavior without claiming enumeration is implemented. No compiler profile,
intrinsic request, native operation or ABI changes.

## Evidence and review

Ten authored probes plus the advanced public DictionaryRemove probe provide
3,234 repeated O0/O2 .NET/native comparisons on macOS ARM64 with ASan/UBSan,
forced GC, frame and cleanup checks. They exercise growth, removal/reuse/clear,
colliding chains, head/middle/tail removal, duplicate/missing keys, string identity,
wide keys and nested value/reference outputs, interface calls, null argument order,
and mutable/throwing hash/equality callbacks. Four native GC checks prove direct
and struct-contained values are collectible while their dictionary stays rooted.
38 existing library and public-baseline checks pass.

The first authored collision probe used a constant array initializer and hit the
known RuntimeHelpers.InitializeArray gate. Replace only that test setup with an
equivalent bounded loop; the separate public constant-data blocker remains.
Review checked chain unlinking, free-list bounds, clear/reset and growth invariants,
callback receiver copies, exception-before-mutation ordering, out-value rooting
and release of entry payloads. The ledger advances to 32 gaps and 15 controls.
Private replay has not been rerun; full AC3 remains incomplete.

## Copying follow-up

A local .NET 10.0.11 probe found prerequisites before exact List.CopyTo can be
claimed: negative-index exceptions carry ActualValue in their message, and
Array.Copy downcast failures name the actual element/destination types while
preserving prior successful writes. Array-backed interface copying also needs
runtime element-type relationship checks before copying, including empty arrays.
Implement these explicitly in a subsequent copy slice; do not replace them with
generic store faults or simplified bounds messages. No application rule changes,
hosted work, services or additional agents were used.
