# ADR 0150: Type initialization exception payload

Status: Accepted for the integer-object library profile

## Decision

Provide the exact granted public TypeInitializationException(String, Exception)
constructor, TypeName getter and Exception.InnerException getter. Authenticate the
selected framework identity and SystemException ancestry. Keep native exception
payloads private; the compiler maps supported members to explicit typed IR.

Retain and trace the original name and inner exception alongside message and
parameter payloads. Public construction eagerly formats the pinned invariant
English message, matching the allocation point of the upstream constructor. A
temporary precise root protects the partially constructed exception while the
message allocates. Failure returns no partial object and preserves pending-fault
provenance. Subsequent Message reads return the retained string. TypeName preserves
a non-null supplied string's identity and maps null to the canonical empty string.
InnerException preserves the supplied identity, including nested chains and null.

Original pinned sources, license and hashes are recorded in library/provenance.json.
This slice does not execute type initializers, cache initialization failures,
implement serialization/HResult, or add general Exception constructor overloads.
Managed and scalar exception IR v3, typed reference IR v9 and native v31 carry the
new contract. The non-moving collector traces all four payload reference slots.

## Evidence and review

Four public probes compare 1,542 repeated native/.NET results at O0/O2 with
ASan/UBSan and collection before allocation. They cover names, exact message code
units (including NUL and unpaired surrogates), identity, nested chains and null
receiver faults. Exact grant denial and four detached-IR mutations are checked.
Native payload checks cover collection, corruption, quota failure, construction
failure, retry and preserved fault origin. Message helpers remain separately
bounded under the 2 KiB native stack limit.

Review corrected eager construction ordering, validated reference ownership and
rooting across both allocations, and retained the existing argument-exception
formatting behavior. Initializer execution remains the next compiler gate.
