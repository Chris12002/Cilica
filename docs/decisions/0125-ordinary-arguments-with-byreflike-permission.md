# ADR 0125: Ordinary arguments on byref-like-permitting declarations

Status: Accepted

## Decision

Admit the `AllowByRefLike` generic parameter flag for already-supported ordinary
arguments, while continuing to enforce class, struct, constructor and variance
constraints. The selected .NET 10.0.11 IEquatable declaration carries this flag.
The [runtime specification](https://github.com/dotnet/runtime/blob/main/docs/design/specs/Ecma-335-Augments.md#byreflike-types-in-generics)
defines it as permission for an expanded argument set, not a requirement that an
argument be stack-only. Unknown flags remain rejected.

Actual byref-like values remain outside the executable profile. Snapshot the
reserved IsByRefLikeAttribute marker, including embedded declarations on older
targets, and reject it before executable layout. Reject marked arguments during
bounded constraint traversal, including unused, nested and vector arguments.
Keep specialization keys concerned with identity; semantic attribute inspection
is charged to the existing metadata work ledger. This marker is a conservative
rejection signal and never grants assembly trust or runtime capabilities.

Library type mappings must preserve the marker so a replacement cannot silently
turn stack-only data into heap-compatible data. This slice does not implement
ref struct storage, lifetime checking, escape analysis, boxed value interfaces or
constrained dispatch.

## Evidence and review

All 167 focused checks pass for admission, generic constraints/translation,
snapshots, mappings, layouts and variance. Three direct interface-value probes
supply 882 matching repeated O0/O2 sanitizer results, including 294 new results
for the real runtime IEquatable declaration. Negative tests cover actual C# ref
structs, generic method permissions, nested/unused arguments, embedded/scoped
markers, mapping changes, unknown flags and metadata budgets.

Review moved semantic checks out of repeated identity-key creation and added
mapping-marker preservation. No actionable findings remain. Native kernel
execution and required library member mappings are still pending.
