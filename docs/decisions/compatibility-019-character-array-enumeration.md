# Exact generic Char-array enumeration

2026-10-08. Owned follow-up to batch 016.

## Decision

Admit only the existing generic Enumerate helper for exact Char arrays. Any's
managed array loop already works; discovery also includes the fallback path,
which requires an IEnumerable<Char> target. Reuse ArrayInterface<Char>.Enumerate
and SZGenericArrayEnumerator<Char>, with live Current, reset/disposal, faults,
per-closed-type mutable empty cache and ordinary managed ownership.

Make the narrow recipe predicate depend on the actual vector identity. Shared
discovery and detached validators must admit Char Enumerate while preserving
all other existing gates. Match IEnumerator<Char> by exact closed primitive
identity and require the helper to retain the exact Char receiver. No new
managed library body, intrinsic grant, native operation, quota or GC path.

Char indexed operations/copy and Byte/Boolean generic enumeration remain outside
this slice. Signed aliases and forged receiver/result packets remain rejected.
Interface plan advances from v19 to v20. CIL advances from v104 to v105 after the committed provider-parse slice
30cbcf8; object IR63/native79 are unchanged.

## Validation plan

Owned differential tests cover all UTF-16 units, cursor states and live mutation,
empty cache identity, faults, Any/All/Count, the reduced decoder composition, and
sole-array retention/release across GC. Test denied exact grant and malformed
logical/scalar plans. Preserve prior narrow negative controls and raw failures.
No upstream source execution or full candidate compatibility claim.

## Validation and review

All 90 selected checks pass: 54 focused plus 36 adjacent regressions. New
semantics add 9,588 repeated O0/O2 native/.NET comparisons and four precise native
GC/cache checks. Every UTF-16 unit, cursor state/mutation, boxed Current, Any,
empty cache identity and null/fault order are covered. Exact grants and both-stage
packet mutations retain boundaries. No runtime/core changes or open finding.

The complete owned decoder shape now reaches missing ToUpperInvariant; retain
that exact negative. Initial shared-fixture failures are preserved. See
[batch017](../compatibility-candidates/batch-017-results.md).
