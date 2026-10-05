# ADR 0146: Exact closed empty-array identity

Status: Accepted for Int32 and admitted reference vectors

## Decision

Expose the selected System.Array.Empty<T> declaration through one exact intrinsic
grant. Authenticate its public static generic signature, InternalCall flags,
absence of body/initializers and selected library identity before lowering.
Unsupported unsigned, narrow, wide and inline-value element arrays remain build
errors; assignment compatibility must not merge distinct generic cache identities.

A typed EmptyArray IR operation names the exact admitted vector descriptor and
requires an exception edge and allocation safepoint. Each native runtime instance
caches one zero-length vector per exact descriptor. Reference covariance affects
assignability, not cache identity. Cached arrays are permanent roots, separately
traced through a bounded helper to preserve the 2 KiB native stack-frame limit.
Allocation failure publishes nothing. Cache traversal checks ownership, descriptor,
length and heap count before dereferencing links. Collection detects corrupt cycles.
Profiles advance to typed-reference-ir-v7 and object-frames-native-v27.

## Evidence and review

Three reduced probes compare 882 repeated O0/O2 results with .NET, including
allocation-time collection, generic forwarding, distinct fresh arrays, covariance,
Count and bounds exceptions. Exact-grant denial and unsupported element diagnostics
are checked. Detached IR rejects wrong return types, non-vector descriptors and
missing fault edges. Native sanitizer tests check quota failure, instance isolation,
permanent roots, foreign/unreadable pointers, corrupt cycles/length and marking.

Review preserved distinct reference descriptors and mutation-free cache hits,
checked publication/rooting and retained the stack limit by splitting tracing.
The concrete unmodified kernel advances from Array.Empty<T> to missing List<T>.
Native transcript equivalence and the final milestone audit remain pending.

All 25 composed local array, dictionary, tracing, exception and collection checks
pass. No hosted CI or external application changes were made.
